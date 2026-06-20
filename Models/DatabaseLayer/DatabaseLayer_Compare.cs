using Ecommerce_Backend.Helpers;
using Microsoft.AspNetCore.Mvc;
using Npgsql;

namespace Ecommerce_Backend.Models.DatabaseLayer
{
    public partial interface IDatabaseLayer
    {
        Task<IActionResult> AddCompare([FromForm] CompareModel compare);
        Task<IActionResult> GetCompareList(int? userId, string? ipAddress);
        Task<IActionResult> CompareDelete(int id, int? userId, string? ipAddress);
        Task<IActionResult> ClearCompare(int? userId, string? ipAddress);
        Task<IActionResult> CompareProductsByIds(int[] productIds);
        Task MergeGuestCompareToUser(int userId, string ipAddress);
    }

    public partial class DataBaseLayer : IDatabaseLayer
    {
        public async Task<IActionResult> AddCompare([FromForm] CompareModel compare)
        {
            try
            {
                using var con = new NpgsqlConnection(DbConnection);
                await con.OpenAsync();

                if (compare.UserId.HasValue && !string.IsNullOrWhiteSpace(compare.IpAddress))
                {
                    await CompareHelper.MergeGuestCompareToUserAsync(
                        con, compare.UserId.Value, compare.IpAddress);
                }

                compare.VariantId = await CartHelper.ResolveVariantIdAsync(
                    con, compare.ProductId, compare.VariantId, compare.ColorId, compare.SizeId);

                if (!compare.VariantId.HasValue &&
                    (compare.ColorId.HasValue || compare.SizeId.HasValue))
                {
                    return new BadRequestObjectResult(new
                    {
                        success = false,
                        message = "No variant found for the selected color and size."
                    });
                }

                if (compare.VariantId.HasValue)
                {
                    const string variantQuery = "SELECT productid FROM product_variants WHERE id = @variantid";
                    await using var variantCmd = new NpgsqlCommand(variantQuery, con);
                    variantCmd.Parameters.AddWithValue("@variantid", compare.VariantId.Value);
                    var productId = await variantCmd.ExecuteScalarAsync();
                    if (productId == null)
                    {
                        return new BadRequestObjectResult(new { success = false, message = "Variant not found" });
                    }
                    compare.ProductId = Convert.ToInt32(productId);
                }

                var countQuery = compare.UserId.HasValue
                    ? "SELECT COUNT(*) FROM product_compare WHERE userid = @userid"
                    : "SELECT COUNT(*) FROM product_compare WHERE userid IS NULL AND ipaddress = @ipaddress";

                await using (var countCmd = new NpgsqlCommand(countQuery, con))
                {
                    if (compare.UserId.HasValue)
                        countCmd.Parameters.AddWithValue("@userid", compare.UserId.Value);
                    else
                        countCmd.Parameters.AddWithValue("@ipaddress", compare.IpAddress ?? "");

                    var currentCount = Convert.ToInt32(await countCmd.ExecuteScalarAsync());
                    if (currentCount >= CompareHelper.MaxCompareItems)
                    {
                        return new BadRequestObjectResult(new
                        {
                            success = false,
                            message = $"You can compare maximum {CompareHelper.MaxCompareItems} products at a time."
                        });
                    }
                }

                string checkQuery = compare.UserId.HasValue
                    ? """
                SELECT COUNT(*) FROM product_compare
                WHERE productid = @productid
                  AND COALESCE(variantid, 0) = COALESCE(@variantid, 0)
                  AND userid = @userid
                """
                    : """
                SELECT COUNT(*) FROM product_compare
                WHERE productid = @productid
                  AND COALESCE(variantid, 0) = COALESCE(@variantid, 0)
                  AND userid IS NULL AND ipaddress = @ipaddress
                """;

                await using (var dupCmd = new NpgsqlCommand(checkQuery, con))
                {
                    dupCmd.Parameters.AddWithValue("@productid", compare.ProductId);
                    dupCmd.Parameters.AddWithValue("@variantid", compare.VariantId ?? (object)DBNull.Value);
                    if (compare.UserId.HasValue)
                        dupCmd.Parameters.AddWithValue("@userid", compare.UserId.Value);
                    else
                        dupCmd.Parameters.AddWithValue("@ipaddress", compare.IpAddress ?? "");

                    if (Convert.ToInt32(await dupCmd.ExecuteScalarAsync()) > 0)
                    {
                        return new OkObjectResult(new
                        {
                            success = false,
                            message = "Product already in compare list"
                        });
                    }
                }

                const string insertQuery = """
                    INSERT INTO product_compare (userid, productid, variantid, ipaddress, createdat)
                    VALUES (@userid, @productid, @variantid, @ipaddress, NOW())
                    RETURNING id
                    """;

                await using var insertCmd = new NpgsqlCommand(insertQuery, con);
                insertCmd.Parameters.AddWithValue("@userid", compare.UserId ?? (object)DBNull.Value);
                insertCmd.Parameters.AddWithValue("@productid", compare.ProductId);
                insertCmd.Parameters.AddWithValue("@variantid", compare.VariantId ?? (object)DBNull.Value);
                insertCmd.Parameters.AddWithValue("@ipaddress",
                    compare.UserId.HasValue ? DBNull.Value : compare.IpAddress ?? "");

                var compareId = await insertCmd.ExecuteScalarAsync();

                return new OkObjectResult(new
                {
                    success = true,
                    message = "Product added to compare list",
                    compareId,
                    productId = compare.ProductId,
                    variantId = compare.VariantId
                });
            }
            catch (Exception ex)
            {
                return new ObjectResult(new
                {
                    success = false,
                    message = ex.Message,
                    innerException = ex.InnerException?.Message
                }) { StatusCode = 500 };
            }
        }

