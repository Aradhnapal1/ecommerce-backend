using CloudinaryDotNet;
using CloudinaryDotNet.Actions;
using Ecommerce_Backend.Helpers;
using Microsoft.AspNetCore.Mvc;
using Npgsql;
using System.Text.RegularExpressions;

namespace Ecommerce_Backend.Models.DatabaseLayer
{
    public partial interface IDatabaseLayer
    {
        Task<List<ProductVariantModel>> GetAllVariants();
        Task<object> AddVariant([FromForm] ProductVariantModel variant);
        Task<object> UpdateVariant(int id, ProductVariantModel variant);
        Task<object> DeleteVariant(int id);
        Task<List<ProductVariantModel>> GetVariantsByProductId(int productId);
        Task<ProductVariantModel?> GetVariantBySlug(string slug);
        Task<object> GetVariantById(int id);
    }
    public partial class DataBaseLayer : IDatabaseLayer
    {

        public async Task<List<ProductVariantModel>> GetAllVariants()
        {
            var variants = new List<ProductVariantModel>();

            using var con = new NpgsqlConnection(DbConnection);
            await con.OpenAsync();

            var query = @"
SELECT 
    pv.*,

    c.color_name,

    (
        SELECT ARRAY_AGG(s.size_name)
        FROM sizes s
        WHERE s.id = ANY(pv.sizes)
    ) AS size_names

FROM product_variants pv
LEFT JOIN colors c ON c.id = pv.color::int
ORDER BY pv.id DESC;
";

            using var cmd = new NpgsqlCommand(query, con);
            using var reader = await cmd.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                variants.Add(new ProductVariantModel
                {
                    Id = reader.GetInt32(reader.GetOrdinal("id")),
                    ProductId = reader.GetInt32(reader.GetOrdinal("productid")),

                    VariantName = reader["variantname"]?.ToString(),
                    Slug = reader["slug"]?.ToString(),
                    SKU = reader["sku"]?.ToString(),

                    // ================= SIZE IDS =================
                    Sizes = reader.IsDBNull(reader.GetOrdinal("sizes"))
                        ? Array.Empty<int>()
                        : reader.GetFieldValue<int[]>(reader.GetOrdinal("sizes")),

                    // ================= SIZE NAMES =================
                    SizeNames = reader.IsDBNull(reader.GetOrdinal("size_names"))
                        ? new List<string>()
                        : reader.GetFieldValue<string[]>(reader.GetOrdinal("size_names")).ToList(),

                    // ================= COLOR (STRING DIRECT) =================
                    Color = reader["color"]?.ToString(),
                    ColorName = reader.IsDBNull(reader.GetOrdinal("color_name"))
    ? null
    : reader.GetString(reader.GetOrdinal("color_name")),

                    MRP = reader.IsDBNull(reader.GetOrdinal("mrp"))
                        ? 0
                        : reader.GetDecimal(reader.GetOrdinal("mrp")),

                    DiscountPercent = reader.IsDBNull(reader.GetOrdinal("discountpercent"))
                        ? 0
                        : reader.GetDecimal(reader.GetOrdinal("discountpercent")),

                    BasePrice = reader.IsDBNull(reader.GetOrdinal("baseprice"))
                        ? 0
                        : reader.GetDecimal(reader.GetOrdinal("baseprice")),

                    SalePrice = reader.IsDBNull(reader.GetOrdinal("saleprice"))
                        ? 0
                        : reader.GetDecimal(reader.GetOrdinal("saleprice")),

                    GST = reader.IsDBNull(reader.GetOrdinal("gst"))
                        ? 0
                        : reader.GetDecimal(reader.GetOrdinal("gst")),

                    Stock = reader.IsDBNull(reader.GetOrdinal("stock"))
                        ? 0
                        : reader.GetInt32(reader.GetOrdinal("stock")),

                    VariantImageUrl = reader["variantimageurl"]?.ToString(),

                    GalleryImages = reader.IsDBNull(reader.GetOrdinal("galleryimages"))
                        ? Array.Empty<string>()
                        : reader.GetFieldValue<string[]>(reader.GetOrdinal("galleryimages")),

                    IsActive = reader.GetBoolean(reader.GetOrdinal("isactive")),

                    CreatedAt = reader.IsDBNull(reader.GetOrdinal("createdat"))
                        ? DateTime.MinValue
                        : reader.GetDateTime(reader.GetOrdinal("createdat")),

                    UpdatedAt = reader.IsDBNull(reader.GetOrdinal("updatedat"))
                        ? DateTime.MinValue
                        : reader.GetDateTime(reader.GetOrdinal("updatedat"))
                });
            }

