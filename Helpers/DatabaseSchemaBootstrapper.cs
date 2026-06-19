using Npgsql;

namespace Ecommerce_Backend.Helpers
{
    public static class DatabaseSchemaBootstrapper
    {
        public static async Task EnsureSecurityColumnsAsync(string connectionString, ILogger logger)
        {
            try
            {
                await using var conn = new NpgsqlConnection(connectionString);
                await conn.OpenAsync();

                const string sql = """
                    ALTER TABLE user_register ALTER COLUMN otp TYPE VARCHAR(128);
                    ALTER TABLE user_register ADD COLUMN IF NOT EXISTS otp_expires_at TIMESTAMP;
                    CREATE INDEX IF NOT EXISTS ix_user_register_otp_expires_at ON user_register (otp_expires_at);
                    """;

                await using var cmd = new NpgsqlCommand(sql, conn);
                await cmd.ExecuteNonQueryAsync();
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Could not apply security schema bootstrap (otp_expires_at). Run Scripts/add_security_columns.sql manually.");
            }
        }
    }
}
