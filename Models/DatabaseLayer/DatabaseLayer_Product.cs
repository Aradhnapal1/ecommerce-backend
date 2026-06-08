using CloudinaryDotNet;
using CloudinaryDotNet.Actions;
using Microsoft.AspNetCore.Mvc;
using Npgsql;
using System.Text.RegularExpressions;

namespace Ecommerce_Backend.Models.DatabaseLayer
{
    public partial interface IDatabaseLayer
    {
        Task<List<ProductModel>> GetAllProducts();
        Task<IActionResult> AddProduct([FromForm] ProductModel product);
        Task<IActionResult> UpdateProduct(int id, [FromForm] ProductModel product);
        Task<IActionResult> DeleteProduct(int id);
        Task<ProductModel?> GetProductById(int id);
    }

    public partial class DataBaseLayer : IDatabaseLayer
    {
        public async Task<List<ProductModel>> GetAllProducts()
        {
            var products = new List<ProductModel>();

            using var con = new NpgsqlConnection(DbConnection);
            await con.OpenAsync();

            var query = @"
SELECT
    p.*,
    b.brand_name,
    c.category_name,
    col.color_name,

    (
        SELECT ARRAY_AGG(s.size_name)
        FROM sizes s
        WHERE s.id = ANY(p.sizes)
    ) AS size_names

FROM products p

LEFT JOIN brands b ON b.id = p.brandid
LEFT JOIN categories c ON c.id = p.categoryid
LEFT JOIN colors col ON col.id = p.color::INT

ORDER BY p.id DESC;
";

            using var cmd = new NpgsqlCommand(query, con);
            using var reader = await cmd.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                var product = new ProductModel
                {
                    Id = reader.GetInt32(reader.GetOrdinal("id")),
                    ProductName = reader["productname"]?.ToString(),
                    Slug = reader["slug"]?.ToString(),
                    Type = reader["type"]?.ToString(),
                    ShortDescription = reader["shortdescription"]?.ToString(),
                    Description = reader["description"]?.ToString(),
                    SKU = reader["sku"]?.ToString(),

                    BrandId = reader.IsDBNull(reader.GetOrdinal("brandid"))
                        ? null
                        : reader.GetInt32(reader.GetOrdinal("brandid")),

                    CategoryId = reader.IsDBNull(reader.GetOrdinal("categoryid"))
                        ? null
                        : reader.GetInt32(reader.GetOrdinal("categoryid")),

                    BasePrice = reader.IsDBNull(reader.GetOrdinal("baseprice"))
                        ? 0
                        : reader.GetDecimal(reader.GetOrdinal("baseprice")),

                    MRP = reader.IsDBNull(reader.GetOrdinal("mrp"))
                        ? 0
                        : reader.GetDecimal(reader.GetOrdinal("mrp")),

                    DiscountPrice = reader.IsDBNull(reader.GetOrdinal("discountprice"))
                        ? null
                        : reader.GetDecimal(reader.GetOrdinal("discountprice")),

                    SalePrice = reader.IsDBNull(reader.GetOrdinal("saleprice"))
                        ? 0
                        : reader.GetDecimal(reader.GetOrdinal("saleprice")),

                    GST = reader.IsDBNull(reader.GetOrdinal("gst"))
                        ? 0
                        : reader.GetDecimal(reader.GetOrdinal("gst")),

                    Stock = reader.IsDBNull(reader.GetOrdinal("stock"))
                        ? 0
                        : reader.GetInt32(reader.GetOrdinal("stock")),

                    ProductImageUrl = reader["productimageurl"]?.ToString(),

                    GalleryImages = reader.IsDBNull(reader.GetOrdinal("galleryimages"))
                        ? Array.Empty<string>()
                        : reader.GetFieldValue<string[]>(reader.GetOrdinal("galleryimages")),

                    // ✅ FIXED: INT[] SAFE READ
                    Sizes = reader.IsDBNull(reader.GetOrdinal("sizes"))
                        ? Array.Empty<int>()
                        : reader.GetFieldValue<int[]>(reader.GetOrdinal("sizes")),

                    Color = reader["color"]?.ToString(),

                    IsActive = reader.IsDBNull(reader.GetOrdinal("isactive"))
                        ? false
                        : reader.GetBoolean(reader.GetOrdinal("isactive")),

                    CreatedAt = reader.IsDBNull(reader.GetOrdinal("createdat"))
                        ? DateTime.MinValue
                        : reader.GetDateTime(reader.GetOrdinal("createdat")),

                    UpdatedAt = reader.IsDBNull(reader.GetOrdinal("updatedat"))
                        ? DateTime.MinValue
                        : reader.GetDateTime(reader.GetOrdinal("updatedat")),

                    BrandName = reader["brand_name"]?.ToString(),
                    CategoryName = reader["category_name"]?.ToString(),
                    ColorName = reader["color_name"]?.ToString(),

                    SizeNames = reader.IsDBNull(reader.GetOrdinal("size_names"))
                        ? new List<string>()
                        : reader.GetFieldValue<string[]>(reader.GetOrdinal("size_names")).ToList()
                };

                products.Add(product);
            }

