using CloudinaryDotNet;
using CloudinaryDotNet.Actions;
using Ecommerce_Backend.Helpers;
using Microsoft.AspNetCore.Mvc;
using Npgsql;

namespace Ecommerce_Backend.Models.DatabaseLayer
{
    public partial interface IDatabaseLayer
    {
        Task<(byte[] Content, string FileName, string ContentType)> GetProductImportSample();
        Task<(byte[] Content, string FileName, string ContentType)> ExportProductsCsv();
        Task<IActionResult> ImportProductsCsv(IFormFile file);
    }

    public partial class DataBaseLayer : IDatabaseLayer
    {
        public Task<(byte[] Content, string FileName, string ContentType)> GetProductImportSample()
        {
            var bytes = ProductCsvHelper.BuildSampleFile();
            return Task.FromResult((
                bytes,
                "product-import-sample.csv",
                "text/csv"));
        }

        public async Task<(byte[] Content, string FileName, string ContentType)> ExportProductsCsv()
        {
            using var con = new NpgsqlConnection(DbConnection);
            await con.OpenAsync();

            const string query = """
                SELECT
                    p.productname,
                    p.type,
                    p.shortdescription,
                    p.description,
                    p.sku,
                    b.brand_name,
                    c.category_name,
                    col.color_name,
                    col.color_code,
                    (
                        SELECT STRING_AGG(s.size_name, '|' ORDER BY s.id)
                        FROM sizes s
                        WHERE s.id = ANY(COALESCE(p.sizes, ARRAY[]::int[]))
                    ) AS size_names,
                    p.mrp,
                    p.discountprice,
                    p.gst,
                    p.stock,
                    p.productimageurl,
                    array_to_string(p.galleryimages, '|') AS gallery_urls,
                    p.isactive
                FROM products p
                LEFT JOIN brands b ON b.id = p.brandid
                LEFT JOIN categories c ON c.id = p.categoryid
                LEFT JOIN colors col ON col.id = NULLIF(p.color, '')::INT
                ORDER BY p.id DESC
                """;

            var rows = new List<ProductImportRow>();

            await using var cmd = new NpgsqlCommand(query, con);
            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                rows.Add(new ProductImportRow
                {
                    ProductName = reader["productname"]?.ToString() ?? "",
                    Type = reader["type"]?.ToString(),
                    ShortDescription = reader["shortdescription"]?.ToString(),
                    Description = reader["description"]?.ToString(),
                    SKU = reader["sku"]?.ToString(),
                    Brand = reader["brand_name"]?.ToString(),
                    Category = reader["category_name"]?.ToString(),
                    Color = reader["color_name"]?.ToString(),
                    ColorCode = reader["color_code"]?.ToString(),
                    Sizes = reader["size_names"]?.ToString(),
                    MRP = reader["mrp"] == DBNull.Value ? 0 : Convert.ToDecimal(reader["mrp"]),
                    DiscountPercent = reader["discountprice"] == DBNull.Value
                        ? 0
                        : Convert.ToDecimal(reader["discountprice"]),
                    GST = reader["gst"] == DBNull.Value ? 0 : Convert.ToDecimal(reader["gst"]),
                    Stock = reader["stock"] == DBNull.Value ? 0 : Convert.ToInt32(reader["stock"]),
                    ProductImageUrl = reader["productimageurl"]?.ToString(),
                    GalleryImageUrls = reader["gallery_urls"]?.ToString(),
                    IsActive = reader["isactive"] != DBNull.Value && Convert.ToBoolean(reader["isactive"])
                });
            }

            var bytes = ProductCsvHelper.BuildExportFile(rows);
            var fileName = $"products-export-{DateTime.UtcNow:yyyyMMdd-HHmmss}.csv";
            return (bytes, fileName, "text/csv");
        }

        public async Task<IActionResult> ImportProductsCsv(IFormFile file)
        {
            if (file == null || file.Length == 0)
            {
                return new BadRequestObjectResult(new
                {
                    success = false,
                    message = "CSV file is required"
                });
            }

            var extension = Path.GetExtension(file.FileName)?.ToLowerInvariant();
            if (extension is not (".csv" or ".txt"))
            {
                return new BadRequestObjectResult(new
                {
                    success = false,
                    message = "Only .csv files are supported"
                });
            }

            List<ProductImportRow> rows;
            await using (var stream = file.OpenReadStream())
            {
                rows = ProductCsvHelper.Parse(stream);
            }

            if (rows.Count == 0)
            {
                return new BadRequestObjectResult(new
                {
                    success = false,
                    message = "No product rows found in CSV"
                });
            }

            var cloudinary = CreateCloudinary();
            using var httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(60) };
            httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("EcommerceBackend/1.0");

