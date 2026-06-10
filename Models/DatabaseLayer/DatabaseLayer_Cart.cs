using Microsoft.AspNetCore.Mvc;
using Npgsql;

namespace Ecommerce_Backend.Models.DatabaseLayer
{
   public partial interface IDatabaseLayer
    {
        Task<List<CartModel>> GetAllCartItems();
        Task<IActionResult> AddCartItem([FromForm] CartModel cartItem);

    }
    public partial class DataBaseLayer : IDatabaseLayer
    {

        public async Task<List<CartModel>> GetAllCartItems()
        {
            var cartItems = new List<CartModel>();

            using var connection = new NpgsqlConnection(DbConnection);
            await connection.OpenAsync();

            using var command = new NpgsqlCommand(@"
        SELECT
            id,
            userid,
            ipaddress,
            productid,
            variantid,
            quantity,
            mrp,
            saleprice,
            totalprice,
            createdat,
            updatedat
        FROM cart
        ORDER BY id DESC;
    ", connection);

            using var reader = await command.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                cartItems.Add(new CartModel
                {
                    Id = Convert.ToInt32(reader["id"]),

                    UserId = reader["userid"] == DBNull.Value
                        ? null
                        : Convert.ToInt32(reader["userid"]),

                    IpAddress = reader["ipaddress"] == DBNull.Value
                        ? null
                        : reader["ipaddress"].ToString(),

                    ProductId = reader["productid"] == DBNull.Value
                        ? null
                        : Convert.ToInt32(reader["productid"]),

                    VariantId = reader["variantid"] == DBNull.Value
                        ? null
                        : Convert.ToInt32(reader["variantid"]),

                    Quantity = Convert.ToInt32(reader["quantity"]),
                    Mrp = Convert.ToDecimal(reader["mrp"]),
                    SalePrice = Convert.ToDecimal(reader["saleprice"]),
                    TotalPrice = Convert.ToDecimal(reader["totalprice"]),
                    CreatedAt = Convert.ToDateTime(reader["createdat"]),
                    UpdatedAt = Convert.ToDateTime(reader["updatedat"])
                });
            }

            return cartItems;
        }
        public async Task<IActionResult> AddCartItem(CartModel cartItem)
        {
            try
            {
                using var connection = new NpgsqlConnection(DbConnection);
                await connection.OpenAsync();

                if (!cartItem.UserId.HasValue && string.IsNullOrWhiteSpace(cartItem.IpAddress))
                {
                    return new BadRequestObjectResult(new
                    {
                        success = false,
                        message = "UserId or IpAddress is required"
                    });
                }

                decimal mrp = 0;
                decimal salePrice = 0;

                // Fetch MRP and SalePrice
                if (cartItem.VariantId.HasValue)
                {
                    using var cmd = new NpgsqlCommand(@"
                SELECT mrp, saleprice 
                FROM product_variants 
                WHERE id = @id", connection);
                    cmd.Parameters.AddWithValue("@id", cartItem.VariantId.Value);
                    using var reader = await cmd.ExecuteReaderAsync();
                    if (!await reader.ReadAsync())
                    {
                        return new BadRequestObjectResult(new { success = false, message = "Invalid VariantId" });
                    }
                    mrp = Convert.ToDecimal(reader["mrp"]);
                    salePrice = Convert.ToDecimal(reader["saleprice"]);
                }
                else if (cartItem.ProductId.HasValue)
                {
                    using var cmd = new NpgsqlCommand(@"
                SELECT mrp, saleprice 
                FROM products 
                WHERE id = @id", connection);
                    cmd.Parameters.AddWithValue("@id", cartItem.ProductId.Value);
                    using var reader = await cmd.ExecuteReaderAsync();
                    if (!await reader.ReadAsync())
                    {
                        return new BadRequestObjectResult(new { success = false, message = "Invalid ProductId" });
                    }
                    mrp = Convert.ToDecimal(reader["mrp"]);
                    salePrice = Convert.ToDecimal(reader["saleprice"]);
                }
                else
                {
                    return new BadRequestObjectResult(new { success = false, message = "ProductId or VariantId is required" });
                }

                decimal totalPrice = salePrice * cartItem.Quantity;

                // Insert Query (Clean)
                using var insertCmd = new NpgsqlCommand(@"
            INSERT INTO cart 
            (
                userid, ipaddress, productid, variantid, quantity, 
                mrp, saleprice, totalprice, createdat, updatedat
            )
            VALUES 
            (
                @userid, @ipaddress, @productid, @variantid, @quantity, 
                @mrp, @saleprice, @totalprice, @createdat, @updatedat
            )", connection);

                insertCmd.Parameters.AddWithValue("@userid", (object?)cartItem.UserId ?? DBNull.Value);
                insertCmd.Parameters.AddWithValue("@ipaddress", (object?)cartItem.IpAddress ?? DBNull.Value);
                insertCmd.Parameters.AddWithValue("@productid", (object?)cartItem.ProductId ?? DBNull.Value);
                insertCmd.Parameters.AddWithValue("@variantid", (object?)cartItem.VariantId ?? DBNull.Value);
                insertCmd.Parameters.AddWithValue("@quantity", cartItem.Quantity);
                insertCmd.Parameters.AddWithValue("@mrp", mrp);
                insertCmd.Parameters.AddWithValue("@saleprice", salePrice);
                insertCmd.Parameters.AddWithValue("@totalprice", totalPrice);
                insertCmd.Parameters.AddWithValue("@createdat", DateTime.UtcNow);
                insertCmd.Parameters.AddWithValue("@updatedat", DateTime.UtcNow);

                await insertCmd.ExecuteNonQueryAsync();

                return new OkObjectResult(new
                {
                    success = true,
                    message = "Item added to cart successfully"
                });
            }
            catch (Exception ex)
            {
                return new BadRequestObjectResult(new
                {
                    success = false,
                    message = ex.Message
                });
            }
        }

    }
}
