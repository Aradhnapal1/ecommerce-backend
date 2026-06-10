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
            c.id,
            c.userid,
            c.ipaddress,
            c.productid,
            c.variantid,
            c.quantity,
            c.mrp,
            c.saleprice,
            c.totalprice,
            c.createdat,
            c.updatedat,

            -- PRODUCT
            p.productname,
            p.slug,
            p.description,
            p.productimageurl,
            p.baseprice,
            p.stock,

            -- VARIANT
            pv.variantname,
            pv.sku,
            pv.color,
            pv.mrp AS variant_mrp,
            pv.baseprice AS variant_baseprice,
            pv.saleprice AS variant_saleprice,
            pv.stock AS variant_stock,
            pv.variantimageurl

        FROM cart c
        LEFT JOIN products p ON c.productid = p.id
        LEFT JOIN product_variants pv ON c.variantid = pv.id
        ORDER BY c.id DESC;
    ", connection);

            using var reader = await command.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                cartItems.Add(new CartModel
                {
                    // ================= CART =================
                    Id = Convert.ToInt32(reader["id"]),
                    UserId = reader["userid"] == DBNull.Value ? null : Convert.ToInt32(reader["userid"]),
                    IpAddress = reader["ipaddress"]?.ToString(),
                    ProductId = reader["productid"] == DBNull.Value ? null : Convert.ToInt32(reader["productid"]),
                    VariantId = reader["variantid"] == DBNull.Value ? null : Convert.ToInt32(reader["variantid"]),
                    Quantity = Convert.ToInt32(reader["quantity"]),
                    Mrp = Convert.ToDecimal(reader["mrp"]),
                    SalePrice = Convert.ToDecimal(reader["saleprice"]),
                    TotalPrice = Convert.ToDecimal(reader["totalprice"]),
                    CreatedAt = Convert.ToDateTime(reader["createdat"]),
                    UpdatedAt = Convert.ToDateTime(reader["updatedat"]),

                    // ================= PRODUCT =================
                    ProductName = reader["productname"]?.ToString(),
                    ProductSlug = reader["slug"]?.ToString(),
                    ProductDescription = reader["description"]?.ToString(),
                    ProductImageUrl = reader["productimageurl"]?.ToString(),
                    ProductBasePrice = reader["baseprice"] == DBNull.Value ? null : Convert.ToDecimal(reader["baseprice"]),
                    ProductStock = reader["stock"] == DBNull.Value ? null : Convert.ToInt32(reader["stock"]),

                    // ================= VARIANT =================
                    VariantName = reader["variantname"]?.ToString(),
                    VariantSku = reader["sku"]?.ToString(),
                    VariantColor = reader["color"]?.ToString(),
                    VariantImageUrl = reader["variantimageurl"]?.ToString(),

                    VariantMRP = reader["variant_mrp"] == DBNull.Value ? null : Convert.ToDecimal(reader["variant_mrp"]),
                    VariantBasePrice = reader["variant_baseprice"] == DBNull.Value ? null : Convert.ToDecimal(reader["variant_baseprice"]),
                    VariantSalePrice = reader["variant_saleprice"] == DBNull.Value ? null : Convert.ToDecimal(reader["variant_saleprice"]),
                    VariantStock = reader["variant_stock"] == DBNull.Value ? null : Convert.ToInt32(reader["variant_stock"])
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
