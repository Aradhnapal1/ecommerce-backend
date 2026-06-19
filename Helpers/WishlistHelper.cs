using Npgsql;

namespace Ecommerce_Backend.Helpers
{
    public static class WishlistHelper
    {
        public static async Task MergeGuestWishlistToUserAsync(
            NpgsqlConnection connection,
            int userId,
            string ipAddress)
        {
            if (string.IsNullOrWhiteSpace(ipAddress))
                return;

            const string deleteDuplicates = """
                DELETE FROM wishlist AS g
                WHERE g.userid IS NULL
                  AND g.ipaddress = @ipaddress
                  AND EXISTS (
                      SELECT 1
                      FROM wishlist AS u
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
                UPDATE wishlist
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