        public async Task<IActionResult> GetCompareList(int? userId, string? ipAddress)
        {
            try
            {
                var items = await FetchCompareItemsAsync(userId, ipAddress);
                return new OkObjectResult(new
                {
                    success = true,
                    count = items.Count,
                    maxAllowed = CompareHelper.MaxCompareItems,
                    data = items
                });
            }
            catch (Exception ex)
            {
                return new ObjectResult(new { success = false, message = ex.Message }) { StatusCode = 500 };
            }
        }

        public async Task<IActionResult> CompareProductsByIds(int[] productIds)
        {
            try
            {
                if (productIds.Length == 0)
                {
                    return new BadRequestObjectResult(new { success = false, message = "At least one product id is required." });
                }

                if (productIds.Length > CompareHelper.MaxCompareItems)
                {
                    return new BadRequestObjectResult(new
                    {
                        success = false,
                        message = $"Maximum {CompareHelper.MaxCompareItems} products can be compared at once."
                    });
                }

                using var con = new NpgsqlConnection(DbConnection);
                await con.OpenAsync();

                const string query = """
                    SELECT
                        p.id AS productid,
                        p.productname,
                        p.slug AS product_slug,
                        p.shortdescription,
                        p.description,
                        p.sku,
                        p.mrp,
                        p.saleprice,
                        p.discountprice,
                        p.stock,
                        p.productimageurl,
                        p.isactive,
                        b.brand_name,
                        b.slug AS brand_slug,
                        c.category_name,
                        c.slug AS category_slug,
                        col.color_name,
                        col.color_code,
                        col.slug AS color_slug,
                        (
                            SELECT ARRAY_AGG(s.size_name ORDER BY s.id)
                            FROM sizes s
                            WHERE s.id = ANY(COALESCE(p.sizes, ARRAY[]::int[]))
                        ) AS size_names,
                        COALESCE((
                            SELECT AVG(rating)::numeric(10,2)
                            FROM product_reviews pr WHERE pr.product_id = p.id
                        ), 0) AS average_rating,
                        COALESCE((
                            SELECT COUNT(*)::int FROM product_reviews pr WHERE pr.product_id = p.id
                        ), 0) AS total_reviews
                    FROM products p
                    LEFT JOIN brands b ON b.id = p.brandid
                    LEFT JOIN categories c ON c.id = p.categoryid
                    LEFT JOIN colors col ON col.id = p.color::INT
                    WHERE p.id = ANY(@productIds) AND p.isactive = TRUE
                    ORDER BY array_position(@productIds, p.id)
                    """;

                await using var cmd = new NpgsqlCommand(query, con);
                cmd.Parameters.AddWithValue("@productIds", productIds);

                var products = new List<object>();
                await using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    products.Add(new
                    {
                        productId = Convert.ToInt32(reader["productid"]),
                        productName = reader["productname"]?.ToString(),
                        slug = reader["product_slug"]?.ToString(),
                        shortDescription = reader["shortdescription"]?.ToString(),
                        description = reader["description"]?.ToString(),
                        sku = reader["sku"]?.ToString(),
                        mrp = Convert.ToDecimal(reader["mrp"]),
                        salePrice = Convert.ToDecimal(reader["saleprice"]),
                        discountPercent = reader["discountprice"] == DBNull.Value ? 0m : Convert.ToDecimal(reader["discountprice"]),
                        stock = Convert.ToInt32(reader["stock"]),
                        imageUrl = reader["productimageurl"]?.ToString(),
                        brandName = reader["brand_name"]?.ToString(),
                        brandSlug = reader["brand_slug"]?.ToString(),
                        categoryName = reader["category_name"]?.ToString(),
                        categorySlug = reader["category_slug"]?.ToString(),
                        colorName = reader["color_name"]?.ToString(),
                        colorCode = reader["color_code"]?.ToString(),
                        colorSlug = reader["color_slug"]?.ToString(),
                        sizeNames = reader.IsDBNull(reader.GetOrdinal("size_names"))
                            ? new List<string>()
                            : reader.GetFieldValue<string[]>(reader.GetOrdinal("size_names")).ToList(),
                        averageRating = Convert.ToDecimal(reader["average_rating"]),
                        totalReviews = Convert.ToInt32(reader["total_reviews"])
                    });
                }

                return new OkObjectResult(new
                {
                    success = true,
                    count = products.Count,
                    maxAllowed = CompareHelper.MaxCompareItems,
                    data = products
                });
            }
            catch (Exception ex)
            {
                return new ObjectResult(new { success = false, message = ex.Message }) { StatusCode = 500 };
            }
        }

