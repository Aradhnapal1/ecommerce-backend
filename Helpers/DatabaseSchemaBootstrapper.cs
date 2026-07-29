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

                    ALTER TABLE addcart ADD COLUMN IF NOT EXISTS colorid INT NULL;
                    ALTER TABLE addcart ADD COLUMN IF NOT EXISTS sizeid INT NULL;

                    ALTER TABLE products ALTER COLUMN shortdescription TYPE TEXT;
                    ALTER TABLE products ALTER COLUMN sizes DROP NOT NULL;
                    ALTER TABLE product_variants ALTER COLUMN sizes DROP NOT NULL;

                    CREATE TABLE IF NOT EXISTS product_compare (
                        id SERIAL PRIMARY KEY,
                        userid INT NULL,
                        productid INT NOT NULL,
                        variantid INT NULL,
                        ipaddress VARCHAR(100),
                        createdat TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
                        CONSTRAINT fk_product_compare_user
                            FOREIGN KEY (userid) REFERENCES user_register(id) ON DELETE CASCADE,
                        CONSTRAINT fk_product_compare_product
                            FOREIGN KEY (productid) REFERENCES products(id) ON DELETE CASCADE,
                        CONSTRAINT fk_product_compare_variant
                            FOREIGN KEY (variantid) REFERENCES product_variants(id) ON DELETE CASCADE
                    );
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
