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
         Task<IActionResult> GetWishlist();
         Task<IActionResult> WishlistDelete(int id);

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

                // Guest Wishlist -> User Wishlist Migration

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

            AND (
                variantid = @variantid
                OR
                (
                    variantid IS NULL
                    AND @variantid IS NULL
                )
            )

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

                // Duplicate Check

                string checkQuery = @"
                    SELECT COUNT(*)
                    FROM wishlist
                    WHERE productid = @productid

                    AND (
                        variantid = @variantid
                        OR
                        (
                            variantid IS NULL
                            AND @variantid IS NULL
                        )
                    )

                    AND (
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

                int count = Convert.ToInt32(
                    await checkCmd.ExecuteScalarAsync()
                );

                if (count > 0)
                {
                    return new OkObjectResult(new
                    {
                        success = false,
                        message = "Product already exists in wishlist"
                    });
                }

                // Insert Wishlist

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
                    message = "Product added to wishlist successfully",
                    wishlistId = wishlistId
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
        public async Task<IActionResult> GetWishlist()
        {
            try
            {
                var wishlist =
                    new List<WishlistModel>();

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
    p.sku,
    p.categoryid,
    p.baseprice,
    p.saleprice,
    p.mrp,
    p.stock,
    p.productimageurl,

    pv.id AS variant_id,
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

ORDER BY w.createdat DESC";

                using var cmd =
                    new NpgsqlCommand(
                        query,
                        con
                    );

                using var reader =
                    await cmd.ExecuteReaderAsync();

                while (await reader.ReadAsync())
                {
                    var item =
                        new WishlistModel
                        {
                            Id =
                                Convert.ToInt32(
                                    reader["id"]
                                ),

                            UserId =
                                reader["userid"] == DBNull.Value
                                ? null
                                : Convert.ToInt32(
                                    reader["userid"]
                                ),

                            ProductId =
                                Convert.ToInt32(
                                    reader["productid"]
                                ),

                            VariantId =
                                reader["variantid"] == DBNull.Value
                                ? null
                                : Convert.ToInt32(
                                    reader["variantid"]
                                ),

                            IpAddress =
                                reader["ipaddress"]?.ToString(),

                            CreatedAt =
                                Convert.ToDateTime(
                                    reader["createdat"]
                                ),

                            Product =
                                new ProductModel
                                {
                                    Id =
                                        Convert.ToInt32(
                                            reader["productid"]
                                        ),

                                    ProductName =
                                        reader["productname"]
                                        ?.ToString(),

                                    SKU =
                                        reader["sku"]
                                        ?.ToString(),

                                    CategoryId =
                                        reader["categoryid"] == DBNull.Value
                                        ? null
                                        : Convert.ToInt32(
                                            reader["categoryid"]
                                        ),

                                    BasePrice =
                                        reader["baseprice"] == DBNull.Value
                                        ? null
                                        : Convert.ToDecimal(
                                            reader["baseprice"]
                                        ),

                                    SalePrice =
                                        reader["saleprice"] == DBNull.Value
                                        ? null
                                        : Convert.ToDecimal(
                                            reader["saleprice"]
                                        ),

                                    MRP =
                                        reader["mrp"] == DBNull.Value
                                        ? 0
                                        : Convert.ToDecimal(
                                            reader["mrp"]
                                        ),

                                    Stock =
                                        reader["stock"] == DBNull.Value
                                        ? 0
                                        : Convert.ToInt32(
                                            reader["stock"]
                                        ),

                                    ProductImageUrl =
                                        reader["productimageurl"]
                                        ?.ToString()
                                }
                        };

                    if (item.VariantId != null)
                    {
                        item.Variant =
                            new ProductVariantModel
                            {
                                Id =
                                    Convert.ToInt32(
                                        reader["variant_id"]
                                    ),

                                ProductId =
                                    item.ProductId,

                                SKU =
                                    reader["variant_sku"]
                                    ?.ToString(),

                                BasePrice =
                                    reader["variant_baseprice"] == DBNull.Value
                                    ? null
                                    : Convert.ToDecimal(
                                        reader["variant_baseprice"]
                                    ),

                                SalePrice =
                                    reader["variant_saleprice"] == DBNull.Value
                                    ? null
                                    : Convert.ToDecimal(
                                        reader["variant_saleprice"]
                                    ),

                                MRP =
                                    reader["variant_mrp"] == DBNull.Value
                                    ? null
                                    : Convert.ToDecimal(
                                        reader["variant_mrp"]
                                    ),

                                Stock =
                                    reader["variant_stock"] == DBNull.Value
                                    ? 0
                                    : Convert.ToInt32(
                                        reader["variant_stock"]
                                    ),

                                VariantImageUrl =
                                    reader["variantimageurl"]
                                    ?.ToString()
                            };
                    }

                    wishlist.Add(item);
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
                    innerException =
                        ex.InnerException?.Message
                })
                {
                    StatusCode = 500
                };
            }
        }


        public async Task<IActionResult> WishlistDelete(int id)
        {
            try
            {
                using var con =
                    new NpgsqlConnection(DbConnection);

                await con.OpenAsync();

                // Check Wishlist Exists

                string checkQuery = @"
            SELECT COUNT(*)
            FROM wishlist
            WHERE id = @id";

                using var checkCmd =
                    new NpgsqlCommand(
                        checkQuery,
                        con
                    );

                checkCmd.Parameters.AddWithValue(
                    "@id",
                    id
                );

                int count =
                    Convert.ToInt32(
                        await checkCmd.ExecuteScalarAsync()
                    );

                if (count == 0)
                {
                    return new NotFoundObjectResult(new
                    {
                        success = false,
                        message = "Wishlist item not found"
                    });
                }

                // Delete Wishlist

                string deleteQuery = @"
            DELETE FROM wishlist
            WHERE id = @id";

                using var deleteCmd =
                    new NpgsqlCommand(
                        deleteQuery,
                        con
                    );

                deleteCmd.Parameters.AddWithValue(
                    "@id",
                    id
                );

                await deleteCmd.ExecuteNonQueryAsync();

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