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

                    Slug = reader.IsDBNull(reader.GetOrdinal("slug"))
                        ? null
                        : reader.GetString(reader.GetOrdinal("slug")),

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
                // SLUG GENERATION
                // =========================
                string GenerateSlug(string text)
                {
                    if (string.IsNullOrWhiteSpace(text))
                        return "";

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
                        "SELECT COUNT(*) FROM products WHERE slug = @Slug",
                        con);

                    checkCmd.Parameters.AddWithValue("Slug", slug);

                    var count = Convert.ToInt32(await checkCmd.ExecuteScalarAsync());

                    if (count == 0)
                        break;

                    slug = $"{originalSlug}-{counter}";
                    counter++;
                }

                // =========================
                // MAIN IMAGE UPLOAD
                // =========================
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
                        message = "ProductImage is required (Postman key: ProductImage)"
                    });
                }

                // =========================
                // GALLERY IMAGES UPLOAD
                // =========================
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

                        if (uploadResult?.Error == null && uploadResult?.SecureUrl != null)
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
(productname, slug, shortdescription, description, sku, brandid, categoryid,
 baseprice, mrp, discountprice, saleprice, gst, stock,
 productimageurl, galleryimages, sizes, color, isactive, createdat, updatedat)
VALUES 
(@ProductName, @Slug, @ShortDescription, @Description, @SKU, @BrandId, @CategoryId,
 @BasePrice, @MRP, @DiscountPrice, @SalePrice, @GST, @Stock,
 @ProductImageUrl, @GalleryImages::text[], @Sizes::text[], @Color, @IsActive, NOW(), NOW())";

                using var cmd = new NpgsqlCommand(query, con);

                cmd.Parameters.AddWithValue("ProductName", product.ProductName);
                cmd.Parameters.AddWithValue("Slug", slug);
                cmd.Parameters.AddWithValue("ShortDescription", (object?)product.ShortDescription ?? DBNull.Value);
                cmd.Parameters.AddWithValue("Description", (object?)product.Description ?? DBNull.Value);
                cmd.Parameters.AddWithValue("SKU", (object?)product.SKU ?? DBNull.Value);
                cmd.Parameters.AddWithValue("BrandId", (object?)product.BrandId ?? DBNull.Value);
                cmd.Parameters.AddWithValue("CategoryId", (object?)product.CategoryId ?? DBNull.Value);

                cmd.Parameters.AddWithValue("BasePrice", product.BasePrice ?? 0);
                cmd.Parameters.AddWithValue("MRP", product.MRP);
                cmd.Parameters.AddWithValue("DiscountPrice", discountPercent);
                cmd.Parameters.AddWithValue("SalePrice", product.SalePrice ?? 0);
                cmd.Parameters.AddWithValue("GST", product.GST);
                cmd.Parameters.AddWithValue("Stock", product.Stock);

                cmd.Parameters.AddWithValue("ProductImageUrl",
                    (object?)productImageUrl ?? DBNull.Value);

                cmd.Parameters.AddWithValue("GalleryImages",
                    galleryUrls.Count > 0 ? galleryUrls.ToArray() : (object)DBNull.Value);

                cmd.Parameters.AddWithValue("Sizes",
                    product.Sizes ?? (object)DBNull.Value);

                cmd.Parameters.AddWithValue("Color",
                    (object?)product.Color ?? DBNull.Value);

                cmd.Parameters.AddWithValue("IsActive",
                    product.IsActive);

                await cmd.ExecuteNonQueryAsync();

                return new OkObjectResult(new
                {
                    status = true,
                    message = "Product added successfully",
                    productImage = productImageUrl,
                    galleryImages = galleryUrls,
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



        public async Task<IActionResult> UpdateProduct(int id, [FromForm] ProductModel product)
        {
            try
            {
                using var con = new NpgsqlConnection(DbConnection);
                await con.OpenAsync();

                // =========================
                // CHECK PRODUCT EXISTS
                // =========================
                using (var checkCmd = new NpgsqlCommand(
                    "SELECT COUNT(*) FROM products WHERE id=@id",
                    con))
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
                // MAIN IMAGE UPDATE
                // =========================
                string imageUrl = "";

                if (product.ProductImage != null && product.ProductImage.Length > 0)
                {
                    // Get old image url
                    string oldImageUrl = "";

                    using (var getCmd = new NpgsqlCommand(
                        "SELECT productimageurl FROM products WHERE id=@id",
                        con))
                    {
                        getCmd.Parameters.AddWithValue("@id", id);

                        var result = await getCmd.ExecuteScalarAsync();

                        oldImageUrl = result?.ToString() ?? "";
                    }

                    // Delete old image from Cloudinary
                    if (!string.IsNullOrEmpty(oldImageUrl))
                    {
                        try
                        {
                            var uri = new Uri(oldImageUrl);

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
                            // ignore delete error
                        }
                    }

                    // Upload new image
                    using var stream = product.ProductImage.OpenReadStream();

                    var uploadParams = new ImageUploadParams
                    {
                        File = new FileDescription(
                            product.ProductImage.FileName,
                            stream
                        ),
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

                // =========================
                // GALLERY IMAGE UPDATE
                // =========================
                List<string> galleryUrls = new();

                if (product.GalleryFiles != null && product.GalleryFiles.Count > 0)
                {
                    foreach (var file in product.GalleryFiles)
                    {
                        if (file == null || file.Length == 0)
                            continue;

                        using var stream = file.OpenReadStream();

                        var uploadParams = new ImageUploadParams
                        {
                            File = new FileDescription(
                                file.FileName,
                                stream
                            ),
                            Folder = "products/gallery"
                        };

                        var uploadResult = await cloudinary.UploadAsync(uploadParams);

                        if (uploadResult.Error == null)
                        {
                            galleryUrls.Add(
                                uploadResult.SecureUrl.ToString()
                            );
                        }
                    }
                }

                // =========================
                // DISCOUNT % LOGIC
                // =========================
                decimal discountPercent = product.DiscountPrice ?? 0;

                decimal discountAmount =
                    (product.MRP * discountPercent) / 100;

                decimal basePrice =
                    product.MRP - discountAmount;

                decimal gstAmount =
                    (basePrice * product.GST) / 100;

                decimal salePrice =
                    basePrice + gstAmount;

                product.BasePrice = basePrice;
                product.SalePrice = salePrice;

                // =========================
                // UPDATE PRODUCT
                // =========================
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

productimageurl=
CASE
    WHEN @productimageurl IS NULL
    THEN productimageurl
    ELSE @productimageurl
END,

galleryimages=
CASE
    WHEN @galleryimages IS NULL
    THEN galleryimages
    ELSE @galleryimages
END,

sizes=@sizes,
color=@color,

isactive=@isactive,
updatedat=NOW()

WHERE id=@id
", con);

                cmd.Parameters.AddWithValue("@id", id);

                cmd.Parameters.AddWithValue(
                    "@productname",
                    product.ProductName ?? ""
                );

                cmd.Parameters.AddWithValue(
                    "@shortdescription",
                    (object?)product.ShortDescription ?? DBNull.Value
                );

                cmd.Parameters.AddWithValue(
                    "@description",
                    (object?)product.Description ?? DBNull.Value
                );

                cmd.Parameters.AddWithValue(
                    "@sku",
                    (object?)product.SKU ?? DBNull.Value
                );

                cmd.Parameters.AddWithValue(
                    "@brandid",
                    (object?)product.BrandId ?? DBNull.Value
                );

                cmd.Parameters.AddWithValue(
                    "@categoryid",
                    (object?)product.CategoryId ?? DBNull.Value
                );

                cmd.Parameters.AddWithValue(
                    "@baseprice",
                    product.BasePrice ?? 0
                );

                cmd.Parameters.AddWithValue(
                    "@mrp",
                    product.MRP
                );

                cmd.Parameters.AddWithValue(
                    "@discountprice",
                    discountPercent
                );

                cmd.Parameters.AddWithValue(
                    "@saleprice",
                    product.SalePrice ?? 0
                );

                cmd.Parameters.AddWithValue(
                    "@gst",
                    product.GST
                );

                cmd.Parameters.AddWithValue(
                    "@stock",
                    product.Stock
                );

                cmd.Parameters.AddWithValue(
                    "@productimageurl",
                    string.IsNullOrEmpty(imageUrl)
                        ? DBNull.Value
                        : imageUrl
                );

                cmd.Parameters.AddWithValue(
                    "@galleryimages",
                    galleryUrls.Count > 0
                        ? galleryUrls.ToArray()
                        : (object)DBNull.Value
                );

                cmd.Parameters.AddWithValue(
                    "@sizes",
                    product.Sizes ?? (object)DBNull.Value
                );

                cmd.Parameters.AddWithValue(
                    "@color",
                    (object?)product.Color ?? DBNull.Value
                );

                cmd.Parameters.AddWithValue(
                    "@isactive",
                    product.IsActive
                );

                await cmd.ExecuteNonQueryAsync();

                return new OkObjectResult(new
                {
                    status = true,
                    message = "Product updated successfully",
                    productImage = imageUrl,
                    galleryImages = galleryUrls,
                    basePrice = basePrice,
                    salePrice = salePrice
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

    }
}
