using Npgsql;

namespace Ecommerce_Backend.Helpers
{
    public static class StockHelper
    {
        public static async Task DeductAsync(
            NpgsqlConnection con,
            NpgsqlTransaction? transaction,
            int productId,
            int variantId,
            int quantity,
            string? productName = null)
        {
            if (quantity <= 0)
                throw new InvalidOperationException("Quantity must be greater than 0.");

            if (variantId > 0)
            {
                // Single CTE: deduct variant + sync product stock in one roundtrip
                const string sql = """
                    WITH v AS (
                        UPDATE product_variants
                        SET stock = GREATEST(stock - @qty, 0),
                            updatedat = NOW()
                        WHERE id = @vid AND productid = @pid AND stock >= @qty
                        RETURNING productid, stock
                    ),
                    sync AS (
                        UPDATE products
                        SET stock = COALESCE((
                                SELECT SUM(pv.stock)::int
                                FROM product_variants pv
                                WHERE pv.productid = products.id AND pv.isactive = TRUE
                            ), stock),
                            updatedat = NOW()
                        WHERE id IN (SELECT productid FROM v)
                        RETURNING id
                    )
                    SELECT COUNT(*) FROM v
                    """;

                await using var cmd = Cmd(sql, con, transaction);
                cmd.Parameters.AddWithValue("@qty", quantity);
                cmd.Parameters.AddWithValue("@vid", variantId);
                cmd.Parameters.AddWithValue("@pid", productId);

                var affected = Convert.ToInt32(await cmd.ExecuteScalarAsync());
                if (affected == 0)
                    throw new InvalidOperationException(
                        $"Insufficient stock for {productName ?? "product"} (variant).");

                return;
            }

            const string productSql = """
                UPDATE products
                SET stock = GREATEST(stock - @qty, 0),
                    updatedat = NOW()
                WHERE id = @pid AND stock >= @qty
                """;

            await using var productCmd = Cmd(productSql, con, transaction);
            productCmd.Parameters.AddWithValue("@qty", quantity);
            productCmd.Parameters.AddWithValue("@pid", productId);

            if (await productCmd.ExecuteNonQueryAsync() == 0)
                throw new InvalidOperationException(
                    $"Insufficient stock for {productName ?? "product"}.");
        }

        public static async Task RestoreAsync(
            NpgsqlConnection con,
            NpgsqlTransaction? transaction,
            int productId,
            int variantId,
            int quantity)
        {
            if (quantity <= 0) return;

            if (variantId > 0)
            {
                const string sql = """
                    WITH v AS (
                        UPDATE product_variants
                        SET stock = stock + @qty,
                            updatedat = NOW()
                        WHERE id = @vid AND productid = @pid
                        RETURNING productid
                    )
                    UPDATE products
                    SET stock = COALESCE((
                            SELECT SUM(pv.stock)::int
                            FROM product_variants pv
                            WHERE pv.productid = products.id AND pv.isactive = TRUE
                        ), stock),
                        updatedat = NOW()
                    WHERE id IN (SELECT productid FROM v)
                    """;

                await using var cmd = Cmd(sql, con, transaction);
                cmd.Parameters.AddWithValue("@qty", quantity);
                cmd.Parameters.AddWithValue("@vid", variantId);
                cmd.Parameters.AddWithValue("@pid", productId);
                await cmd.ExecuteNonQueryAsync();
                return;
            }

            const string productSql = """
                UPDATE products
                SET stock = stock + @qty, updatedat = NOW()
                WHERE id = @pid
                """;

            await using var productCmd = Cmd(productSql, con, transaction);
            productCmd.Parameters.AddWithValue("@qty", quantity);
            productCmd.Parameters.AddWithValue("@pid", productId);
            await productCmd.ExecuteNonQueryAsync();
        }

        public static async Task SyncProductStockFromVariantsAsync(
            NpgsqlConnection con,
            NpgsqlTransaction? transaction,
            int productId)
        {
            const string sql = """
                UPDATE products p
                SET stock = COALESCE((
                        SELECT SUM(pv.stock)::int
                        FROM product_variants pv
                        WHERE pv.productid = p.id AND pv.isactive = TRUE
                    ), p.stock),
                    updatedat = NOW()
                WHERE p.id = @pid
                  AND EXISTS (
                      SELECT 1 FROM product_variants pv2
                      WHERE pv2.productid = p.id AND pv2.isactive = TRUE
                  )
                """;

            await using var cmd = Cmd(sql, con, transaction);
            cmd.Parameters.AddWithValue("@pid", productId);
            await cmd.ExecuteNonQueryAsync();
        }

        public static async Task<int> GetAvailableStockAsync(
            NpgsqlConnection con,
            int productId,
            int? variantId)
        {
            if (variantId is > 0)
            {
                const string q = "SELECT COALESCE(stock, 0) FROM product_variants WHERE id = @vid AND productid = @pid AND isactive = TRUE LIMIT 1";
                await using var cmd = new NpgsqlCommand(q, con);
                cmd.Parameters.AddWithValue("@vid", variantId.Value);
                cmd.Parameters.AddWithValue("@pid", productId);
                var r = await cmd.ExecuteScalarAsync();
                return r == null || r == DBNull.Value ? 0 : Convert.ToInt32(r);
            }

            const string pq = """
                SELECT CASE
                    WHEN EXISTS (SELECT 1 FROM product_variants pv WHERE pv.productid = p.id AND pv.isactive = TRUE)
                    THEN COALESCE((SELECT SUM(pv.stock)::int FROM product_variants pv WHERE pv.productid = p.id AND pv.isactive = TRUE), 0)
                    ELSE COALESCE(p.stock, 0)
                END
                FROM products p WHERE p.id = @pid LIMIT 1
                """;

            await using var pc = new NpgsqlCommand(pq, con);
            pc.Parameters.AddWithValue("@pid", productId);
            var s = await pc.ExecuteScalarAsync();
            return s == null || s == DBNull.Value ? 0 : Convert.ToInt32(s);
        }

        public static bool IsOutOfStock(int stock) => stock <= 0;
        public static string StockStatus(int stock) => stock <= 0 ? "Out of Stock" : "In Stock";

        private static NpgsqlCommand Cmd(string sql, NpgsqlConnection con, NpgsqlTransaction? tx) =>
            tx == null ? new NpgsqlCommand(sql, con) : new NpgsqlCommand(sql, con, tx);
    }
}