            return products;
        }


        public async Task<IActionResult> AddProduct([FromForm] ProductModel product)
        {
            try
            {
                if (product == null)
                {
                    return new BadRequestObjectResult(new
                    {
                        status = false,
                        message = "Product is null"
                    });
                }

                using var con = new NpgsqlConnection(DbConnection);
                await con.OpenAsync();

                // ================= CLOUDINARY =================
                var account = new Account(
                    _configuration["CloudinarySettings:CloudName"],
                    _configuration["CloudinarySettings:ApiKey"],
                    _configuration["CloudinarySettings:ApiSecret"]
                );

                var cloudinary = new Cloudinary(account);

                // ================= SLUG =================
                string GenerateSlug(string text)
                {
                    if (string.IsNullOrWhiteSpace(text)) return "";

                    text = text.ToLower().Trim();
                    text = Regex.Replace(text, @"[^a-z0-9\s-]", "");
                    text = Regex.Replace(text, @"\s+", "-");
                    text = Regex.Replace(text, @"-+", "-");

                    return text;
                }

                string slug = GenerateSlug(product.ProductName);

                string originalSlug = slug;
                int counter = 1;

                while (true)
                {
                    using var checkCmd = new NpgsqlCommand(
                        "SELECT COUNT(*) FROM products WHERE slug = @Slug", con);

                    checkCmd.Parameters.AddWithValue("Slug", slug);

                    var count = Convert.ToInt32(await checkCmd.ExecuteScalarAsync());

                    if (count == 0) break;

                    slug = $"{originalSlug}-{counter}";
                    counter++;
                }

                // ================= MAIN IMAGE =================
                string productImageUrl = null;

                if (product.ProductImage != null && product.ProductImage.Length > 0)
                {
                    using var stream = product.ProductImage.OpenReadStream();

                    var uploadParams = new ImageUploadParams
                    {
                        File = new FileDescription(product.ProductImage.FileName, stream),
                        Folder = "products/main"
                    };

                    var uploadResult = await cloudinary.UploadAsync(uploadParams);

                    if (uploadResult?.Error != null)
                    {
                        return new BadRequestObjectResult(new
                        {
                            status = false,
                            message = uploadResult.Error.Message
                        });
                    }

                    productImageUrl = uploadResult?.SecureUrl?.ToString();
                }
                else
                {
                    return new BadRequestObjectResult(new
                    {
                        status = false,
                        message = "Product image is required"
                    });
                }

                // ================= GALLERY =================
                List<string> galleryUrls = new();

                if (product.GalleryFiles != null && product.GalleryFiles.Count > 0)
                {
                    foreach (var file in product.GalleryFiles)
                    {
                        if (file == null || file.Length == 0) continue;

                        using var stream = file.OpenReadStream();

                        var uploadParams = new ImageUploadParams
                        {
                            File = new FileDescription(file.FileName, stream),
                            Folder = "products/gallery"
                        };

                        var uploadResult = await cloudinary.UploadAsync(uploadParams);

                        if (uploadResult?.SecureUrl != null)
                        {
                            galleryUrls.Add(uploadResult.SecureUrl.ToString());
                        }
                    }
                }

                // ================= PRICE CALC =================
                decimal discountPercent = product.DiscountPrice ?? 0;
                decimal discountAmount = (product.MRP * discountPercent) / 100;

                decimal basePrice = product.MRP - discountAmount;
                decimal gstAmount = (basePrice * product.GST) / 100;

                decimal salePrice = basePrice + gstAmount;

                product.BasePrice = basePrice;
                product.SalePrice = salePrice;

                // ================= INSERT =================
                var query = @"
INSERT INTO products
(
productname, slug, type, shortdescription, description, sku,
brandid, categoryid,
baseprice, mrp, discountprice, saleprice, gst, stock,
productimageurl, galleryimages, sizes, color, isactive,
createdat, updatedat
)
VALUES
(
@ProductName, @Slug, @Type, @ShortDescription, @Description, @SKU,
@BrandId, @CategoryId,
@BasePrice, @MRP, @DiscountPrice, @SalePrice, @GST, @Stock,
@ProductImageUrl, @GalleryImages, @Sizes, @Color, @IsActive,
NOW(), NOW()
)";

                using var cmd = new NpgsqlCommand(query, con);

                cmd.Parameters.AddWithValue("ProductName", product.ProductName);
                cmd.Parameters.AddWithValue("Slug", slug);
                cmd.Parameters.AddWithValue("Type", (object?)product.Type ?? DBNull.Value);
                cmd.Parameters.AddWithValue("ShortDescription", (object?)product.ShortDescription ?? DBNull.Value);
                cmd.Parameters.AddWithValue("Description", (object?)product.Description ?? DBNull.Value);
                cmd.Parameters.AddWithValue("SKU", (object?)product.SKU ?? DBNull.Value);
                cmd.Parameters.AddWithValue("BrandId", (object?)product.BrandId ?? DBNull.Value);
                cmd.Parameters.AddWithValue("CategoryId", (object?)product.CategoryId ?? DBNull.Value);

                cmd.Parameters.AddWithValue("BasePrice", product.BasePrice ?? 0);
                cmd.Parameters.AddWithValue("MRP", product.MRP);
                cmd.Parameters.AddWithValue("DiscountPrice", discountPercent);
                cmd.Parameters.AddWithValue("SalePrice", salePrice);
                cmd.Parameters.AddWithValue("GST", product.GST);
                cmd.Parameters.AddWithValue("Stock", product.Stock);

                cmd.Parameters.AddWithValue("ProductImageUrl",
                    (object?)productImageUrl ?? DBNull.Value);

                cmd.Parameters.AddWithValue("GalleryImages",
                    galleryUrls.Count > 0 ? galleryUrls.ToArray() : Array.Empty<string>());

                // ================= FIXED SIZES =================
                cmd.Parameters.AddWithValue(
                    "Sizes",
                    (object?)product.Sizes ?? Array.Empty<int>()
                );

                cmd.Parameters.AddWithValue("Color", (object?)product.Color ?? DBNull.Value);
                cmd.Parameters.AddWithValue("IsActive", product.IsActive);

                await cmd.ExecuteNonQueryAsync();

                return new OkObjectResult(new
                {
                    status = true,
                    message = "Product added successfully",
                    data = new
                    {
                        productImage = productImageUrl,
                        galleryImages = galleryUrls,
                        basePrice,
                        salePrice
                    }
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


        public async Task<IActionResult> UpdateProduct(int id, [FromForm] ProductModel product)
        {
            try
            {
                using var con = new NpgsqlConnection(DbConnection);
                await con.OpenAsync();

                // ================= CHECK PRODUCT =================
                using (var checkCmd = new NpgsqlCommand(
                    "SELECT COUNT(*) FROM products WHERE id=@id", con))
                {
                    checkCmd.Parameters.AddWithValue("@id", id);

                    var count = Convert.ToInt32(await checkCmd.ExecuteScalarAsync());

                    if (count == 0)
                    {
                        return new NotFoundObjectResult(new
                        {
                            status = false,
                            message = "Product not found"
                        });
                    }
                }

                // ================= CLOUDINARY =================
                var account = new Account(
                    _configuration["CloudinarySettings:CloudName"],
                    _configuration["CloudinarySettings:ApiKey"],
                    _configuration["CloudinarySettings:ApiSecret"]
                );

                var cloudinary = new Cloudinary(account);

                // ================= GET OLD IMAGE =================
                string imageUrl = null;

                if (product.ProductImage != null && product.ProductImage.Length > 0)
                {
                    using var getCmd = new NpgsqlCommand(
                        "SELECT productimageurl FROM products WHERE id=@id", con);

                    getCmd.Parameters.AddWithValue("@id", id);

                    var oldImageUrl = (await getCmd.ExecuteScalarAsync())?.ToString();

                    // ================= DELETE OLD IMAGE (FIXED) =================
                    if (!string.IsNullOrEmpty(oldImageUrl))
                    {
                        try
                        {
                            var uri = new Uri(oldImageUrl);

                            // extract public id from cloudinary url
                            var parts = uri.AbsolutePath.Split("/upload/");
                            if (parts.Length > 1)
                            {
                                var publicId = parts[1];

                                publicId = Regex.Replace(publicId, @"^v\d+/", "");

                                publicId = Path.ChangeExtension(publicId, null)
                                    .Replace("\\", "/");

                                await cloudinary.DestroyAsync(new DeletionParams(publicId));
                            }
                        }
                        catch
                        {
                            // ignore delete error
                        }
                    }

                    // ================= UPLOAD NEW IMAGE =================
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

                    imageUrl = uploadResult.SecureUrl.ToString();
                }

                // ================= GALLERY =================
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
                            galleryUrls.Add(uploadResult.SecureUrl.ToString());
                    }
                }

                // ================= PRICE CALC =================
                decimal discountPercent = product.DiscountPrice ?? 0;
                decimal discountAmount = (product.MRP * discountPercent) / 100;

                decimal basePrice = product.MRP - discountAmount;
                decimal gstAmount = (basePrice * product.GST) / 100;

                decimal salePrice = basePrice + gstAmount;

                // ================= UPDATE QUERY =================
                using var cmd = new NpgsqlCommand(@"
UPDATE products SET
productname=@productname,
shortdescription=@shortdescription,
description=@description,
sku=@sku,
brandid=@brandid,
categoryid=@categoryid,
baseprice=@baseprice,
mrp=@mrp,
discountprice=@discountprice,
saleprice=@saleprice,
gst=@gst,
stock=@stock,

productimageurl = COALESCE(@productimageurl, productimageurl),

galleryimages = COALESCE(@galleryimages, galleryimages),

sizes=@sizes,
color=@color,
isactive=@isactive,
updatedat=NOW()

WHERE id=@id", con);

                cmd.Parameters.AddWithValue("@id", id);
                cmd.Parameters.AddWithValue("@productname", product.ProductName ?? "");
                cmd.Parameters.AddWithValue("@shortdescription", (object?)product.ShortDescription ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@description", (object?)product.Description ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@sku", (object?)product.SKU ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@brandid", (object?)product.BrandId ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@categoryid", (object?)product.CategoryId ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@baseprice", basePrice);
                cmd.Parameters.AddWithValue("@mrp", product.MRP);
                cmd.Parameters.AddWithValue("@discountprice", discountPercent);
                cmd.Parameters.AddWithValue("@saleprice", salePrice);
                cmd.Parameters.AddWithValue("@gst", product.GST);
                cmd.Parameters.AddWithValue("@stock", product.Stock);

                cmd.Parameters.AddWithValue("@productimageurl",
                    (object?)imageUrl ?? DBNull.Value);

                cmd.Parameters.AddWithValue("@galleryimages",
                    galleryUrls.Count > 0 ? galleryUrls.ToArray() : (object)DBNull.Value);

                cmd.Parameters.AddWithValue("@sizes",
                    product.Sizes ?? (object)DBNull.Value);

                cmd.Parameters.AddWithValue("@color",
                    (object?)product.Color ?? DBNull.Value);

                cmd.Parameters.AddWithValue("@isactive",
                    product.IsActive);

                await cmd.ExecuteNonQueryAsync();

                return new OkObjectResult(new
                {
                    status = true,
                    message = "Product updated successfully",
                    image = imageUrl,
                    gallery = galleryUrls,
                    basePrice,
                    salePrice
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


        public async Task<IActionResult> DeleteProduct(int id)
        {
            try
            {
                using var con = new NpgsqlConnection(DbConnection);
                await con.OpenAsync();

                // =========================
                // GET PRODUCT IMAGES
                // =========================
                string productImageUrl = "";
                string[] galleryImages = null;

                using (var cmd = new NpgsqlCommand(
                    @"SELECT productimageurl, galleryimages
              FROM products
              WHERE id = @id", con))
                {
                    cmd.Parameters.AddWithValue("@id", id);

                    using var reader = await cmd.ExecuteReaderAsync();

                    if (!await reader.ReadAsync())
                    {
                        return new NotFoundObjectResult(new
                        {
                            status = false,
                            message = "Product not found"
                        });
                    }

                    productImageUrl = reader["productimageurl"]?.ToString() ?? "";

                    if (!reader.IsDBNull(reader.GetOrdinal("galleryimages")))
                    {
                        galleryImages = reader.GetFieldValue<string[]>(
                            reader.GetOrdinal("galleryimages")
                        );
                    }
                }

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
                // DELETE MAIN IMAGE
                // =========================
                if (!string.IsNullOrEmpty(productImageUrl))
                {
                    try
                    {
                        var uri = new Uri(productImageUrl);

                        var parts = uri.AbsolutePath.Split("/upload/");

                        if (parts.Length > 1)
                        {
                            var publicId = parts[1];

                            publicId = System.Text.RegularExpressions.Regex
                                .Replace(publicId, @"^v\d+\/", "");

                            publicId = Path.Combine(
                                Path.GetDirectoryName(publicId) ?? "",
                                Path.GetFileNameWithoutExtension(publicId)
                            ).Replace("\\", "/");

                            await cloudinary.DestroyAsync(
                                new DeletionParams(publicId)
                            );
                        }
                    }
                    catch
                    {
                        // Ignore image delete errors
                    }
                }

                // =========================
                // DELETE GALLERY IMAGES
                // =========================
                if (galleryImages != null && galleryImages.Length > 0)
                {
                    foreach (var imageUrl in galleryImages)
                    {
                        try
                        {
                            var uri = new Uri(imageUrl);

                            var parts = uri.AbsolutePath.Split("/upload/");

                            if (parts.Length > 1)
                            {
                                var publicId = parts[1];

                                publicId = System.Text.RegularExpressions.Regex
                                    .Replace(publicId, @"^v\d+\/", "");

                                publicId = Path.Combine(
                                    Path.GetDirectoryName(publicId) ?? "",
                                    Path.GetFileNameWithoutExtension(publicId)
                                ).Replace("\\", "/");

                                await cloudinary.DestroyAsync(
                                    new DeletionParams(publicId)
                                );
                            }
                        }
                        catch
                        {
                            // Ignore gallery image delete errors
                        }
                    }
                }

                // =========================
                // DELETE PRODUCT
                // =========================
                using var deleteCmd = new NpgsqlCommand(
                    "DELETE FROM products WHERE id = @id",
                    con);

                deleteCmd.Parameters.AddWithValue("@id", id);

                await deleteCmd.ExecuteNonQueryAsync();

                return new OkObjectResult(new
                {
                    status = true,
                    message = "Product deleted successfully"
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




        public async Task<ProductModel?> GetProductById(int id)
        {
            using var con = new NpgsqlConnection(DbConnection);
            await con.OpenAsync();

            var query = @"
SELECT 
    p.*,
    b.brand_name,
    c.category_name,
    col.color_name,

    (
        SELECT ARRAY_AGG(s.size_name)
        FROM sizes s
        WHERE s.id = ANY(COALESCE(p.sizes, ARRAY[]::int[]))
    ) AS size_names

FROM products p

LEFT JOIN brands b ON b.id = p.brandid
LEFT JOIN categories c ON c.id = p.categoryid
LEFT JOIN colors col ON col.id = p.color::INT

WHERE p.id = @id;
";

            using var cmd = new NpgsqlCommand(query, con);
            cmd.Parameters.AddWithValue("id", id);

            using var reader = await cmd.ExecuteReaderAsync();

            if (!await reader.ReadAsync())
                return null;

            var product = new ProductModel
            {
                Id = reader.GetInt32(reader.GetOrdinal("id")),
                ProductName = reader["productname"]?.ToString(),
                Slug = reader["slug"]?.ToString(),
                Type = reader["type"]?.ToString(),
                ShortDescription = reader["shortdescription"]?.ToString(),
                Description = reader["description"]?.ToString(),
                SKU = reader["sku"]?.ToString(),

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

                ProductImageUrl = reader["productimageurl"]?.ToString(),

                Color = reader["color"]?.ToString(),

                IsActive = reader.GetBoolean(reader.GetOrdinal("isactive")),
                CreatedAt = reader.GetDateTime(reader.GetOrdinal("createdat")),
                UpdatedAt = reader.GetDateTime(reader.GetOrdinal("updatedat")),

                BrandName = reader.IsDBNull(reader.GetOrdinal("brand_name"))
                    ? null
                    : reader.GetString(reader.GetOrdinal("brand_name")),

                CategoryName = reader.IsDBNull(reader.GetOrdinal("category_name"))
                    ? null
                    : reader.GetString(reader.GetOrdinal("category_name")),

                ColorName = reader.IsDBNull(reader.GetOrdinal("color_name"))
                    ? null
                    : reader.GetString(reader.GetOrdinal("color_name")),

                SizeNames = reader.IsDBNull(reader.GetOrdinal("size_names"))
                    ? new List<string>()
                    : reader.GetFieldValue<string[]>(reader.GetOrdinal("size_names")).ToList(),

                Sizes = reader.IsDBNull(reader.GetOrdinal("sizes"))
                    ? Array.Empty<int>()
                    : reader.GetFieldValue<int[]>(reader.GetOrdinal("sizes"))
            };

            return product;
        }
    }
}
