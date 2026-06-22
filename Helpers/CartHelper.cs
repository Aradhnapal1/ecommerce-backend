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
                await ApplyVariantSelectionAsync(connection, cart);
            }
            else if (cart.ColorId.HasValue || cart.SizeId.HasValue)
            {
                cart.VariantId = await ResolveVariantIdAsync(
                    connection,
                    cart.ProductId,
                    null,
                    cart.ColorId,
                    cart.SizeId);

                if (cart.VariantId.HasValue)
                {
                    await ApplyVariantSelectionAsync(connection, cart);
                }
                else if (await ProductHasVariantsAsync(connection, cart.ProductId))
                {
                    return (false, "No variant found for the selected color and size.");
                }
            }

            if (cart.SizeId.HasValue &&
                cart.VariantId.HasValue &&
                !await IsSizeAllowedForVariantAsync(connection, cart.VariantId.Value, cart.SizeId.Value))
            {
                return (false, "Selected size is not available for this product variant.");
            }

            if (cart.VariantId.HasValue && !cart.ColorId.HasValue)
            {
                cart.ColorId = await GetVariantColorIdAsync(connection, cart.VariantId.Value);
            }

            return (true, null);
        }

        public static async Task<int?> ResolveVariantIdAsync(
            NpgsqlConnection connection,
            int productId,
            int? variantId,
            int? colorId,
            int? sizeId)
        {
            if (variantId.HasValue)
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

        private static async Task ApplyVariantSelectionAsync(
            NpgsqlConnection connection,
            AddCartModel cart)
        {
            if (!cart.VariantId.HasValue)
                return;

            const string query = """
                SELECT productid, color, sizes
                FROM product_variants
                WHERE id = @variantid
                  AND isactive = TRUE
                LIMIT 1
                """;

            await using var cmd = new NpgsqlCommand(query, connection);
            cmd.Parameters.AddWithValue("@variantid", cart.VariantId.Value);

            await using var reader = await cmd.ExecuteReaderAsync();
            if (!await reader.ReadAsync())
                return;

            cart.ProductId = Convert.ToInt32(reader["productid"]);

            if (!cart.ColorId.HasValue && reader["color"] != DBNull.Value)
            {
                var colorText = reader["color"]?.ToString();
                if (int.TryParse(colorText, out var parsedColorId))
                    cart.ColorId = parsedColorId;
            }

            if (!cart.SizeId.HasValue && reader["sizes"] != DBNull.Value)
            {
                var sizes = reader.GetFieldValue<int[]>(reader.GetOrdinal("sizes"));
                if (sizes.Length > 0)
                    cart.SizeId = sizes[0];
            }
        }

        private static async Task<bool> ProductHasVariantsAsync(
            NpgsqlConnection connection,
            int productId)
        {
            const string query = """
                SELECT EXISTS(
                    SELECT 1
                    FROM product_variants
                    WHERE productid = @productid
                      AND isactive = TRUE
                )
                """;

            await using var cmd = new NpgsqlCommand(query, connection);
            cmd.Parameters.AddWithValue("@productid", productId);
            var result = await cmd.ExecuteScalarAsync();
            return result is bool exists && exists;
        }

        private static async Task<bool> IsSizeAllowedForVariantAsync(
            NpgsqlConnection connection,
            int variantId,
            int sizeId)
        {
            const string query = """
                SELECT @sizeid = ANY(sizes)
                FROM product_variants
                WHERE id = @variantid
                LIMIT 1
                """;

            await using var cmd = new NpgsqlCommand(query, connection);
            cmd.Parameters.AddWithValue("@variantid", variantId);
            cmd.Parameters.AddWithValue("@sizeid", sizeId);

            var result = await cmd.ExecuteScalarAsync();
            return result is bool allowed && allowed;
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