            var successRows = new List<object>();
            var failedRows = new List<object>();
            var createdBrands = 0;
            var createdCategories = 0;
            var createdColors = 0;
            var createdSizes = 0;
            var createdProducts = 0;

            using var con = new NpgsqlConnection(DbConnection);
            await con.OpenAsync();

            foreach (var row in rows)
            {
                try
                {
                    if (string.IsNullOrWhiteSpace(row.ProductName))
                        throw new Exception("ProductName is required");

                    if (row.MRP <= 0)
                        throw new Exception("MRP must be greater than 0");

                    if (!string.IsNullOrWhiteSpace(row.SKU) &&
                        await SkuExistsAsync(con, row.SKU!))
                    {
                        throw new Exception($"SKU already exists: {row.SKU}");
                    }

                    int? brandId = null;
                    if (!string.IsNullOrWhiteSpace(row.Brand))
                    {
                        var (id, createdNow) = await GetOrCreateBrandAsync(con, row.Brand!);
                        brandId = id;
                        if (createdNow) createdBrands++;
                    }

                    int? categoryId = null;
                    if (!string.IsNullOrWhiteSpace(row.Category))
                    {
                        var (id, createdNow) = await GetOrCreateCategoryAsync(con, row.Category!);
                        categoryId = id;
                        if (createdNow) createdCategories++;
                    }

                    string? colorIdText = null;
                    if (!string.IsNullOrWhiteSpace(row.Color))
                    {
                        var (id, createdNow) = await GetOrCreateColorAsync(
                            con, row.Color!, row.ColorCode);
                        colorIdText = id.ToString();
                        if (createdNow) createdColors++;
                    }

                    var sizeIds = new List<int>();
                    foreach (var sizeName in ProductCsvHelper.SplitList(row.Sizes))
                    {
                        var (id, createdNow) = await GetOrCreateSizeAsync(con, sizeName);
                        sizeIds.Add(id);
                        if (createdNow) createdSizes++;
                    }

                    string? productImageUrl = null;
                    if (!string.IsNullOrWhiteSpace(row.ProductImageUrl))
                    {
                        productImageUrl = await UploadImageFromLinkAsync(
                            cloudinary, httpClient, row.ProductImageUrl!, "products/main");
                    }

                    var galleryUrls = new List<string>();
                    foreach (var galleryUrl in ProductCsvHelper.SplitList(row.GalleryImageUrls))
                    {
                        var uploaded = await UploadImageFromLinkAsync(
                            cloudinary, httpClient, galleryUrl, "products/gallery");
                        if (!string.IsNullOrWhiteSpace(uploaded))
                            galleryUrls.Add(uploaded!);
                    }

                    var discountPercent = row.DiscountPercent;
                    var discountAmount = (row.MRP * discountPercent) / 100m;
                    var basePrice = row.MRP - discountAmount;
                    if (basePrice < 0) basePrice = 0;
                    var gstAmount = (basePrice * row.GST) / 100m;
                    var salePrice = basePrice + gstAmount;

                    var slug = await SlugHelper.GenerateUniqueSlugAsync(
                        con, "products", "slug", row.ProductName);

                    const string insert = """
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
                        )
                        RETURNING id
                        """;

                    await using var insertCmd = new NpgsqlCommand(insert, con);
                    insertCmd.Parameters.AddWithValue("@ProductName", row.ProductName.Trim());
                    insertCmd.Parameters.AddWithValue("@Slug", slug);
                    insertCmd.Parameters.AddWithValue("@Type", (object?)row.Type ?? DBNull.Value);
                    insertCmd.Parameters.AddWithValue("@ShortDescription", (object?)row.ShortDescription ?? DBNull.Value);
                    insertCmd.Parameters.AddWithValue("@Description", (object?)row.Description ?? DBNull.Value);
                    insertCmd.Parameters.AddWithValue("@SKU", (object?)row.SKU ?? DBNull.Value);
                    insertCmd.Parameters.AddWithValue("@BrandId", (object?)brandId ?? DBNull.Value);
                    insertCmd.Parameters.AddWithValue("@CategoryId", (object?)categoryId ?? DBNull.Value);
                    insertCmd.Parameters.AddWithValue("@BasePrice", basePrice);
                    insertCmd.Parameters.AddWithValue("@MRP", row.MRP);
                    insertCmd.Parameters.AddWithValue("@DiscountPrice", discountPercent);
                    insertCmd.Parameters.AddWithValue("@SalePrice", salePrice);
                    insertCmd.Parameters.AddWithValue("@GST", row.GST);
                    insertCmd.Parameters.AddWithValue("@Stock", row.Stock);
                    insertCmd.Parameters.AddWithValue("@ProductImageUrl", (object?)productImageUrl ?? DBNull.Value);
                    insertCmd.Parameters.AddWithValue(
                        "@GalleryImages",
                        galleryUrls.Count > 0 ? galleryUrls.ToArray() : (object)DBNull.Value);
                    insertCmd.Parameters.AddWithValue(
                        "@Sizes",
                        sizeIds.Count > 0 ? sizeIds.ToArray() : (object)DBNull.Value);
                    insertCmd.Parameters.AddWithValue("@Color", (object?)colorIdText ?? DBNull.Value);
                    insertCmd.Parameters.AddWithValue("@IsActive", row.IsActive);

                    var productId = Convert.ToInt32(await insertCmd.ExecuteScalarAsync());
                    createdProducts++;

                    successRows.Add(new
                    {
                        row = row.RowNumber,
                        productId,
                        productName = row.ProductName,
                        slug,
                        brandId,
                        categoryId,
                        colorId = colorIdText,
                        sizeIds,
                        productImageUrl,
                        galleryCount = galleryUrls.Count
                    });
                }
                catch (Exception ex)
                {
                    failedRows.Add(new
                    {
                        row = row.RowNumber,
                        productName = row.ProductName,
                        error = ex.Message
                    });
                }
            }

