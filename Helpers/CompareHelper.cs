using Npgsql;

namespace Ecommerce_Backend.Helpers
{
    public static class CompareHelper
    {
        public const int MaxCompareItems = 4;

        public static async Task MergeGuestCompareToUserAsync(
            NpgsqlConnection connection,
            int userId,
            string ipAddress)
        {
            if (string.IsNullOrWhiteSpace(ipAddress))
                return;

            const string deleteDuplicates = """
                DELETE FROM product_compare AS g
                WHERE g.userid IS NULL
                  AND g.ipaddress = @ipaddress
                  AND EXISTS (
                      SELECT 1
                      FROM product_compare AS u
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

            const string trimExcess = """
                DELETE FROM product_compare
                WHERE id IN (
                    SELECT id FROM (
                        SELECT id,
                               ROW_NUMBER() OVER (ORDER BY createdat DESC) AS rn
                        FROM product_compare
                        WHERE userid = @userid
                    ) ranked
                    WHERE rn > @maxItems
                )
                """;

            await using (var trimCmd = new NpgsqlCommand(trimExcess, connection))
            {
                trimCmd.Parameters.AddWithValue("@userid", userId);
                trimCmd.Parameters.AddWithValue("@maxItems", MaxCompareItems);
                await trimCmd.ExecuteNonQueryAsync();
            }

            const string moveRemaining = """
                UPDATE product_compare
                SET userid = @userid,
                    ipaddress = NULL
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
