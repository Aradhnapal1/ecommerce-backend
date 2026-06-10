using Microsoft.AspNetCore.Mvc;
using Npgsql;

namespace Ecommerce_Backend.Models.DatabaseLayer
{
   public partial interface IDatabaseLayer
    {
        Task<List<CartResponseModel>> GetAllCartItems();
        Task<IActionResult> AddCartItem(CartModel cartItem);

    }
    public partial class DataBaseLayer : IDatabaseLayer
    {

        public async Task<List<CartResponseModel>> GetAllCartItems()
        {
            var result = new List<CartResponseModel>();

            using var connection = new NpgsqlConnection(DbConnection);
            await connection.OpenAsync();

            using var cmd = new NpgsqlCommand(@"
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
            p.id AS pid,
            p.productname,
            p.slug,
            p.description,
            p.productimageurl,
            p.baseprice,
            p.mrp AS pmrp,
            p.saleprice AS psaleprice,
            p.stock AS pstock,

            -- VARIANT
            pv.id AS vid,
            pv.productid AS vproductid,
            pv.variantname,
            pv.sku,
            pv.color,
            pv.mrp AS vmrp,
            pv.baseprice AS vbaseprice,
            pv.saleprice AS vsaleprice,
            pv.stock AS vstock,
            pv.variantimageurl

        FROM cart c
        LEFT JOIN products p ON c.productid = p.id
        LEFT JOIN product_variants pv ON c.variantid = pv.id
        ORDER BY c.id DESC;
    ", connection);

            using var reader = await cmd.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                var model = new CartResponseModel
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
                    Product = reader["pid"] == DBNull.Value ? null : new ProductModel
                    {
                        Id = Convert.ToInt32(reader["pid"]),
                        ProductName = reader["productname"]?.ToString(),
                        Slug = reader["slug"]?.ToString(),
                        Description = reader["description"]?.ToString(),
                        ProductImageUrl = reader["productimageurl"]?.ToString(),
                        BasePrice = reader["baseprice"] == DBNull.Value ? null : Convert.ToDecimal(reader["baseprice"]),
                        MRP = Convert.ToDecimal(reader["pmrp"]),
                        SalePrice = reader["psaleprice"] == DBNull.Value ? null : Convert.ToDecimal(reader["psaleprice"]),
                        Stock = Convert.ToInt32(reader["pstock"])
                    },

                    // ================= VARIANT =================
                    Variant = reader["vid"] == DBNull.Value ? null : new ProductVariantModel
                    {
                        Id = Convert.ToInt32(reader["vid"]),
                        ProductId = Convert.ToInt32(reader["vproductid"]),
                        VariantName = reader["variantname"]?.ToString(),
                        SKU = reader["sku"]?.ToString(),
                        Color = reader["color"]?.ToString(),
                        MRP = reader["vmrp"] == DBNull.Value ? null : Convert.ToDecimal(reader["vmrp"]),
                        BasePrice = reader["vbaseprice"] == DBNull.Value ? null : Convert.ToDecimal(reader["vbaseprice"]),
                        SalePrice = reader["vsaleprice"] == DBNull.Value ? null : Convert.ToDecimal(reader["vsaleprice"]),
                        Stock = Convert.ToInt32(reader["vstock"]),
                        VariantImageUrl = reader["variantimageurl"]?.ToString()
                    }
                };

                result.Add(model);
            }

            return result;
        }
        public async Task<IActionResult> AddCartItem(CartModel cartItem)
        {
            try
            {
                using var connection = new NpgsqlConnection(DbConnection);
                await connection.OpenAsync();

                var ip = cartItem.IpAddress;

                if (!cartItem.UserId.HasValue && string.IsNullOrWhiteSpace(ip))
                {
                    return new BadRequestObjectResult(new { success = false, message = "UserId or IP required" });
                }

                decimal mrp = 0;
                decimal salePrice = 0;

                // ================= VARIANT =================
                if (cartItem.VariantId.HasValue)
                {
                    using var cmd = new NpgsqlCommand(@"
                SELECT mrp, saleprice FROM product_variants WHERE id = @id", connection);

                    cmd.Parameters.AddWithValue("@id", cartItem.VariantId.Value);

                    using var reader = await cmd.ExecuteReaderAsync();
                    if (!await reader.ReadAsync())
                        return new BadRequestObjectResult(new { success = false, message = "Invalid Variant" });

                    mrp = Convert.ToDecimal(reader["mrp"]);
                    salePrice = Convert.ToDecimal(reader["saleprice"]);

                    cartItem.ProductId = null;
                }
                // ================= PRODUCT =================
                else if (cartItem.ProductId.HasValue)
                {
                    using var cmd = new NpgsqlCommand(@"
                SELECT mrp, saleprice FROM products WHERE id = @id", connection);

                    cmd.Parameters.AddWithValue("@id", cartItem.ProductId.Value);

                    using var reader = await cmd.ExecuteReaderAsync();
                    if (!await reader.ReadAsync())
                        return new BadRequestObjectResult(new { success = false, message = "Invalid Product" });

                    mrp = Convert.ToDecimal(reader["mrp"]);
                    salePrice = Convert.ToDecimal(reader["saleprice"]);

                    cartItem.VariantId = null;
                }
                else
                {
                    return new BadRequestObjectResult(new { success = false, message = "Product or Variant required" });
                }

                decimal total = salePrice * cartItem.Quantity;

                using var insert = new NpgsqlCommand(@"
            INSERT INTO cart 
            (userid, ipaddress, productid, variantid, quantity, mrp, saleprice, totalprice, createdat, updatedat)
            VALUES
            (@userid, @ip, @pid, @vid, @qty, @mrp, @sp, @total, NOW(), NOW());
        ", connection);

                insert.Parameters.AddWithValue("@userid", (object?)cartItem.UserId ?? DBNull.Value);
                insert.Parameters.AddWithValue("@ip", (object?)cartItem.IpAddress ?? DBNull.Value);
                insert.Parameters.AddWithValue("@pid", (object?)cartItem.ProductId ?? DBNull.Value);
                insert.Parameters.AddWithValue("@vid", (object?)cartItem.VariantId ?? DBNull.Value);
                insert.Parameters.AddWithValue("@qty", cartItem.Quantity);
                insert.Parameters.AddWithValue("@mrp", mrp);
                insert.Parameters.AddWithValue("@sp", salePrice);
                insert.Parameters.AddWithValue("@total", total);

                await insert.ExecuteNonQueryAsync();

                return new OkObjectResult(new { success = true, message = "Added to cart" });
            }
            catch (Exception ex)
            {
                return new BadRequestObjectResult(new { success = false, message = ex.Message });
            }
        }
    }
}