            return new OkObjectResult(new
            {
                success = true,
                message = $"Import finished. {createdProducts} products created, {failedRows.Count} failed.",
                summary = new
                {
                    totalRows = rows.Count,
                    createdProducts,
                    failed = failedRows.Count,
                    createdBrands,
                    createdCategories,
                    createdColors,
                    createdSizes
                },
                created = successRows,
                errors = failedRows
            });
        }

        private Cloudinary CreateCloudinary()
        {
            var account = new Account(
                _configuration["CloudinarySettings:CloudName"],
                _configuration["CloudinarySettings:ApiKey"],
                _configuration["CloudinarySettings:ApiSecret"]);
            return new Cloudinary(account);
        }

        private static async Task<string?> UploadImageFromLinkAsync(
            Cloudinary cloudinary,
            HttpClient httpClient,
            string imageUrl,
            string folder)
        {
            imageUrl = imageUrl.Trim();
            if (!Uri.TryCreate(imageUrl, UriKind.Absolute, out var uri) ||
                (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
            {
                throw new Exception($"Invalid image URL: {imageUrl}");
            }

            try
            {
                var uploadParams = new ImageUploadParams
                {
                    File = new FileDescription(uri.ToString()),
                    Folder = folder
                };
                var result = await cloudinary.UploadAsync(uploadParams);
                if (result.Error == null && !string.IsNullOrWhiteSpace(result.SecureUrl?.ToString()))
                    return result.SecureUrl!.ToString();
            }
            catch
            {
                // Fall through to download-then-upload
            }

            using var response = await httpClient.GetAsync(uri);
            if (!response.IsSuccessStatusCode)
                throw new Exception($"Could not download image: {imageUrl} ({(int)response.StatusCode})");

            await using var stream = await response.Content.ReadAsStreamAsync();
            var fileName = Path.GetFileName(uri.AbsolutePath);
            if (string.IsNullOrWhiteSpace(fileName) || !fileName.Contains('.'))
                fileName = $"import-{Guid.NewGuid():N}.jpg";

            var fallbackParams = new ImageUploadParams
            {
                File = new FileDescription(fileName, stream),
                Folder = folder
            };
            var fallbackResult = await cloudinary.UploadAsync(fallbackParams);
            if (fallbackResult.Error != null)
                throw new Exception($"Image upload failed for {imageUrl}: {fallbackResult.Error.Message}");

            return fallbackResult.SecureUrl?.ToString();
        }

        private static async Task<bool> SkuExistsAsync(NpgsqlConnection con, string sku)
        {
            await using var cmd = new NpgsqlCommand(
                "SELECT EXISTS(SELECT 1 FROM products WHERE LOWER(sku) = LOWER(@sku))",
                con);
            cmd.Parameters.AddWithValue("@sku", sku.Trim());
            var result = await cmd.ExecuteScalarAsync();
            return result is bool exists && exists;
        }

        private static async Task<(int Id, bool Created)> GetOrCreateBrandAsync(
            NpgsqlConnection con,
            string brandName)
        {
            brandName = brandName.Trim();

            await using (var find = new NpgsqlCommand(
                "SELECT id FROM brands WHERE LOWER(brand_name) = LOWER(@name) LIMIT 1", con))
            {
                find.Parameters.AddWithValue("@name", brandName);
                var existing = await find.ExecuteScalarAsync();
                if (existing != null && existing != DBNull.Value)
                    return (Convert.ToInt32(existing), false);
            }

            var slug = await SlugHelper.GenerateUniqueSlugAsync(con, "brands", "slug", brandName);
            await using var insert = new NpgsqlCommand("""
                INSERT INTO brands (brand_name, slug, brand_img, is_active)
                VALUES (@name, @slug, @img, TRUE)
                RETURNING id
                """, con);
            insert.Parameters.AddWithValue("@name", brandName);
            insert.Parameters.AddWithValue("@slug", slug);
            insert.Parameters.AddWithValue("@img", "");
            var id = Convert.ToInt32(await insert.ExecuteScalarAsync());
            return (id, true);
        }

        private static async Task<(int Id, bool Created)> GetOrCreateCategoryAsync(
            NpgsqlConnection con,
            string categoryName)
        {
            categoryName = categoryName.Trim();

            await using (var find = new NpgsqlCommand("""
                SELECT id FROM categories
                WHERE LOWER(category_name) = LOWER(@name)
                  AND parent_id IS NULL
                LIMIT 1
                """, con))
            {
                find.Parameters.AddWithValue("@name", categoryName);
                var existing = await find.ExecuteScalarAsync();
                if (existing != null && existing != DBNull.Value)
                    return (Convert.ToInt32(existing), false);
            }

            var slug = await SlugHelper.GenerateUniqueSlugAsync(
                con, "categories", "slug", categoryName);

            await using var insert = new NpgsqlCommand("""
                INSERT INTO categories
                (category_name, slug, parent_id, type, category_image, browsecategory, herosection, is_active)
                VALUES (@name, @slug, NULL, NULL, '', FALSE, FALSE, TRUE)
                RETURNING id
                """, con);
            insert.Parameters.AddWithValue("@name", categoryName);
            insert.Parameters.AddWithValue("@slug", slug);
            var id = Convert.ToInt32(await insert.ExecuteScalarAsync());
            return (id, true);
        }

        private static async Task<(int Id, bool Created)> GetOrCreateColorAsync(
            NpgsqlConnection con,
            string colorName,
            string? colorCode)
        {
            colorName = colorName.Trim();

            await using (var find = new NpgsqlCommand(
                "SELECT id FROM colors WHERE LOWER(color_name) = LOWER(@name) LIMIT 1", con))
            {
                find.Parameters.AddWithValue("@name", colorName);
                var existing = await find.ExecuteScalarAsync();
                if (existing != null && existing != DBNull.Value)
                    return (Convert.ToInt32(existing), false);
            }

            if (string.IsNullOrWhiteSpace(colorCode) || !colorCode.Trim().StartsWith('#'))
                colorCode = BuildFallbackColorCode(colorName);
            else
                colorCode = colorCode.Trim().ToUpperInvariant();

            var slug = await SlugHelper.GenerateUniqueSlugAsync(con, "colors", "slug", colorName);
            await using var insert = new NpgsqlCommand("""
                INSERT INTO colors (color_name, slug, color_code, status)
                VALUES (@name, @slug, @code, TRUE)
                RETURNING id
                """, con);
            insert.Parameters.AddWithValue("@name", colorName);
            insert.Parameters.AddWithValue("@slug", slug);
            insert.Parameters.AddWithValue("@code", colorCode);
            var id = Convert.ToInt32(await insert.ExecuteScalarAsync());
            return (id, true);
        }

        private static async Task<(int Id, bool Created)> GetOrCreateSizeAsync(
            NpgsqlConnection con,
            string sizeName)
        {
            sizeName = sizeName.Trim();

            await using (var find = new NpgsqlCommand(
                "SELECT id FROM sizes WHERE LOWER(size_name) = LOWER(@name) LIMIT 1", con))
            {
                find.Parameters.AddWithValue("@name", sizeName);
                var existing = await find.ExecuteScalarAsync();
                if (existing != null && existing != DBNull.Value)
                    return (Convert.ToInt32(existing), false);
            }

            var slug = await SlugHelper.GenerateUniqueSlugAsync(con, "sizes", "slug", sizeName);
            await using var insert = new NpgsqlCommand("""
                INSERT INTO sizes (size_name, slug, created_at, is_active)
                VALUES (@name, @slug, NOW(), TRUE)
                RETURNING id
                """, con);
            insert.Parameters.AddWithValue("@name", sizeName);
            insert.Parameters.AddWithValue("@slug", slug);
            var id = Convert.ToInt32(await insert.ExecuteScalarAsync());
            return (id, true);
        }

        private static string BuildFallbackColorCode(string colorName)
        {
            var hash = colorName.ToLowerInvariant().Aggregate(0, (h, c) => h * 31 + c);
            var r = Math.Clamp((hash >> 16) & 0xFF, 80, 220);
            var g = Math.Clamp((hash >> 8) & 0xFF, 80, 220);
            var b = Math.Clamp(hash & 0xFF, 80, 220);
            return $"#{r:X2}{g:X2}{b:X2}";
        }
    }
}
