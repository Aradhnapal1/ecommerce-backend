using Npgsql;

namespace Ecommerce_Backend.Helpers
{
    public static class DatabaseSchemaBootstrapper
    {
        public static async Task EnsureSchemaAsync(string connectionString, ILogger logger)
        {
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                logger.LogError("Database connection string is empty. Configure ConnectionStrings__AppDbContextConnection on the server.");
                return;
            }

            try
            {
                await using var conn = new NpgsqlConnection(connectionString);
                await conn.OpenAsync();

                const string sql = """
                    ALTER TABLE user_register ALTER COLUMN otp TYPE VARCHAR(128);
                    ALTER TABLE user_register ADD COLUMN IF NOT EXISTS otp_expires_at TIMESTAMP;
                    CREATE INDEX IF NOT EXISTS ix_user_register_otp_expires_at ON user_register (otp_expires_at);

                    ALTER TABLE categories ADD COLUMN IF NOT EXISTS slug VARCHAR(255);
                    ALTER TABLE brands ADD COLUMN IF NOT EXISTS slug VARCHAR(255);
                    ALTER TABLE colors ADD COLUMN IF NOT EXISTS slug VARCHAR(255);
                    ALTER TABLE sizes ADD COLUMN IF NOT EXISTS slug VARCHAR(255);

                    ALTER TABLE orders ADD COLUMN IF NOT EXISTS razorpay_order_id VARCHAR(255);
                    ALTER TABLE orders ADD COLUMN IF NOT EXISTS razorpay_payment_id VARCHAR(255);
                    ALTER TABLE orders ADD COLUMN IF NOT EXISTS razorpay_signature TEXT;
                    """;

                await using var cmd = new NpgsqlCommand(sql, conn);
                await cmd.ExecuteNonQueryAsync();
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex,
                    "Could not apply schema bootstrap. Run Scripts/add_security_columns.sql and Scripts/add_filter_slugs.sql manually.");
            }
        }
    }
}
