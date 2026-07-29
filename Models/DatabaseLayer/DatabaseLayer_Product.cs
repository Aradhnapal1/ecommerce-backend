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
        Task<List<ProductModel>> GetAllProducts();
        Task<(List<ProductModel> Products, int Total)> GetFilteredProducts(ProductFilterRequest filter);
        Task<IActionResult> AddProduct([FromForm] ProductModel product);
        Task<IActionResult> UpdateProduct(int id, [FromForm] ProductModel product);
        Task<IActionResult> DeleteProduct(int id);
        Task<ProductModel?> GetProductById(int id);
        Task<ProductModel?> GetProductBySlug(string slug);
        Task<List<ProductModel>> GetRelatedProducts(int productId, int limit);
        Task<ProductDetailModel?> GetProductDetail(int? id, string? slug);
        Task<List<ProductModel>> GetTopDiscountedProducts(int limit);
    }

    public partial class DataBaseLayer : IDatabaseLayer
    {
        private const string ProductSelectColumns = @"
    p.*,
    b.brand_name,
    b.slug AS brand_slug,
    c.category_name,
    c.slug AS category_slug,
    col.color_name,
    col.slug AS color_slug,
    (
        SELECT ARRAY_AGG(s.size_name ORDER BY s.id)
        FROM sizes s
        WHERE s.id = ANY(COALESCE(p.sizes, ARRAY[]::int[]))
    ) AS size_names,
    (
        SELECT ARRAY_AGG(s.slug ORDER BY s.id)
        FROM sizes s
        WHERE s.id = ANY(COALESCE(p.sizes, ARRAY[]::int[]))
    ) AS size_slugs,
    COALESCE((
        SELECT AVG(rating)::numeric(10,2)
        FROM product_reviews pr
        WHERE pr.product_id = p.id
    ), 0) AS average_rating,
    COALESCE((
        SELECT COUNT(*)::int
        FROM product_reviews pr
        WHERE pr.product_id = p.id
    ), 0) AS total_reviews";

        private const string ProductJoins = @"
FROM products p
LEFT JOIN brands b ON b.id = p.brandid
LEFT JOIN categories c ON c.id = p.categoryid
LEFT JOIN colors col ON col.id = p.color::INT";

        public async Task<List<ProductModel>> GetAllProducts()
        {
            var products = new List<ProductModel>();

            using var con = new NpgsqlConnection(DbConnection);
            await con.OpenAsync();

            var query = $@"
SELECT
{ProductSelectColumns}
{ProductJoins}
ORDER BY p.id DESC;
";

            using var cmd = new NpgsqlCommand(query, con);
            using var reader = await cmd.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                products.Add(MapProduct(reader));
            }

            return products;
        }

        public async Task<(List<ProductModel> Products, int Total)> GetFilteredProducts(
            ProductFilterRequest filter)
        {
            var products = new List<ProductModel>();
            var whereClauses = new List<string> { "p.isactive = true" };
            var parameters = new List<NpgsqlParameter>();

            if (filter.ResolvedCategorySlugs.Length > 0)
            {
                whereClauses.Add(@"p.categoryid IN (
    WITH RECURSIVE category_tree AS (
        SELECT id FROM categories WHERE LOWER(slug) = ANY(@categorySlugs)
        UNION ALL
        SELECT c.id FROM categories c
        INNER JOIN category_tree ct ON c.parent_id = ct.id
    )
    SELECT id FROM category_tree
)");
                parameters.Add(new NpgsqlParameter("categorySlugs", filter.ResolvedCategorySlugs));
            }
            else if (filter.ResolvedCategoryIds.Length > 0)
            {
                whereClauses.Add(@"p.categoryid IN (
    WITH RECURSIVE category_tree AS (
        SELECT id FROM categories WHERE id = ANY(@categoryIds)
        UNION ALL
        SELECT c.id FROM categories c
        INNER JOIN category_tree ct ON c.parent_id = ct.id
    )
    SELECT id FROM category_tree
)");
                parameters.Add(new NpgsqlParameter("categoryIds", filter.ResolvedCategoryIds));
            }

            if (filter.ResolvedBrandSlugs.Length > 0)
            {
                whereClauses.Add("LOWER(b.slug) = ANY(@brandSlugs)");
                parameters.Add(new NpgsqlParameter("brandSlugs", filter.ResolvedBrandSlugs));
            }
            else if (filter.ResolvedBrandIds.Length > 0)
            {
                whereClauses.Add("p.brandid = ANY(@brandIds)");
                parameters.Add(new NpgsqlParameter("brandIds", filter.ResolvedBrandIds));
            }

            if (filter.ResolvedColorSlugs.Length > 0)
            {
                whereClauses.Add("LOWER(col.slug) = ANY(@colorSlugs)");
                parameters.Add(new NpgsqlParameter("colorSlugs", filter.ResolvedColorSlugs));
            }
            else if (filter.ResolvedColorIds.Length > 0)
            {
                whereClauses.Add("p.color = ANY(@colorIds)");
                parameters.Add(new NpgsqlParameter(
                    "colorIds",
                    filter.ResolvedColorIds.Select(id => id.ToString()).ToArray()));
            }

            if (filter.ResolvedSizeSlugs.Length > 0)
            {
                whereClauses.Add(@"
EXISTS (
    SELECT 1
    FROM sizes sz
    WHERE sz.id = ANY(COALESCE(p.sizes, ARRAY[]::int[]))
      AND LOWER(sz.slug) = ANY(@sizeSlugs)
)");
                parameters.Add(new NpgsqlParameter("sizeSlugs", filter.ResolvedSizeSlugs));
            }
            else if (filter.ResolvedSizeIds.Length > 0)
            {
                whereClauses.Add("p.sizes && @sizeIds");
                parameters.Add(new NpgsqlParameter("sizeIds", filter.ResolvedSizeIds));
            }

            bool hasMinPrice = filter.MinPrice.HasValue && filter.MinPrice.Value > 0;
            bool hasMaxPrice = filter.MaxPrice.HasValue && filter.MaxPrice.Value > 0;

            if (hasMinPrice || hasMaxPrice)
            {
                var pConds = new List<string>();
                var vConds = new List<string>();

                if (hasMinPrice)
                {
                    pConds.Add("p.saleprice >= @minPrice");
                    vConds.Add("pv.saleprice >= @minPrice");
                    parameters.Add(new NpgsqlParameter("minPrice", filter.MinPrice.Value));
                }
                if (hasMaxPrice)
                {
                    pConds.Add("p.saleprice <= @maxPrice");
                    vConds.Add("pv.saleprice <= @maxPrice");
                    parameters.Add(new NpgsqlParameter("maxPrice", filter.MaxPrice.Value));
                }

                whereClauses.Add($@"({string.Join(" AND ", pConds)} OR EXISTS (
    SELECT 1 FROM product_variants pv 
    WHERE pv.productid = p.id AND {string.Join(" AND ", vConds)}
))");
            }

            if (filter.ResolvedDiscountPercents.Length > 0)
            {
                whereClauses.Add(
                    "ROUND(COALESCE(p.discountprice, 0)::numeric, 0) = ANY(@discountPercents)");
                parameters.Add(new NpgsqlParameter(
                    "discountPercents",
                    filter.ResolvedDiscountPercents
                        .Select(d => (int)Math.Round(d))
                        .ToArray()));
            }

            if (filter.HasDiscount == true)
            {
                whereClauses.Add("(COALESCE(p.discountprice, 0) > 0 OR p.saleprice < p.mrp)");
            }

            if (filter.InStock == true)
            {
                whereClauses.Add("p.stock > 0");
            }

            if (!string.IsNullOrWhiteSpace(filter.ResolvedSearch))
            {
                var searchClause = filter.UseGlobalSearch
                    ? @"(
                        p.productname ILIKE @search
                        OR p.sku ILIKE @search
                        OR p.slug ILIKE @search
                        OR p.shortdescription ILIKE @search
                        OR p.description ILIKE @search
                        OR b.brand_name ILIKE @search
                        OR b.slug ILIKE @search
                        OR c.category_name ILIKE @search
                        OR c.slug ILIKE @search
                        OR col.color_name ILIKE @search
                        OR col.slug ILIKE @search
                    )"
                    : @"(p.productname ILIKE @search OR p.sku ILIKE @search OR p.slug ILIKE @search)";

                whereClauses.Add(searchClause);
                parameters.Add(new NpgsqlParameter(
                    "search",
                    $"%{filter.ResolvedSearch}%"));
            }

            var whereSql = string.Join(" AND ", whereClauses);

            var orderBy = filter.SortBy?.ToLowerInvariant() switch
            {
                "price_low" => "p.saleprice ASC, p.id DESC",
                "price_high" => "p.saleprice DESC, p.id DESC",
                "discount_high" => "COALESCE(p.discountprice, 0) DESC, p.id DESC",
                "name" => "p.productname ASC",
                _ => "p.id DESC"
            };

            var page = filter.Page < 1 ? 1 : filter.Page;
            var pageSize = filter.PageSize < 1 ? 20 : Math.Min(filter.PageSize, 100);
            var offset = (page - 1) * pageSize;

            var baseFrom = ProductJoins;

            using var con = new NpgsqlConnection(DbConnection);
            await con.OpenAsync();

            var countQuery = $"SELECT COUNT(*) {baseFrom} WHERE {whereSql}";
            using (var countCmd = new NpgsqlCommand(countQuery, con))
            {
                foreach (var p in parameters)
                    countCmd.Parameters.Add(p);

                var total = Convert.ToInt32(await countCmd.ExecuteScalarAsync());

                var dataQuery = $@"
SELECT
{ProductSelectColumns}
{baseFrom}
WHERE {whereSql}
ORDER BY {orderBy}
LIMIT @pageSize OFFSET @offset";

                using var cmd = new NpgsqlCommand(dataQuery, con);
                foreach (var p in parameters)
                    cmd.Parameters.Add(new NpgsqlParameter(p.ParameterName, p.Value));

                cmd.Parameters.AddWithValue("pageSize", pageSize);
                cmd.Parameters.AddWithValue("offset", offset);

                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                    products.Add(MapProduct(reader));

                return (products, total);
            }
        }

        public async Task<List<ProductModel>> GetTopDiscountedProducts(int limit)
        {
            var products = new List<ProductModel>();

            using var con = new NpgsqlConnection(DbConnection);
            await con.OpenAsync();

            var query = $@"
SELECT
{ProductSelectColumns}
{ProductJoins}
WHERE p.isactive = true AND p.discountprice > 0
ORDER BY p.discountprice DESC NULLS LAST
LIMIT @limit;
";
            using var cmd = new NpgsqlCommand(query, con);
            cmd.Parameters.AddWithValue("limit", limit);
            using var reader = await cmd.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                products.Add(MapProduct(reader));
            }

            return products;
        }

        private static ProductModel MapProduct(NpgsqlDataReader reader)
        {
            return new ProductModel
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
                Sizes = reader.IsDBNull(reader.GetOrdinal("sizes"))
                    ? Array.Empty<int>()
                    : reader.GetFieldValue<int[]>(reader.GetOrdinal("sizes")),
                Color = reader["color"]?.ToString(),
                IsActive = !reader.IsDBNull(reader.GetOrdinal("isactive"))
                    && reader.GetBoolean(reader.GetOrdinal("isactive")),
                CreatedAt = reader.IsDBNull(reader.GetOrdinal("createdat"))
                    ? DateTime.MinValue
                    : reader.GetDateTime(reader.GetOrdinal("createdat")),
                UpdatedAt = reader.IsDBNull(reader.GetOrdinal("updatedat"))
                    ? DateTime.MinValue
                    : reader.GetDateTime(reader.GetOrdinal("updatedat")),
                BrandName = reader["brand_name"]?.ToString(),
                BrandSlug = reader["brand_slug"]?.ToString(),
                CategoryName = reader["category_name"]?.ToString(),
                CategorySlug = reader["category_slug"]?.ToString(),
                ColorName = reader["color_name"]?.ToString(),
                ColorSlug = reader["color_slug"]?.ToString(),
                SizeNames = reader.IsDBNull(reader.GetOrdinal("size_names"))
                    ? new List<string>()
                    : reader.GetFieldValue<string[]>(reader.GetOrdinal("size_names")).ToList(),
                SizeSlugs = reader.IsDBNull(reader.GetOrdinal("size_slugs"))
                    ? new List<string>()
                    : reader.GetFieldValue<string[]>(reader.GetOrdinal("size_slugs")).ToList(),
                AverageRating = reader.IsDBNull(reader.GetOrdinal("average_rating"))
                    ? 0
                    : Convert.ToDecimal(reader["average_rating"]),
                TotalReviews = reader.IsDBNull(reader.GetOrdinal("total_reviews"))
                    ? 0
                    : Convert.ToInt32(reader["total_reviews"])
            };
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
                string slug = await SlugHelper.GenerateUniqueSlugAsync(
                    con, "products", "slug", product.ProductName);

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

                // sizes is optional / nullable
                cmd.Parameters.AddWithValue(
                    "Sizes",
                    product.Sizes is { Length: > 0 }
                        ? product.Sizes
                        : (object)DBNull.Value
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
                    product.Sizes is { Length: > 0 }
                        ? product.Sizes
                        : (object)DBNull.Value);

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

            var query = $@"