            return variants;
        }


        public async Task<object> AddVariant(ProductVariantModel variant)
        {
            try
            {
                using var con = new NpgsqlConnection(DbConnection);
                await con.OpenAsync();

                // ================= CLOUDINARY =================
                var account = new Account(
                    _configuration["CloudinarySettings:CloudName"],
                    _configuration["CloudinarySettings:ApiKey"],
                    _configuration["CloudinarySettings:ApiSecret"]
                );

                var cloudinary = new Cloudinary(account);


                // ================= SLUG GENERATOR =================
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


                // ================= MAIN IMAGE =================
                string variantImageUrl = "";

                if (variant.VariantImage != null && variant.VariantImage.Length > 0)
                {
                    using var stream = variant.VariantImage.OpenReadStream();

                    var uploadParams = new ImageUploadParams
                    {
                        File = new FileDescription(variant.VariantImage.FileName, stream),
                        Folder = "variants/main"
                    };

                    var uploadResult = await cloudinary.UploadAsync(uploadParams);

                    if (uploadResult.Error != null)
                        throw new Exception(uploadResult.Error.Message);

                    variantImageUrl = uploadResult.SecureUrl.ToString();
                }

                // ================= GALLERY =================
                List<string> galleryUrls = new();

                if (variant.GalleryFiles != null && variant.GalleryFiles.Count > 0)
                {
                    foreach (var file in variant.GalleryFiles)
                    {
                        if (file == null || file.Length == 0) continue;

                        using var stream = file.OpenReadStream();

                        var uploadParams = new ImageUploadParams
                        {
                            File = new FileDescription(file.FileName, stream),
                            Folder = "variants/gallery"
                        };

                        var uploadResult = await cloudinary.UploadAsync(uploadParams);

                        if (uploadResult.Error == null && uploadResult.SecureUrl != null)
                        {
                            galleryUrls.Add(uploadResult.SecureUrl.ToString());
                        }
                    }
                }

                // ================= PRICE CALC (FIXED) =================
                decimal mrp = variant.MRP ?? 0;
                decimal discountPercent = variant.DiscountPercent ?? 0;
                decimal gstPercent = variant.GST;

                // Discount on MRP
                decimal discountAmount = (mrp * discountPercent) / 100;
                decimal basePrice = mrp - discountAmount;

                if (basePrice < 0) basePrice = 0;

                // GST on base price
                decimal gstAmount = (basePrice * gstPercent) / 100;
                decimal salePrice = basePrice + gstAmount;

                if (salePrice < 0) salePrice = 0;

                variant.BasePrice = basePrice;
                variant.SalePrice = salePrice;


                // ================= SLUG BUILD =================
                string baseSlug = GenerateSlug(variant.VariantName ?? "variant");

           

              

                string slug = baseSlug;

     

              

                // ================= UNIQUE SLUG CHECK =================
                string originalSlug = slug;
                int counter = 1;

                while (true)
                {
                    using var checkCmd = new NpgsqlCommand(
                        "SELECT COUNT(*) FROM product_variants WHERE slug = @Slug",
                        con);

                    checkCmd.Parameters.AddWithValue("Slug", slug);

                    var count = Convert.ToInt32(await checkCmd.ExecuteScalarAsync());

                    if (count == 0)
                        break;

                    slug = $"{originalSlug}-{counter}";
                    counter++;
                }

                // ================= INSERT QUERY =================
                var query = @"
INSERT INTO product_variants
(
productid,
variantname,
slug,
sku,
sizes,
color,
mrp,
discountpercent,
baseprice,
saleprice,
gst,
stock,
variantimageurl,
galleryimages,
isactive,
createdat,
updatedat
)
VALUES
(
@ProductId,
@VariantName,
@Slug,
@SKU,
@Sizes,
@Color,
@MRP,
@DiscountPercent,
@BasePrice,
@SalePrice,
@GST,
@Stock,
@VariantImageUrl,
@GalleryImages,
@IsActive,
NOW(),
NOW()
)
RETURNING id";

                using var cmd = new NpgsqlCommand(query, con);

                cmd.Parameters.AddWithValue("ProductId", variant.ProductId);
                cmd.Parameters.AddWithValue("VariantName", (object?)variant.VariantName ?? DBNull.Value);
                cmd.Parameters.AddWithValue("Slug", slug);
                cmd.Parameters.AddWithValue("SKU", (object?)variant.SKU ?? DBNull.Value);
                // sizes is optional / nullable
                cmd.Parameters.AddWithValue(
                    "Sizes",
                    variant.Sizes is { Length: > 0 }
                        ? variant.Sizes
                        : (object)DBNull.Value
                );
                cmd.Parameters.AddWithValue("Color", (object?)variant.Color ?? DBNull.Value);

                cmd.Parameters.AddWithValue("MRP", mrp);
                cmd.Parameters.AddWithValue("DiscountPercent", discountPercent);
                cmd.Parameters.AddWithValue("BasePrice", basePrice);
                cmd.Parameters.AddWithValue("SalePrice", salePrice);
                cmd.Parameters.AddWithValue("GST", gstPercent);
                cmd.Parameters.AddWithValue("Stock", variant.Stock);

                cmd.Parameters.AddWithValue("VariantImageUrl",
                    string.IsNullOrEmpty(variantImageUrl) ? DBNull.Value : variantImageUrl);

                cmd.Parameters.AddWithValue("GalleryImages",
                    galleryUrls.Count > 0 ? galleryUrls.ToArray() : (object)DBNull.Value);

                cmd.Parameters.AddWithValue("IsActive", variant.IsActive);

                var id = Convert.ToInt32(await cmd.ExecuteScalarAsync());
                await StockHelper.SyncProductStockFromVariantsAsync(con, null, variant.ProductId);

                return new
                {
                    Id = id,
                    BasePrice = basePrice,
                    SalePrice = salePrice,
                    VariantImage = variantImageUrl,
                    GalleryImages = galleryUrls
                };
            }
            catch (Exception ex)
            {
                throw new Exception(ex.Message);
            }
        }


