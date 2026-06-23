using Ecommerce_Backend.Models;
using Npgsql;

namespace Ecommerce_Backend.Helpers
{
    public static class CartHelper
    {
        public static async Task<(bool Success, string? ErrorMessage)> PrepareCartSelectionAsync(
            NpgsqlConnection connection,
            AddCartModel cart)
        {
            cart.NormalizeFormFields();

            if (!cart.ColorId.HasValue && !string.IsNullOrWhiteSpace(cart.ColorSlug))
            {
                cart.ColorId = await ResolveColorIdAsync(connection, cart.ColorSlug);
            }

            if (!cart.SizeId.HasValue && !string.IsNullOrWhiteSpace(cart.SizeSlug))
            {
                cart.SizeId = await ResolveSizeIdAsync(connection, cart.SizeSlug);
            }

            if (cart.VariantId.HasValue)
            {
                var variant = await GetVariantByIdAsync(connection, cart.VariantId.Value);
                if (variant != null)
                {
                    ApplyVariantToCart(cart, variant);
                }
                else
                {
                    // Frontend may send stale/invalid variantId (e.g. 0) for simple products.
                    cart.VariantId = null;
                }
            }

            if (!cart.VariantId.HasValue && cart.ProductId > 0)
            {
                if (!await ProductExistsAsync(connection, cart.ProductId))
                {
                    return (false, "Product not found");
                }

                await ResolveOrApplyDefaultForProductAsync(connection, cart);
            }
            else if (!cart.VariantId.HasValue)
            {
                return (false, "ProductId is required.");
            }

            await EnsureValidSizeForVariantAsync(connection, cart);

            if (!cart.ColorId.HasValue && cart.VariantId.HasValue)
            {
                cart.ColorId = await GetVariantColorIdAsync(connection, cart.VariantId.Value);
            }

            if (!cart.SizeId.HasValue && cart.ProductId > 0)
            {
                await ApplyProductDefaultsAsync(connection, cart);
            }

            return (true, null);
        }

        private static async Task ResolveOrApplyDefaultForProductAsync(
            NpgsqlConnection connection,
            AddCartModel cart)
        {
            if (cart.ColorId.HasValue || cart.SizeId.HasValue)
            {
                cart.VariantId = await ResolveVariantIdAsync(
                    connection,
                    cart.ProductId,
                    null,
                    cart.ColorId,
                    cart.SizeId);

                if (cart.VariantId.HasValue)
                {
                    var variant = await GetVariantByIdAsync(connection, cart.VariantId.Value);
                    if (variant != null)
                    {
                        ApplyVariantToCart(cart, variant, preserveSelectedSize: true);
                        return;
                    }
                }
            }

            await ApplyDefaultSelectionForProductAsync(connection, cart);
        }

        private static async Task ApplyDefaultSelectionForProductAsync(
            NpgsqlConnection connection,
            AddCartModel cart)
        {
            var defaultVariant = await GetDefaultVariantAsync(connection, cart.ProductId);
            if (defaultVariant != null)
            {
                ApplyVariantToCart(cart, defaultVariant);
                return;
            }

            cart.VariantId = null;
            await ApplyProductDefaultsAsync(connection, cart);
        }

        private static async Task EnsureValidSizeForVariantAsync(
            NpgsqlConnection connection,
            AddCartModel cart)
        {
            if (!cart.VariantId.HasValue)
                return;

            var variant = await GetVariantByIdAsync(connection, cart.VariantId.Value);
            if (variant == null)
                return;

            if (!cart.SizeId.HasValue)
            {
                cart.SizeId = variant.DefaultSizeId;
                return;
            }

            if (!variant.Sizes.Contains(cart.SizeId.Value))
            {
                cart.SizeId = variant.DefaultSizeId;
            }
        }

