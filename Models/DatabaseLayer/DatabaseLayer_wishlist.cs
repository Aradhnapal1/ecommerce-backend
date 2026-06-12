using Microsoft.AspNetCore.Mvc;
using Npgsql;
using NuGet.Packaging.Signing;

namespace Ecommerce_Backend.Models.DatabaseLayer
{
    public partial interface IDatabaseLayer
    {
        Task<IActionResult> AddWishlist(
            [FromForm] WishlistModel wishlist
        );
         Task<IActionResult> GetWishlist(int? userId, string? ipAddress);
         Task<IActionResult> WishlistDelete(int id, int? userId, string? ipAddress);

    }

    public partial class DataBaseLayer : IDatabaseLayer
    {
        public async Task<IActionResult> AddWishlist(
       [FromForm] WishlistModel wishlist)
        {
            try
            {
                using var con =
                    new NpgsqlConnection(DbConnection);

                await con.OpenAsync();

                // =========================================
                // Get ProductId From Variant
                // =========================================

                if (wishlist.VariantId != null)
                {
                    string variantQuery = @"
                SELECT productid
                FROM product_variants
                WHERE id = @variantid";

                    using var variantCmd =
                        new NpgsqlCommand(
                            variantQuery,
                            con
                        );

                    variantCmd.Parameters.AddWithValue(
                        "@variantid",
                        wishlist.VariantId.Value
                    );

                    var productId =
                        await variantCmd.ExecuteScalarAsync();

                    if (productId == null)
                    {
                        return new BadRequestObjectResult(new
                        {
                            success = false,
                            message = "Variant not found"
                        });
                    }

                    wishlist.ProductId =
                        Convert.ToInt32(productId);
                }

                // =========================================
                // Guest Wishlist Migration
                // =========================================

                if (wishlist.UserId != null &&
                    !string.IsNullOrWhiteSpace(wishlist.IpAddress))
                {
                    string migrateQuery = @"
                UPDATE wishlist
                SET
                    userid = @userid,
                    ipaddress = NULL
                WHERE
                    productid = @productid

                    AND
                    COALESCE(variantid,0)
                    =
                    COALESCE(@variantid,0)

                    AND ipaddress = @ipaddress
                    AND userid IS NULL";

                    using var migrateCmd =
                        new NpgsqlCommand(
                            migrateQuery,
                            con
                        );

                    migrateCmd.Parameters.AddWithValue(
                        "@userid",
                        wishlist.UserId
                    );

                    migrateCmd.Parameters.AddWithValue(
                        "@productid",
                        wishlist.ProductId
                    );

                    migrateCmd.Parameters.AddWithValue(
                        "@variantid",
                        wishlist.VariantId ??
                        (object)DBNull.Value
                    );

                    migrateCmd.Parameters.AddWithValue(
                        "@ipaddress",
                        wishlist.IpAddress
                    );

                    int migratedRows =
                        await migrateCmd.ExecuteNonQueryAsync();

                    if (migratedRows > 0)
                    {
                        return new OkObjectResult(new
                        {
                            success = true,
                            message = "Wishlist migrated successfully"
                        });
                    }
                }

                // =========================================
                // Duplicate Check
                // =========================================

                string checkQuery = @"
            SELECT COUNT(*)
            FROM wishlist
            WHERE
                productid = @productid

                AND
                COALESCE(variantid,0)
                =
                COALESCE(@variantid,0)

                AND
                (
                    userid = @userid
                    OR
                    ipaddress = @ipaddress
                )";

                using var checkCmd =
                    new NpgsqlCommand(
                        checkQuery,
                        con
                    );

                checkCmd.Parameters.AddWithValue(
                    "@productid",
                    wishlist.ProductId
                );

                checkCmd.Parameters.AddWithValue(
                    "@variantid",
                    wishlist.VariantId ??
                    (object)DBNull.Value
                );

                checkCmd.Parameters.AddWithValue(
                    "@userid",
                    wishlist.UserId ??
                    (object)DBNull.Value
                );

                checkCmd.Parameters.AddWithValue(
                    "@ipaddress",
                    wishlist.IpAddress ??
                    (object)DBNull.Value
                );

                int count =
                    Convert.ToInt32(
                        await checkCmd.ExecuteScalarAsync()
                    );

                if (count > 0)
                {
                    return new OkObjectResult(new
                    {
                        success = false,
                        message = "Item already exists in wishlist"
                    });
                }

                // =========================================
                // Insert Wishlist
                // =========================================

                string insertQuery = @"
            INSERT INTO wishlist
            (
                userid,
                productid,
                variantid,
                ipaddress,
                createdat
            )
            VALUES
            (
                @userid,
                @productid,
                @variantid,
                @ipaddress,
                NOW()
            )
            RETURNING id";

                using var insertCmd =
                    new NpgsqlCommand(
                        insertQuery,
                        con
                    );

                insertCmd.Parameters.AddWithValue(
                    "@userid",
                    wishlist.UserId ??
                    (object)DBNull.Value
                );

                insertCmd.Parameters.AddWithValue(
                    "@productid",
                    wishlist.ProductId
                );

                insertCmd.Parameters.AddWithValue(
                    "@variantid",
                    wishlist.VariantId ??
                    (object)DBNull.Value
                );

                insertCmd.Parameters.AddWithValue(
                    "@ipaddress",
                    wishlist.IpAddress ??
                    (object)DBNull.Value
                );

                var wishlistId =
                    await insertCmd.ExecuteScalarAsync();

                return new OkObjectResult(new
                {
                    success = true,
                    message = "Item added to wishlist successfully",
                    wishlistId
                });
            }
            catch (Exception ex)
            {
                return new ObjectResult(new
                {
                    success = false,
                    message = ex.Message,
                    innerException =
                        ex.InnerException?.Message
                })
                {
                    StatusCode = 500
                };
            }
        }