SELECT 
{ProductSelectColumns}
{ProductJoins}
WHERE p.id = @id;
";

            using var cmd = new NpgsqlCommand(query, con);
            cmd.Parameters.AddWithValue("id", id);

            using var reader = await cmd.ExecuteReaderAsync();

            if (!await reader.ReadAsync())
                return null;

            return MapProduct(reader);
        }

        public async Task<ProductModel?> GetProductBySlug(string slug)
        {
            if (string.IsNullOrWhiteSpace(slug))
                return null;

            using var con = new NpgsqlConnection(DbConnection);
            await con.OpenAsync();

            var query = $@"
SELECT
{ProductSelectColumns}
{ProductJoins}
WHERE LOWER(p.slug) = LOWER(@slug) AND p.isactive = true
LIMIT 1;
";

            using var cmd = new NpgsqlCommand(query, con);
            cmd.Parameters.AddWithValue("slug", slug.Trim());

            using var reader = await cmd.ExecuteReaderAsync();
            if (!await reader.ReadAsync())
                return null;

            return MapProduct(reader);
        }

        public async Task<List<ProductModel>> GetRelatedProducts(int productId, int limit)
        {
            var products = new List<ProductModel>();
            if (limit < 1) limit = 8;
            if (limit > 24) limit = 24;

            using var con = new NpgsqlConnection(DbConnection);
            await con.OpenAsync();

            const string contextQuery = """
                SELECT categoryid, brandid
                FROM products
                WHERE id = @productId AND isactive = true
                LIMIT 1
                """;

            int? categoryId = null;
            int? brandId = null;

            await using (var contextCmd = new NpgsqlCommand(contextQuery, con))
            {
                contextCmd.Parameters.AddWithValue("productId", productId);
                await using var contextReader = await contextCmd.ExecuteReaderAsync();
                if (!await contextReader.ReadAsync())
                    return products;

                categoryId = contextReader["categoryid"] == DBNull.Value
                    ? null
                    : Convert.ToInt32(contextReader["categoryid"]);
                brandId = contextReader["brandid"] == DBNull.Value
                    ? null
                    : Convert.ToInt32(contextReader["brandid"]);
            }

            var query = $@"
SELECT
{ProductSelectColumns}
{ProductJoins}
WHERE p.isactive = true
  AND p.id <> @productId
  AND (
        (@categoryId IS NOT NULL AND p.categoryid = @categoryId)
        OR (@brandId IS NOT NULL AND p.brandid = @brandId)
      )
ORDER BY
  CASE
    WHEN @categoryId IS NOT NULL AND p.categoryid = @categoryId
         AND @brandId IS NOT NULL AND p.brandid = @brandId THEN 0
    WHEN @categoryId IS NOT NULL AND p.categoryid = @categoryId THEN 1
    ELSE 2
  END,
  p.discountprice DESC NULLS LAST,
  p.createdat DESC
LIMIT @limit;
";

            using var cmd = new NpgsqlCommand(query, con);
            cmd.Parameters.AddWithValue("productId", productId);
            cmd.Parameters.AddWithValue("categoryId", (object?)categoryId ?? DBNull.Value);
            cmd.Parameters.AddWithValue("brandId", (object?)brandId ?? DBNull.Value);
            cmd.Parameters.AddWithValue("limit", limit);

            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
                products.Add(MapProduct(reader));

            return products;
        }

        public async Task<ProductDetailModel?> GetProductDetail(int? id, string? slug)
        {
            ProductModel? product = id.HasValue
                ? await GetProductById(id.Value)
                : await GetProductBySlug(slug ?? string.Empty);

            if (product == null || !product.IsActive)
                return null;

            var variants = await GetVariantsByProductId(product.Id);
            var related = await GetRelatedProducts(product.Id, 8);

            return new ProductDetailModel
            {
                Product = product,
                Variants = variants,
                Reviews = new ProductReviewSummary
                {
                    AverageRating = product.AverageRating,
                    TotalReviews = product.TotalReviews
                },
                RelatedProducts = related
            };
        }
    }
}
