using Npgsql;

namespace Ecommerce_Backend.Helpers
{
    public static class CartHelper
    {
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
                  AND (@colorid IS NULL OR color = @colorid::text)
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
    }
}