        public static async Task<int?> ResolveVariantIdAsync(
            NpgsqlConnection connection,
            int productId,
            int? variantId,
            int? colorId,
            int? sizeId)
        {
            if (variantId is > 0)
                return variantId;

            if (!colorId.HasValue && !sizeId.HasValue)
                return null;

            const string query = """
                SELECT id
                FROM product_variants
                WHERE productid = @productid
                  AND isactive = TRUE
                  AND (@colorid IS NULL OR CAST(NULLIF(color, '') AS INTEGER) = @colorid)
                  AND (@sizeid IS NULL OR @sizeid = ANY(sizes))
                ORDER BY id
                LIMIT 1
                """;

            await using var cmd = new NpgsqlCommand(query, connection);
            cmd.Parameters.AddWithValue("@productid", productId);
            cmd.Parameters.AddWithValue("@colorid", (object?)colorId ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@sizeid", (object?)sizeId ?? DBNull.Value);

            var result = await cmd.ExecuteScalarAsync();
            return result == null ? null : Convert.ToInt32(result);
        }

        public static async Task MergeGuestCartToUserAsync(
            NpgsqlConnection connection,
            int userId,
            string ipAddress)
        {
            if (string.IsNullOrWhiteSpace(ipAddress))
                return;

            const string mergeQuantities = """
                UPDATE addcart AS u
                SET quantity = u.quantity + g.quantity,
                    updatedat = NOW()
                FROM addcart AS g
                WHERE g.userid IS NULL
                  AND g.ipaddress = @ipaddress
                  AND u.userid = @userid
                  AND u.productid = g.productid
                  AND COALESCE(u.variantid, 0) = COALESCE(g.variantid, 0)
                  AND COALESCE(u.colorid, 0) = COALESCE(g.colorid, 0)
                  AND COALESCE(u.sizeid, 0) = COALESCE(g.sizeid, 0)
                """;

            await using (var mergeCmd = new NpgsqlCommand(mergeQuantities, connection))
            {
                mergeCmd.Parameters.AddWithValue("@userid", userId);
                mergeCmd.Parameters.AddWithValue("@ipaddress", ipAddress);
                await mergeCmd.ExecuteNonQueryAsync();
            }

            const string deleteDuplicates = """
                DELETE FROM addcart AS g
                WHERE g.userid IS NULL
                  AND g.ipaddress = @ipaddress
                  AND EXISTS (
                      SELECT 1
                      FROM addcart AS u
                      WHERE u.userid = @userid
                        AND u.productid = g.productid
                        AND COALESCE(u.variantid, 0) = COALESCE(g.variantid, 0)
                        AND COALESCE(u.colorid, 0) = COALESCE(g.colorid, 0)
                        AND COALESCE(u.sizeid, 0) = COALESCE(g.sizeid, 0)
                  )
                """;

            await using (var deleteCmd = new NpgsqlCommand(deleteDuplicates, connection))
            {
                deleteCmd.Parameters.AddWithValue("@userid", userId);
                deleteCmd.Parameters.AddWithValue("@ipaddress", ipAddress);
                await deleteCmd.ExecuteNonQueryAsync();
            }

            const string moveRemaining = """
                UPDATE addcart
                SET userid = @userid,
                    ipaddress = NULL,
                    updatedat = NOW()
                WHERE userid IS NULL
                  AND ipaddress = @ipaddress
                """;

            await using var moveCmd = new NpgsqlCommand(moveRemaining, connection);
            moveCmd.Parameters.AddWithValue("@userid", userId);
            moveCmd.Parameters.AddWithValue("@ipaddress", ipAddress);
            await moveCmd.ExecuteNonQueryAsync();
        }

        private sealed class VariantInfo
        {
            public int Id { get; init; }
            public int ProductId { get; init; }
            public int? ColorId { get; init; }
            public int[] Sizes { get; init; } = Array.Empty<int>();
            public int? DefaultSizeId =>
                Sizes.Length > 0 ? Sizes.OrderBy(static s => s).First() : null;
        }

        private static void ApplyVariantToCart(
            AddCartModel cart,
            VariantInfo variant,
            bool preserveSelectedSize = false)
        {
            cart.ProductId = variant.ProductId;
            cart.VariantId = variant.Id;
            cart.ColorId ??= variant.ColorId;

            if (!preserveSelectedSize || !cart.SizeId.HasValue)
            {
                cart.SizeId ??= variant.DefaultSizeId;
            }
        }

        private static async Task<bool> ProductExistsAsync(
            NpgsqlConnection connection,
            int productId)
        {
            const string query = """
                SELECT EXISTS(
                    SELECT 1
                    FROM products
                    WHERE id = @productid
                      AND isactive = TRUE
                )
                """;

            await using var cmd = new NpgsqlCommand(query, connection);
            cmd.Parameters.AddWithValue("@productid", productId);
            var result = await cmd.ExecuteScalarAsync();
            return result is bool exists && exists;
        }

        private static async Task<VariantInfo?> GetVariantByIdAsync(
            NpgsqlConnection connection,
            int variantId)
        {
            const string query = """
                SELECT id, productid, color, sizes
                FROM product_variants
                WHERE id = @variantid
                LIMIT 1
                """;

            await using var cmd = new NpgsqlCommand(query, connection);
            cmd.Parameters.AddWithValue("@variantid", variantId);

            await using var reader = await cmd.ExecuteReaderAsync();
            if (!await reader.ReadAsync())
                return null;

            return MapVariant(reader);
        }

        private static async Task<VariantInfo?> GetDefaultVariantAsync(
            NpgsqlConnection connection,
            int productId)
        {
            const string query = """
                SELECT id, productid, color, sizes
                FROM product_variants
                WHERE productid = @productid
                  AND isactive = TRUE
                ORDER BY id
                LIMIT 1
                """;

            await using var cmd = new NpgsqlCommand(query, connection);
            cmd.Parameters.AddWithValue("@productid", productId);

            await using var reader = await cmd.ExecuteReaderAsync();
            if (!await reader.ReadAsync())
                return null;

            return MapVariant(reader);
        }

        private static VariantInfo MapVariant(NpgsqlDataReader reader)
        {
            int? colorId = null;
            if (reader["color"] != DBNull.Value)
            {
                var colorText = reader["color"]?.ToString();
                if (int.TryParse(colorText, out var parsedColorId))
                    colorId = parsedColorId;
            }

            var sizes = reader["sizes"] == DBNull.Value
                ? Array.Empty<int>()
                : reader.GetFieldValue<int[]>(reader.GetOrdinal("sizes"));

            return new VariantInfo
            {
                Id = Convert.ToInt32(reader["id"]),
                ProductId = Convert.ToInt32(reader["productid"]),
                ColorId = colorId,
                Sizes = sizes
            };
        }

        private static async Task ApplyProductDefaultsAsync(
            NpgsqlConnection connection,
            AddCartModel cart)
        {
            if (cart.ColorId.HasValue && cart.SizeId.HasValue)
                return;

            const string query = """
                SELECT color, sizes
                FROM products
                WHERE id = @productid
                LIMIT 1
                """;

            await using var cmd = new NpgsqlCommand(query, connection);
            cmd.Parameters.AddWithValue("@productid", cart.ProductId);

            await using var reader = await cmd.ExecuteReaderAsync();
            if (!await reader.ReadAsync())
                return;

            if (!cart.ColorId.HasValue && reader["color"] != DBNull.Value)
            {
                var colorText = reader["color"]?.ToString();
                if (int.TryParse(colorText, out var parsedColorId))
                    cart.ColorId = parsedColorId;
            }

            if (!cart.SizeId.HasValue && reader["sizes"] != DBNull.Value)
            {
                try
                {
                    var sizes = reader.GetFieldValue<int[]>(reader.GetOrdinal("sizes"));
                    if (sizes.Length > 0)
                        cart.SizeId = sizes.OrderBy(static s => s).First();
                }
                catch
                {
                    // Legacy text[] sizes column — ignore and leave size null.
                }
            }
        }

        private static async Task<int?> GetVariantColorIdAsync(
            NpgsqlConnection connection,
            int variantId)
        {
            const string query = """
                SELECT CAST(NULLIF(color, '') AS INTEGER)
                FROM product_variants
                WHERE id = @variantid
                LIMIT 1
                """;

            await using var cmd = new NpgsqlCommand(query, connection);
            cmd.Parameters.AddWithValue("@variantid", variantId);

            var result = await cmd.ExecuteScalarAsync();
            return result == null || result == DBNull.Value
                ? null
                : Convert.ToInt32(result);
        }

        private static async Task<int?> ResolveColorIdAsync(
            NpgsqlConnection connection,
            string colorSlug)
        {
            const string query = """
                SELECT id
                FROM colors
                WHERE LOWER(slug) = LOWER(@slug)
                   OR LOWER(color_name) = LOWER(@slug)
                LIMIT 1
                """;

            await using var cmd = new NpgsqlCommand(query, connection);
            cmd.Parameters.AddWithValue("@slug", colorSlug.Trim());

            var result = await cmd.ExecuteScalarAsync();
            return result == null ? null : Convert.ToInt32(result);
        }

        private static async Task<int?> ResolveSizeIdAsync(
            NpgsqlConnection connection,
            string sizeSlug)
        {
            const string query = """
                SELECT id
                FROM sizes
                WHERE LOWER(slug) = LOWER(@slug)
                   OR LOWER(size_name) = LOWER(@slug)
                LIMIT 1
                """;

            await using var cmd = new NpgsqlCommand(query, connection);
            cmd.Parameters.AddWithValue("@slug", sizeSlug.Trim());

            var result = await cmd.ExecuteScalarAsync();
            return result == null ? null : Convert.ToInt32(result);
        }
    }
}
