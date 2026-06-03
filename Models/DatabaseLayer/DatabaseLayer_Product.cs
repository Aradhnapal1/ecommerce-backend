using CloudinaryDotNet;
using CloudinaryDotNet.Actions;
using Microsoft.AspNetCore.Mvc;
using Npgsql;

namespace Ecommerce_Backend.Models.DatabaseLayer
{
    public partial interface IDatabaseLayer
    {
        Task<List<ProductModel>> GetAllProducts();
        Task<IActionResult> AddProduct([FromForm] ProductModel product);
    }

    public partial class DataBaseLayer : IDatabaseLayer
    {
        public async Task<List<ProductModel>> GetAllProducts()
        {
            var products = new List<ProductModel>();

            using var con = new NpgsqlConnection(DbConnection);
            await con.OpenAsync();

            var query = "SELECT * FROM products";
            using var cmd = new NpgsqlCommand(query, con);

            using var reader = await cmd.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                var product = new ProductModel
                {
                    Id = reader.GetInt32(reader.GetOrdinal("id")),
                    ProductName = reader.GetString(reader.GetOrdinal("productname")),

                    ShortDescription = reader.IsDBNull(reader.GetOrdinal("shortdescription"))
                        ? null
                        : reader.GetString(reader.GetOrdinal("shortdescription")),

                    Description = reader.IsDBNull(reader.GetOrdinal("description"))
                        ? null
                        : reader.GetString(reader.GetOrdinal("description")),

                    SKU = reader.IsDBNull(reader.GetOrdinal("sku"))
                        ? null
                        : reader.GetString(reader.GetOrdinal("sku")),

                    BrandId = reader.IsDBNull(reader.GetOrdinal("brandid"))
                        ? null
                        : reader.GetInt32(reader.GetOrdinal("brandid")),

                    CategoryId = reader.IsDBNull(reader.GetOrdinal("categoryid"))
                        ? null
                        : reader.GetInt32(reader.GetOrdinal("categoryid")),

                    BasePrice = reader.GetDecimal(reader.GetOrdinal("baseprice")),
                    MRP = reader.GetDecimal(reader.GetOrdinal("mrp")),

                    DiscountPrice = reader.IsDBNull(reader.GetOrdinal("discountprice"))
                        ? null
                        : reader.GetDecimal(reader.GetOrdinal("discountprice")),

                    SalePrice = reader.GetDecimal(reader.GetOrdinal("saleprice")),
                    GST = reader.GetDecimal(reader.GetOrdinal("gst")),

                    Stock = reader.GetInt32(reader.GetOrdinal("stock")),

                    ProductImageUrl = reader.IsDBNull(reader.GetOrdinal("productimageurl"))
                        ? null
                        : reader.GetString(reader.GetOrdinal("productimageurl")),

                    // ✅ Correct TEXT[] handling
                    GalleryImages = reader.IsDBNull(reader.GetOrdinal("galleryimages"))
                        ? null
                        : reader.GetFieldValue<string[]>(reader.GetOrdinal("galleryimages")),

                    Sizes = reader.IsDBNull(reader.GetOrdinal("sizes"))
                        ? null
                        : reader.GetFieldValue<string[]>(reader.GetOrdinal("sizes")),

                    Color = reader.IsDBNull(reader.GetOrdinal("color"))
                        ? null
                        : reader.GetString(reader.GetOrdinal("color")),

                    IsActive = reader.GetBoolean(reader.GetOrdinal("isactive")),

                    CreatedAt = reader.GetDateTime(reader.GetOrdinal("createdat")),
                    UpdatedAt = reader.GetDateTime(reader.GetOrdinal("updatedat"))
                };

                products.Add(product);
            }

            return products;
        }