        public async Task<IActionResult> CompareDelete(int id, int? userId, string? ipAddress)
        {
            try
            {
                using var con = new NpgsqlConnection(DbConnection);
                await con.OpenAsync();

                var deleteQuery = userId.HasValue
                    ? "DELETE FROM product_compare WHERE id = @id AND userid = @userid"
                    : "DELETE FROM product_compare WHERE id = @id AND userid IS NULL AND ipaddress = @ipaddress";

                await using var cmd = new NpgsqlCommand(deleteQuery, con);
                cmd.Parameters.AddWithValue("@id", id);
                if (userId.HasValue)
                    cmd.Parameters.AddWithValue("@userid", userId.Value);
                else
                    cmd.Parameters.AddWithValue("@ipaddress", ipAddress ?? "");

                if (await cmd.ExecuteNonQueryAsync() == 0)
                {
                    return new NotFoundObjectResult(new { success = false, message = "Compare item not found or access denied" });
                }

                return new OkObjectResult(new { success = true, message = "Product removed from compare list" });
            }
            catch (Exception ex)
            {
                return new ObjectResult(new { success = false, message = ex.Message }) { StatusCode = 500 };
            }
        }

        public async Task<IActionResult> ClearCompare(int? userId, string? ipAddress)
        {
            try
            {
                using var con = new NpgsqlConnection(DbConnection);
                await con.OpenAsync();

                var query = userId.HasValue
                    ? "DELETE FROM product_compare WHERE userid = @userid"
                    : "DELETE FROM product_compare WHERE userid IS NULL AND ipaddress = @ipaddress";

                await using var cmd = new NpgsqlCommand(query, con);
                if (userId.HasValue)
                    cmd.Parameters.AddWithValue("@userid", userId.Value);
                else
                    cmd.Parameters.AddWithValue("@ipaddress", ipAddress ?? "");

                var deleted = await cmd.ExecuteNonQueryAsync();
                return new OkObjectResult(new
                {
                    success = true,
                    message = "Compare list cleared",
                    deletedRows = deleted
                });
            }
            catch (Exception ex)
            {
                return new ObjectResult(new { success = false, message = ex.Message }) { StatusCode = 500 };
            }
        }

        public async Task MergeGuestCompareToUser(int userId, string ipAddress)
        {
            using var con = new NpgsqlConnection(DbConnection);
            await con.OpenAsync();
            await CompareHelper.MergeGuestCompareToUserAsync(con, userId, ipAddress);
        }