        public async Task<object> UpdateVariant(int id, ProductVariantModel variant)
        {
            try
            {
                using var con = new NpgsqlConnection(DbConnection);
                await con.OpenAsync();

                // ================= CHECK EXISTS =================
                using (var checkCmd = new NpgsqlCommand(
                    "SELECT COUNT(*) FROM product_variants WHERE id = @id", con))
                {
                    checkCmd.Parameters.AddWithValue("id", id);

                    var exists = Convert.ToInt32(await checkCmd.ExecuteScalarAsync());

                    if (exists == 0)
                    {
                        return new
                        {
                            status = false,
                            message = "Variant not found"
                        };
                    }
                }

                // ================= GET OLD IMAGE =================
                string oldImageUrl = null;

                using (var getCmd = new NpgsqlCommand(
                    "SELECT variantimageurl FROM product_variants WHERE id = @id", con))
                {
                    getCmd.Parameters.AddWithValue("id", id);

                    var result = await getCmd.ExecuteScalarAsync();
                    oldImageUrl = result?.ToString();
                }

                // ================= CLOUDINARY =================
                var account = new Account(
                    _configuration["CloudinarySettings:CloudName"],
                    _configuration["CloudinarySettings:ApiKey"],
                    _configuration["CloudinarySettings:ApiSecret"]
                );

                var cloudinary = new Cloudinary(account);

                string newImageUrl = oldImageUrl;

                // ================= DELETE OLD + UPLOAD NEW IMAGE =================
                if (variant.VariantImage != null && variant.VariantImage.Length > 0)
                {
                    // 🔥 DELETE OLD IMAGE
                    if (!string.IsNullOrEmpty(oldImageUrl))
                    {
                        try
                        {
                            var uri = new Uri(oldImageUrl);

                            var parts = uri.AbsolutePath.Split("/upload/");
                            if (parts.Length > 1)
                            {
                                var publicId = parts[1];

                                publicId = Regex.Replace(publicId, @"^v\d+\/", "");

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

                    // 🔥 UPLOAD NEW IMAGE
                    using var stream = variant.VariantImage.OpenReadStream();

                    var uploadParams = new ImageUploadParams
                    {
                        File = new FileDescription(variant.VariantImage.FileName, stream),
                        Folder = "variants/main"
                    };

                    var uploadResult = await cloudinary.UploadAsync(uploadParams);

                    if (uploadResult.Error != null)
                    {
                        return new
                        {
                            status = false,
                            message = uploadResult.Error.Message
                        };
                    }

                    newImageUrl = uploadResult.SecureUrl.ToString();
                }

                // ================= PRICE CALCULATION =================
                decimal mrp = variant.MRP ?? 0;
                decimal discountPercent = variant.DiscountPercent ?? 0;
                decimal gstPercent = variant.GST;

                decimal discountAmount = (mrp * discountPercent) / 100;
                decimal basePrice = mrp - discountAmount;
                if (basePrice < 0) basePrice = 0;

                decimal gstAmount = (basePrice * gstPercent) / 100;
                decimal salePrice = basePrice + gstAmount;

                // ================= UPDATE QUERY =================
                var query = @"
UPDATE product_variants
SET
    productid = @ProductId,
    variantname = @VariantName,
    sku = @SKU,
    sizes = @Sizes,
    color = @Color,
    mrp = @MRP,
    discountpercent = @DiscountPercent,
    baseprice = @BasePrice,
    saleprice = @SalePrice,
    gst = @GST,
    stock = @Stock,
    variantimageurl = @VariantImageUrl,
    isactive = @IsActive,
    updatedat = NOW()
WHERE id = @Id";

                using var cmd = new NpgsqlCommand(query, con);

                cmd.Parameters.AddWithValue("Id", id);
                cmd.Parameters.AddWithValue("ProductId", variant.ProductId);
                cmd.Parameters.AddWithValue("VariantName", (object?)variant.VariantName ?? DBNull.Value);
                cmd.Parameters.AddWithValue("SKU", (object?)variant.SKU ?? DBNull.Value);

                // sizes is optional / nullable
                cmd.Parameters.AddWithValue(
                    "Sizes",
                    variant.Sizes is { Length: > 0 }
                        ? variant.Sizes
                        : (object)DBNull.Value
                );
                cmd.Parameters.AddWithValue("Color", (object?)variant.Color ?? DBNull.Value);

                cmd.Parameters.AddWithValue("MRP", mrp);
                cmd.Parameters.AddWithValue("DiscountPercent", discountPercent);
                cmd.Parameters.AddWithValue("BasePrice", basePrice);
                cmd.Parameters.AddWithValue("SalePrice", salePrice);
                cmd.Parameters.AddWithValue("GST", gstPercent);
                cmd.Parameters.AddWithValue("Stock", variant.Stock);

                cmd.Parameters.AddWithValue(
                    "VariantImageUrl",
                    string.IsNullOrEmpty(newImageUrl) ? DBNull.Value : newImageUrl
                );

                cmd.Parameters.AddWithValue("IsActive", variant.IsActive);

                var rows = await cmd.ExecuteNonQueryAsync();

                if (rows > 0)
                {
                    await StockHelper.SyncProductStockFromVariantsAsync(con, null, variant.ProductId);
                }

                return new
                {
                    status = rows > 0,
                    message = rows > 0 ? "Variant updated successfully" : "Variant not found",
                    updatedImage = newImageUrl,
                    basePrice,
                    salePrice
                };
            }
            catch (Exception ex)
            {
                return new
                {
                    status = false,
                    message = ex.Message
                };
            }
        }


        public async Task<object> DeleteVariant(int id)
        {
            try
            {
                using var con = new NpgsqlConnection(DbConnection);
                await con.OpenAsync();

                // ================= GET IMAGE =================
                string imageUrl = null;

                var getQuery = "SELECT variantimageurl FROM product_variants WHERE id = @id";

                using (var getCmd = new NpgsqlCommand(getQuery, con))
                {
                    getCmd.Parameters.AddWithValue("id", id);

                    var result = await getCmd.ExecuteScalarAsync();
                    imageUrl = result == DBNull.Value ? null : result?.ToString();
                }

                // ================= DELETE IMAGE (Cloudinary) =================
                if (!string.IsNullOrEmpty(imageUrl))
                {
                    var account = new Account(
                        _configuration["CloudinarySettings:CloudName"],
                        _configuration["CloudinarySettings:ApiKey"],
                        _configuration["CloudinarySettings:ApiSecret"]
                    );

                    var cloudinary = new Cloudinary(account);

                    try
                    {
                        var uri = new Uri(imageUrl);
                        var segments = uri.AbsolutePath.Split('/');
                        var fileName = segments[^1];
                        var publicId = "variants/main/" + Path.GetFileNameWithoutExtension(fileName);

                        await cloudinary.DestroyAsync(new DeletionParams(publicId));
                    }
                    catch { }
                }

                // ================= DELETE FROM DB =================
                var query = "DELETE FROM product_variants WHERE id = @id";

                using var cmd = new NpgsqlCommand(query, con);
                cmd.Parameters.AddWithValue("id", id);

                var rows = await cmd.ExecuteNonQueryAsync();

                if (rows == 0)
                {
                    return new
                    {
                        status = false,
                        message = "Variant not found"
                    };
                }

                return new
                {
                    status = true,
                    message = "Variant deleted successfully "
                };
            }
            catch (Exception ex)
            {
                return new
                {
                    status = false,
                    message = ex.Message
                };
            }
        }





        public async Task<object> GetVariantById(int id)
        {
            using var con = new NpgsqlConnection(DbConnection);
            await con.OpenAsync();

            var query = @"
    SELECT 
        pv.*,
        c.color_name,

        (
            SELECT ARRAY_AGG(s.size_name)
            FROM sizes s
            WHERE s.id = ANY(pv.sizes)
        ) AS size_names

    FROM product_variants pv
    LEFT JOIN colors c ON c.id = pv.color::int
    WHERE pv.id = @id;
    ";

            using var cmd = new NpgsqlCommand(query, con);
            cmd.Parameters.AddWithValue("id", id);

            using var reader = await cmd.ExecuteReaderAsync();

            if (!await reader.ReadAsync())
                return null;

            var variant = new ProductVariantModel
            {
                Id = reader.GetInt32(reader.GetOrdinal("id")),
                ProductId = reader.GetInt32(reader.GetOrdinal("productid")),

                VariantName = reader["variantname"]?.ToString(),
                Slug = reader["slug"]?.ToString(),
                SKU = reader["sku"]?.ToString(),

                Sizes = reader.IsDBNull(reader.GetOrdinal("sizes"))
                    ? Array.Empty<int>()
                    : reader.GetFieldValue<int[]>(reader.GetOrdinal("sizes")),

                Color = reader["color"]?.ToString(),

                // ✅ NEW FIELD
                ColorName = reader.IsDBNull(reader.GetOrdinal("color_name"))
                    ? null
                    : reader.GetString(reader.GetOrdinal("color_name")),

                MRP = reader.GetDecimal(reader.GetOrdinal("mrp")),
                DiscountPercent = reader.GetDecimal(reader.GetOrdinal("discountpercent")),
                BasePrice = reader.GetDecimal(reader.GetOrdinal("baseprice")),
                SalePrice = reader.GetDecimal(reader.GetOrdinal("saleprice")),
                GST = reader.GetDecimal(reader.GetOrdinal("gst")),
                Stock = reader.GetInt32(reader.GetOrdinal("stock")),

                VariantImageUrl = reader.IsDBNull(reader.GetOrdinal("variantimageurl"))
                    ? null
                    : reader.GetString(reader.GetOrdinal("variantimageurl")),

                GalleryImages = reader.IsDBNull(reader.GetOrdinal("galleryimages"))
                    ? Array.Empty<string>()
                    : reader.GetFieldValue<string[]>(reader.GetOrdinal("galleryimages")),

                IsActive = reader.GetBoolean(reader.GetOrdinal("isactive")),

                // optional if model has it
                SizeNames = reader.IsDBNull(reader.GetOrdinal("size_names"))
                    ? new List<string>()
                    : reader.GetFieldValue<string[]>(reader.GetOrdinal("size_names")).ToList()
            };

            return variant;
        }

        public async Task<List<ProductVariantModel>> GetVariantsByProductId(int productId)
        {
            var variants = new List<ProductVariantModel>();

            using var con = new NpgsqlConnection(DbConnection);
            await con.OpenAsync();

            const string query = """
                SELECT
                    pv.*,
                    c.color_name,
                    (
                        SELECT ARRAY_AGG(s.size_name ORDER BY s.id)
                        FROM sizes s
                        WHERE s.id = ANY(pv.sizes)
                    ) AS size_names
                FROM product_variants pv
                LEFT JOIN colors c ON c.id = pv.color::int
                WHERE pv.productid = @productId AND pv.isactive = TRUE
                ORDER BY pv.id ASC
                """;

            using var cmd = new NpgsqlCommand(query, con);
            cmd.Parameters.AddWithValue("productId", productId);
            using var reader = await cmd.ExecuteReaderAsync();

            while (await reader.ReadAsync())
                variants.Add(MapVariantFromReader(reader));

            return variants;
        }

        public async Task<ProductVariantModel?> GetVariantBySlug(string slug)
        {
            if (string.IsNullOrWhiteSpace(slug))
                return null;

            using var con = new NpgsqlConnection(DbConnection);
            await con.OpenAsync();

            const string query = """
                SELECT
                    pv.*,
                    c.color_name,
                    (
                        SELECT ARRAY_AGG(s.size_name ORDER BY s.id)
                        FROM sizes s
                        WHERE s.id = ANY(pv.sizes)
                    ) AS size_names
                FROM product_variants pv
                LEFT JOIN colors c ON c.id = pv.color::int
                WHERE LOWER(pv.slug) = LOWER(@slug) AND pv.isactive = TRUE
                LIMIT 1
                """;

            using var cmd = new NpgsqlCommand(query, con);
            cmd.Parameters.AddWithValue("slug", slug.Trim());
            using var reader = await cmd.ExecuteReaderAsync();

            if (!await reader.ReadAsync())
                return null;

            return MapVariantFromReader(reader);
        }

        private static ProductVariantModel MapVariantFromReader(NpgsqlDataReader reader)
        {
            return new ProductVariantModel
            {
                Id = reader.GetInt32(reader.GetOrdinal("id")),
                ProductId = reader.GetInt32(reader.GetOrdinal("productid")),
                VariantName = reader["variantname"]?.ToString(),
                Slug = reader["slug"]?.ToString(),
                SKU = reader["sku"]?.ToString(),
                Sizes = reader.IsDBNull(reader.GetOrdinal("sizes"))
                    ? Array.Empty<int>()
                    : reader.GetFieldValue<int[]>(reader.GetOrdinal("sizes")),
                Color = reader["color"]?.ToString(),
                ColorName = reader.IsDBNull(reader.GetOrdinal("color_name"))
                    ? null
                    : reader.GetString(reader.GetOrdinal("color_name")),
                MRP = reader.IsDBNull(reader.GetOrdinal("mrp"))
                    ? 0
                    : reader.GetDecimal(reader.GetOrdinal("mrp")),
                DiscountPercent = reader.IsDBNull(reader.GetOrdinal("discountpercent"))
                    ? 0
                    : reader.GetDecimal(reader.GetOrdinal("discountpercent")),
                BasePrice = reader.IsDBNull(reader.GetOrdinal("baseprice"))
                    ? 0
                    : reader.GetDecimal(reader.GetOrdinal("baseprice")),
                SalePrice = reader.IsDBNull(reader.GetOrdinal("saleprice"))
                    ? 0
                    : reader.GetDecimal(reader.GetOrdinal("saleprice")),
                GST = reader.IsDBNull(reader.GetOrdinal("gst"))
                    ? 0
                    : reader.GetDecimal(reader.GetOrdinal("gst")),
                Stock = reader.IsDBNull(reader.GetOrdinal("stock"))
                    ? 0
                    : reader.GetInt32(reader.GetOrdinal("stock")),
                VariantImageUrl = reader["variantimageurl"]?.ToString(),
                GalleryImages = reader.IsDBNull(reader.GetOrdinal("galleryimages"))
                    ? Array.Empty<string>()
                    : reader.GetFieldValue<string[]>(reader.GetOrdinal("galleryimages")),
                IsActive = reader.GetBoolean(reader.GetOrdinal("isactive")),
                CreatedAt = reader.IsDBNull(reader.GetOrdinal("createdat"))
                    ? DateTime.MinValue
                    : reader.GetDateTime(reader.GetOrdinal("createdat")),
                UpdatedAt = reader.IsDBNull(reader.GetOrdinal("updatedat"))
                    ? DateTime.MinValue
                    : reader.GetDateTime(reader.GetOrdinal("updatedat")),
                SizeNames = reader.IsDBNull(reader.GetOrdinal("size_names"))
                    ? new List<string>()
                    : reader.GetFieldValue<string[]>(reader.GetOrdinal("size_names")).ToList()
            };
        }


    }
}