        public async Task<IActionResult> GetWishlist(int? userId, string? ipAddress)
        {
            try
            {
                if (!userId.HasValue && string.IsNullOrWhiteSpace(ipAddress))
                {
                    return new BadRequestObjectResult(new
                    {
                        success = false,
                        message = "User or guest session required"
                    });
                }

                var wishlist = new List<WishlistModel>();

                using var con =
                    new NpgsqlConnection(DbConnection);

                await con.OpenAsync();

                string query = @"
SELECT
    w.id,
    w.userid,
    w.productid,
    w.variantid,
    w.ipaddress,
    w.createdat,

    p.productname,
    p.slug AS product_slug,
    p.sku,
    p.categoryid,
    p.baseprice,
    p.saleprice,
    p.mrp,
    p.stock,
    p.productimageurl,

    pv.id AS variant_id,
    pv.variantname,
    pv.slug AS variant_slug,
    pv.sku AS variant_sku,
    pv.baseprice AS variant_baseprice,
    pv.saleprice AS variant_saleprice,
    pv.mrp AS variant_mrp,
    pv.stock AS variant_stock,
    pv.variantimageurl

FROM wishlist w

INNER JOIN products p
    ON p.id = w.productid

LEFT JOIN product_variants pv
    ON pv.id = w.variantid

WHERE ";

                query += userId.HasValue
                    ? "w.userid = @userid"
                    : "w.userid IS NULL AND w.ipaddress = @ipaddress";

                query += " ORDER BY w.createdat DESC";

                using var cmd =
                    new NpgsqlCommand(query, con);

                if (userId.HasValue)
                    cmd.Parameters.AddWithValue("@userid", userId.Value);
                else
                    cmd.Parameters.AddWithValue("@ipaddress", ipAddress ?? "");

                using var reader =
                    await cmd.ExecuteReaderAsync();

                while (await reader.ReadAsync())
                {
                    var wishlistItem =
                        new WishlistModel
                        {
                            Id = Convert.ToInt32(reader["id"]),

                            UserId =
                                reader["userid"] == DBNull.Value
                                ? null
                                : Convert.ToInt32(reader["userid"]),

                            ProductId =
                                Convert.ToInt32(reader["productid"]),

                            VariantId =
                                reader["variantid"] == DBNull.Value
                                ? null
                                : Convert.ToInt32(reader["variantid"]),

                            IpAddress =
                                reader["ipaddress"]?.ToString(),

                            CreatedAt =
                                Convert.ToDateTime(reader["createdat"])
                        };

                    // Variant Item
                    if (wishlistItem.VariantId != null)
                    {
                        wishlistItem.Item = new
                        {
                            Id =
                                Convert.ToInt32(reader["variant_id"]),

                            Name =
                                reader["variantname"]?.ToString(),

                            Slug =
                                reader["variant_slug"]?.ToString(),

                            SKU =
                                reader["variant_sku"]?.ToString(),

                            BasePrice =
                                reader["variant_baseprice"] == DBNull.Value
                                ? (decimal?)null
                                : Convert.ToDecimal(reader["variant_baseprice"]),

                            SalePrice =
                                reader["variant_saleprice"] == DBNull.Value
                                ? (decimal?)null
                                : Convert.ToDecimal(reader["variant_saleprice"]),

                            MRP =
                                reader["variant_mrp"] == DBNull.Value
                                ? (decimal?)null
                                : Convert.ToDecimal(reader["variant_mrp"]),

                            Stock =
                                reader["variant_stock"] == DBNull.Value
                                ? (int?)null
                                : Convert.ToInt32(reader["variant_stock"]),

                            Image =
                                reader["variantimageurl"]?.ToString(),

                            ItemType = "VARIANT"
                        };
                    }
                    else
                    {
                        // Product Item

                        wishlistItem.Item = new
                        {
                            Id =
                                Convert.ToInt32(reader["productid"]),

                            Name =
                                reader["productname"]?.ToString(),

                            Slug =
                                reader["product_slug"]?.ToString(),

                            SKU =
                                reader["sku"]?.ToString(),

                            CategoryId =
                                reader["categoryid"] == DBNull.Value
                                ? (int?)null
                                : Convert.ToInt32(reader["categoryid"]),

                            BasePrice =
                                reader["baseprice"] == DBNull.Value
                                ? (decimal?)null
                                : Convert.ToDecimal(reader["baseprice"]),

                            SalePrice =
                                reader["saleprice"] == DBNull.Value
                                ? (decimal?)null
                                : Convert.ToDecimal(reader["saleprice"]),

                            MRP =
                                reader["mrp"] == DBNull.Value
                                ? (decimal?)null
                                : Convert.ToDecimal(reader["mrp"]),

                            Stock =
                                reader["stock"] == DBNull.Value
                                ? (int?)null
                                : Convert.ToInt32(reader["stock"]),

                            Image =
                                reader["productimageurl"]?.ToString(),

                            ItemType = "PRODUCT"
                        };
                    }

                    wishlist.Add(wishlistItem);
                }

                return new OkObjectResult(new
                {
                    success = true,
                    count = wishlist.Count,
                    data = wishlist
                });
            }
            catch (Exception ex)
            {
                return new ObjectResult(new
                {
                    success = false,
                    message = ex.Message,
                    innerException = ex.InnerException?.Message
                })
                {
                    StatusCode = 500
                };
            }
        }