        public async Task<IActionResult> AddProduct([FromForm] ProductModel product)
        {
            try
            {
                using var con = new NpgsqlConnection(DbConnection);
                await con.OpenAsync();

                // =========================
                // CLOUDINARY SETUP
                // =========================
                var account = new Account(
                    _configuration["CloudinarySettings:CloudName"],
                    _configuration["CloudinarySettings:ApiKey"],
                    _configuration["CloudinarySettings:ApiSecret"]
                );

                var cloudinary = new Cloudinary(account);

                // =========================
                // MAIN IMAGE UPLOAD
                // =========================
                string productImageUrl = "";

                if (product.ProductImage != null)
                {
                    using var stream = product.ProductImage.OpenReadStream();

                    var uploadParams = new ImageUploadParams
                    {
                        File = new FileDescription(product.ProductImage.FileName, stream),
                        Folder = "products/main"
                    };

                    var uploadResult = await cloudinary.UploadAsync(uploadParams);

                    if (uploadResult.Error != null)
                    {
                        return new BadRequestObjectResult(new
                        {
                            status = false,
                            message = uploadResult.Error.Message
                        });
                    }

                    productImageUrl = uploadResult.SecureUrl.ToString();
                }

                // =========================
                // GALLERY IMAGES UPLOAD
                // =========================
                List<string> galleryUrls = new();

                if (product.GalleryFiles != null && product.GalleryFiles.Count > 0)
                {
                    foreach (var file in product.GalleryFiles)
                    {
                        using var stream = file.OpenReadStream();

                        var uploadParams = new ImageUploadParams
                        {
                            File = new FileDescription(file.FileName, stream),
                            Folder = "products/gallery"
                        };

                        var uploadResult = await cloudinary.UploadAsync(uploadParams);

                        if (uploadResult.Error == null)
                        {
                            galleryUrls.Add(uploadResult.SecureUrl.ToString());
                        }
                    }
                }

                // =========================
                // DISCOUNT % LOGIC
                // =========================
                decimal discountPercent = product.DiscountPrice ?? 0;

                decimal discountAmount = (product.MRP * discountPercent) / 100;

                decimal basePrice = product.MRP - discountAmount;

                decimal gstAmount = (basePrice * product.GST) / 100;

                decimal salePrice = basePrice + gstAmount;

                product.BasePrice = basePrice;
                product.SalePrice = salePrice;

                // =========================
                // INSERT INTO DB
                // =========================
                var query = @"
INSERT INTO products 
(productname, shortdescription, description, sku, brandid, categoryid,
 baseprice, mrp, discountprice, saleprice, gst, stock,
 productimageurl, galleryimages, sizes, color, isactive, createdat, updatedat)
VALUES 
(@ProductName, @ShortDescription, @Description, @SKU, @BrandId, @CategoryId,
 @BasePrice, @MRP, @DiscountPrice, @SalePrice, @GST, @Stock,
 @ProductImageUrl, @GalleryImages::text[], @Sizes::text[], @Color, @IsActive, NOW(), NOW())";

                using var cmd = new NpgsqlCommand(query, con);

                cmd.Parameters.AddWithValue("ProductName", product.ProductName);
                cmd.Parameters.AddWithValue("ShortDescription", (object?)product.ShortDescription ?? DBNull.Value);
                cmd.Parameters.AddWithValue("Description", (object?)product.Description ?? DBNull.Value);
                cmd.Parameters.AddWithValue("SKU", (object?)product.SKU ?? DBNull.Value);
                cmd.Parameters.AddWithValue("BrandId", (object?)product.BrandId ?? DBNull.Value);
                cmd.Parameters.AddWithValue("CategoryId", (object?)product.CategoryId ?? DBNull.Value);

                cmd.Parameters.AddWithValue("BasePrice", product.BasePrice);
                cmd.Parameters.AddWithValue("MRP", product.MRP);

                cmd.Parameters.AddWithValue("DiscountPrice", discountPercent);

                cmd.Parameters.AddWithValue("SalePrice", product.SalePrice);
                cmd.Parameters.AddWithValue("GST", product.GST);
                cmd.Parameters.AddWithValue("Stock", product.Stock);

                cmd.Parameters.AddWithValue("ProductImageUrl",
                    string.IsNullOrEmpty(productImageUrl) ? DBNull.Value : productImageUrl);

                cmd.Parameters.AddWithValue("GalleryImages",
                    galleryUrls.Count > 0 ? galleryUrls.ToArray() : (object)DBNull.Value);

                cmd.Parameters.AddWithValue("Sizes",
                    product.Sizes ?? (object)DBNull.Value);

                cmd.Parameters.AddWithValue("Color",
                    (object?)product.Color ?? DBNull.Value);

                cmd.Parameters.AddWithValue("IsActive", product.IsActive);

                await cmd.ExecuteNonQueryAsync();

                return new OkObjectResult(new
                {
                    status = true,
                    message = "Product added successfully",
                    productImage = productImageUrl,
                    galleryImages = galleryUrls,
                    salePrice = product.SalePrice
                });
            }
            catch (Exception ex)
            {
                return new BadRequestObjectResult(new
                {
                    status = false,
                    message = ex.Message
                });
            }
        }
    }
}