        private async Task<List<object>> FetchCompareItemsAsync(int? userId, string? ipAddress)
        {
            var items = new List<object>();

            using var con = new NpgsqlConnection(DbConnection);
            await con.OpenAsync();

            const string query = """
                SELECT
                    pc.id AS compare_id,
                    pc.productid,
                    pc.variantid,
                    pc.createdat,
                    p.productname,
                    p.slug AS product_slug,
                    p.shortdescription,
                    p.description,
                    p.sku,
                    COALESCE(pv.mrp, p.mrp) AS mrp,
                    COALESCE(pv.saleprice, p.saleprice) AS saleprice,
                    COALESCE(pv.discountpercent, p.discountprice) AS discountprice,
                    COALESCE(pv.stock, p.stock) AS stock,
                    COALESCE(pv.variantimageurl, p.productimageurl) AS imageurl,
                    p.isactive,
                    b.brand_name,
                    b.slug AS brand_slug,
                    c.category_name,
                    c.slug AS category_slug,
                    col.color_name,
                    col.color_code,
                    col.slug AS color_slug,
                    (
                        SELECT ARRAY_AGG(s.size_name ORDER BY s.id)
                        FROM sizes s
                        WHERE s.id = ANY(COALESCE(pv.sizes, p.sizes, ARRAY[]::int[]))
                    ) AS size_names,
                    COALESCE((
                        SELECT AVG(rating)::numeric(10,2)
                        FROM product_reviews pr WHERE pr.product_id = p.id
                    ), 0) AS average_rating,
                    COALESCE((
                        SELECT COUNT(*)::int FROM product_reviews pr WHERE pr.product_id = p.id
                    ), 0) AS total_reviews,
                    pv.slug AS variant_slug,
                    pv.sku AS variant_sku
                FROM product_compare pc
                INNER JOIN products p ON p.id = pc.productid
                LEFT JOIN product_variants pv ON pv.id = pc.variantid
                LEFT JOIN brands b ON b.id = p.brandid
                LEFT JOIN categories c ON c.id = p.categoryid
                LEFT JOIN colors col ON col.id = CAST(NULLIF(COALESCE(pv.color, p.color), '') AS INTEGER)
                WHERE
                """;

            var where = userId.HasValue
                ? " pc.userid = @userid "
                : " pc.userid IS NULL AND pc.ipaddress = @ipaddress ";

            await using var cmd = new NpgsqlCommand(query + where + " ORDER BY pc.createdat ASC", con);
            if (userId.HasValue)
                cmd.Parameters.AddWithValue("@userid", userId.Value);
            else
                cmd.Parameters.AddWithValue("@ipaddress", ipAddress ?? "");

            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                items.Add(new
                {
                    compareId = Convert.ToInt32(reader["compare_id"]),
                    productId = Convert.ToInt32(reader["productid"]),
                    variantId = reader["variantid"] == DBNull.Value ? (int?)null : Convert.ToInt32(reader["variantid"]),
                    productName = reader["productname"]?.ToString(),
                    slug = reader["product_slug"]?.ToString(),
                    shortDescription = reader["shortdescription"]?.ToString(),
                    description = reader["description"]?.ToString(),
                    sku = reader["variant_sku"] == DBNull.Value ? reader["sku"]?.ToString() : reader["variant_sku"]?.ToString(),
                    mrp = Convert.ToDecimal(reader["mrp"]),
                    salePrice = Convert.ToDecimal(reader["saleprice"]),
                    discountPercent = reader["discountprice"] == DBNull.Value ? 0m : Convert.ToDecimal(reader["discountprice"]),
                    stock = Convert.ToInt32(reader["stock"]),
                    imageUrl = reader["imageurl"]?.ToString(),
                    brandName = reader["brand_name"]?.ToString(),
                    brandSlug = reader["brand_slug"]?.ToString(),
                    categoryName = reader["category_name"]?.ToString(),
                    categorySlug = reader["category_slug"]?.ToString(),
                    colorName = reader["color_name"]?.ToString(),
                    colorCode = reader["color_code"]?.ToString(),
                    colorSlug = reader["color_slug"]?.ToString(),
                    sizeNames = reader.IsDBNull(reader.GetOrdinal("size_names"))
                        ? new List<string>()
                        : reader.GetFieldValue<string[]>(reader.GetOrdinal("size_names")).ToList(),
                    averageRating = Convert.ToDecimal(reader["average_rating"]),
                    totalReviews = Convert.ToInt32(reader["total_reviews"]),
                    variantSlug = reader["variant_slug"] == DBNull.Value ? null : reader["variant_slug"]?.ToString(),
                    createdAt = Convert.ToDateTime(reader["createdat"])
                });
            }

            return items;
        }
    }
}