        public async Task<IActionResult> WishlistDelete(int id, int? userId, string? ipAddress)
        {
            try
            {
                using var con =
                    new NpgsqlConnection(DbConnection);

                await con.OpenAsync();

                string deleteQuery;

                if (userId.HasValue)
                {
                    deleteQuery = @"
            DELETE FROM wishlist
            WHERE id = @id AND userid = @userid";
                }
                else
                {
                    deleteQuery = @"
            DELETE FROM wishlist
            WHERE id = @id AND userid IS NULL AND ipaddress = @ipaddress";
                }

                using var deleteCmd =
                    new NpgsqlCommand(
                        deleteQuery,
                        con
                    );

                deleteCmd.Parameters.AddWithValue("@id", id);

                if (userId.HasValue)
                    deleteCmd.Parameters.AddWithValue("@userid", userId.Value);
                else
                    deleteCmd.Parameters.AddWithValue("@ipaddress", ipAddress ?? "");

                var rows = await deleteCmd.ExecuteNonQueryAsync();

                if (rows == 0)
                {
                    return new NotFoundObjectResult(new
                    {
                        success = false,
                        message = "Wishlist item not found or access denied"
                    });
                }

                return new OkObjectResult(new
                {
                    success = true,
                    message = "Wishlist item deleted successfully"
                });
            }
            catch (Exception ex)
            {
                return new ObjectResult(new
                {
                    success = false,
                    message = ex.Message
                })
                {
                    StatusCode = 500
                };
            }
        }

    }
}       