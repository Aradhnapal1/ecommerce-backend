using Npgsql;

namespace Ecommerce_Backend.Helpers
{
    /// <summary>
    /// Central inventory helpers: deduct stock on order, sync product stock, out-of-stock checks.
    /// </summary>
    public static class StockHelper
    {
        /// <summary>
        /// Deduct quantity from variant stock (if variantId &gt; 0) or product stock.
        /// After variant deduct, syncs products.stock = SUM(active variant stocks).
        /// </summary>
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
                const string variantSql = """
                    UPDATE product_variants
                    SET stock = stock - @Quantity,
                        updatedat = NOW()
                    WHERE id = @VariantId
                      AND productid = @ProductId
                      AND stock >= @Quantity
                    """;

                await using var variantCmd = transaction == null
                    ? new NpgsqlCommand(variantSql, con)
                    : new NpgsqlCommand(variantSql, con, transaction);
                variantCmd.Parameters.AddWithValue("@Quantity", quantity);
                variantCmd.Parameters.AddWithValue("@VariantId", variantId);
                variantCmd.Parameters.AddWithValue("@ProductId", productId);

                if (await variantCmd.ExecuteNonQueryAsync() == 0)
                {
                    throw new InvalidOperationException(
                        $"Insufficient stock for {productName ?? "product"} (variant).");
                }

                await SyncProductStockFromVariantsAsync(con, transaction, productId);
                return;
            }

            const string productSql = """
                UPDATE products
                SET stock = stock - @Quantity,
                    updatedat = NOW()
                WHERE id = @ProductId
                  AND stock >= @Quantity
                """;

            await using var productCmd = transaction == null
                ? new NpgsqlCommand(productSql, con)
                : new NpgsqlCommand(productSql, con, transaction);
            productCmd.Parameters.AddWithValue("@Quantity", quantity);
            productCmd.Parameters.AddWithValue("@ProductId", productId);

            if (await productCmd.ExecuteNonQueryAsync() == 0)
            {
                throw new InvalidOperationException(
                    $"Insufficient stock for {productName ?? "product"}.");
            }
        }

        /// <summary>Restore stock when an order is cancelled (after stock was deducted).</summary>
        public static async Task RestoreAsync(
            NpgsqlConnection con,
            NpgsqlTransaction? transaction,
            int productId,
            int variantId,
            int quantity)
        {
            if (quantity <= 0)
                return;

            if (variantId > 0)
            {
                const string variantSql = """
                    UPDATE product_variants
                    SET stock = stock + @Quantity,
                        updatedat = NOW()
                    WHERE id = @VariantId
                      AND productid = @ProductId
                    """;

                await using var variantCmd = transaction == null
                    ? new NpgsqlCommand(variantSql, con)
                    : new NpgsqlCommand(variantSql, con, transaction);
                variantCmd.Parameters.AddWithValue("@Quantity", quantity);
                variantCmd.Parameters.AddWithValue("@VariantId", variantId);
                variantCmd.Parameters.AddWithValue("@ProductId", productId);
                await variantCmd.ExecuteNonQueryAsync();

                await SyncProductStockFromVariantsAsync(con, transaction, productId);
                return;
            }

            const string productSql = """
                UPDATE products
                SET stock = stock + @Quantity,
                    updatedat = NOW()
                WHERE id = @ProductId
                """;

            await using var productCmd = transaction == null
                ? new NpgsqlCommand(productSql, con)
                : new NpgsqlCommand(productSql, con, transaction);
            productCmd.Parameters.AddWithValue("@Quantity", quantity);
            productCmd.Parameters.AddWithValue("@ProductId", productId);
            await productCmd.ExecuteNonQueryAsync();
        }

        /// <summary>
        /// Keep products.stock in sync with sum of active variant stocks (for listing / filters).
        /// </summary>
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
                        WHERE pv.productid = p.id
                          AND pv.isactive = TRUE
                    ), p.stock),
                    updatedat = NOW()
                WHERE p.id = @ProductId
                  AND EXISTS (
                      SELECT 1 FROM product_variants pv2
                      WHERE pv2.productid = p.id AND pv2.isactive = TRUE
                  )
                """;

            await using var cmd = transaction == null
                ? new NpgsqlCommand(sql, con)
                : new NpgsqlCommand(sql, con, transaction);
            cmd.Parameters.AddWithValue("@ProductId", productId);
            await cmd.ExecuteNonQueryAsync();
        }

        public static async Task<int> GetAvailableStockAsync(
            NpgsqlConnection con,
            int productId,
            int? variantId)
        {
            if (variantId is > 0)
            {
                const string q = """
                    SELECT stock
                    FROM product_variants
                    WHERE id = @VariantId
                      AND productid = @ProductId
                      AND isactive = TRUE
                    LIMIT 1
                    """;
                await using var cmd = new NpgsqlCommand(q, con);
                cmd.Parameters.AddWithValue("@VariantId", variantId.Value);
                cmd.Parameters.AddWithValue("@ProductId", productId);
                var result = await cmd.ExecuteScalarAsync();
                return result == null || result == DBNull.Value ? 0 : Convert.ToInt32(result);
            }

            // Prefer sum of active variants when product has variants
            const string productQ = """
                SELECT CASE
                    WHEN EXISTS (
                        SELECT 1 FROM product_variants pv
                        WHERE pv.productid = p.id AND pv.isactive = TRUE
                    )
                    THEN COALESCE((
                        SELECT SUM(pv.stock)::int
                        FROM product_variants pv
                        WHERE pv.productid = p.id AND pv.isactive = TRUE
                    ), 0)
                    ELSE COALESCE(p.stock, 0)
                END
                FROM products p
                WHERE p.id = @ProductId
                LIMIT 1
                """;

            await using var productCmd = new NpgsqlCommand(productQ, con);
            productCmd.Parameters.AddWithValue("@ProductId", productId);
            var stock = await productCmd.ExecuteScalarAsync();
            return stock == null || stock == DBNull.Value ? 0 : Convert.ToInt32(stock);
        }

        public static bool IsOutOfStock(int stock) => stock <= 0;

        public static string StockStatus(int stock) =>
            stock <= 0 ? "Out of Stock" : "In Stock";
    }
}
