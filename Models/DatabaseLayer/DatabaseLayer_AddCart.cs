using Ecommerce_Backend.Helpers;
using Microsoft.AspNetCore.Mvc;
using Npgsql;

namespace Ecommerce_Backend.Models.DatabaseLayer
{
    public partial interface IDatabaseLayer
    {
        Task<IActionResult> AddCart(
            [FromForm] AddCartModel cart
        );

        Task<IActionResult> GetCart(
    int? userId,
    string? ipAddress
);
        // Interface mein add karo
        Task<IActionResult> UpdateCartQuantity(
            UpdateCartQuantityModel model
        );
        Task<IActionResult> DeleteCartItem(int id, int? userId, string? ipAddress);
        Task<IActionResult> ClearCart(int? userId, string? ipAddress);
        Task MergeGuestCartToUser(int userId, string ipAddress);

    }

    public partial class DataBaseLayer : IDatabaseLayer
    {
        public async Task<IActionResult> AddCart(
        [FromForm] AddCartModel cart)
        {
            try
            {
                using var con =
                    new NpgsqlConnection(
                        DbConnection
                    );

                await con.OpenAsync();

                if (cart.UserId.HasValue &&
                    !string.IsNullOrWhiteSpace(cart.IpAddress))
                {
                    await CartHelper.MergeGuestCartToUserAsync(
                        con,
                        cart.UserId.Value,
                        cart.IpAddress);
                }

                var selection = await CartHelper.PrepareCartSelectionAsync(con, cart);
                if (!selection.Success)
                {
                    return new BadRequestObjectResult(new
                    {
                        success = false,
                        message = selection.ErrorMessage
                    });
                }

                // ====================================
                // Existing Cart Check
                // ====================================

                string checkQuery;

                using var checkCmd =
                    new NpgsqlCommand();

                checkCmd.Connection = con;

                if (cart.UserId.HasValue)
                {
                    checkQuery = @"
SELECT
    id,
    quantity
FROM addcart
WHERE
    userid = @userid
    AND productid = @productid
    AND COALESCE(variantid,0)
        =
        COALESCE(@variantid,0)
    AND COALESCE(colorid,0)
        =
        COALESCE(@colorid,0)
    AND COALESCE(sizeid,0)
        =
        COALESCE(@sizeid,0)
LIMIT 1";

                    checkCmd.Parameters.AddWithValue(
                        "@userid",
                        cart.UserId.Value
                    );
                }
                else
                {
                    checkQuery = @"
SELECT
    id,
    quantity
FROM addcart
WHERE
    ipaddress = @ipaddress
    AND productid = @productid
    AND COALESCE(variantid,0)
        =
        COALESCE(@variantid,0)
    AND COALESCE(colorid,0)
        =
        COALESCE(@colorid,0)
    AND COALESCE(sizeid,0)
        =
        COALESCE(@sizeid,0)
LIMIT 1";

                    checkCmd.Parameters.AddWithValue(
                        "@ipaddress",
                        cart.IpAddress ?? ""
                    );
                }

                checkCmd.CommandText =
                    checkQuery;

                checkCmd.Parameters.AddWithValue(
                    "@productid",
                    cart.ProductId
                );

                checkCmd.Parameters.Add(
                    "@variantid",
                    NpgsqlTypes.NpgsqlDbType.Integer
                ).Value =
                    cart.VariantId.HasValue
                    ? cart.VariantId.Value
                    : DBNull.Value;

                checkCmd.Parameters.Add(
                    "@colorid",
                    NpgsqlTypes.NpgsqlDbType.Integer
                ).Value =
                    cart.ColorId.HasValue
                    ? cart.ColorId.Value
                    : DBNull.Value;

                checkCmd.Parameters.Add(
                    "@sizeid",
                    NpgsqlTypes.NpgsqlDbType.Integer
                ).Value =
                    cart.SizeId.HasValue
                    ? cart.SizeId.Value
                    : DBNull.Value;

                using var reader =
                    await checkCmd
                    .ExecuteReaderAsync();

                if (await reader.ReadAsync())
                {
                    int cartId =
                        Convert.ToInt32(
                            reader["id"]
                        );

                    int oldQty =
                        Convert.ToInt32(
                            reader["quantity"]
                        );

                    await reader.CloseAsync();

                    string updateQuery = @"
UPDATE addcart
SET
    quantity = @qty,
    updatedat = NOW()
WHERE id = @id";

                    using var updateCmd =
                        new NpgsqlCommand(
                            updateQuery,
                            con
                        );

                    updateCmd.Parameters.AddWithValue(
                        "@qty",
                        oldQty + cart.Quantity
                    );

                    updateCmd.Parameters.AddWithValue(
                        "@id",
                        cartId
                    );

                    await updateCmd
                        .ExecuteNonQueryAsync();

                    return new OkObjectResult(
                        new
                        {
                            success = true,
                            message = "Cart quantity updated",
                            cartId,
                            quantity = oldQty + cart.Quantity,
                            productId = cart.ProductId,
                            variantId = cart.VariantId,
                            colorId = cart.ColorId,
                            sizeId = cart.SizeId
                        });
                }

                await reader.CloseAsync();

                // ====================================
                // Insert New Cart Row
                // ====================================

                string insertQuery = @"
INSERT INTO addcart
(
    userid,
    productid,
    variantid,
    colorid,
    sizeid,
    quantity,
    ipaddress,
    createdat,
    updatedat
)
VALUES
(
    @userid,
    @productid,
    @variantid,
    @colorid,
    @sizeid,
    @quantity,
    @ipaddress,
    NOW(),
    NOW()
)
RETURNING id";

                using var insertCmd =
                    new NpgsqlCommand(
                        insertQuery,
                        con
                    );

                insertCmd.Parameters.Add(
                    "@userid",
                    NpgsqlTypes.NpgsqlDbType.Integer
                ).Value =
                    cart.UserId.HasValue
                    ? cart.UserId.Value
                    : DBNull.Value;

                insertCmd.Parameters.AddWithValue(
                    "@productid",
                    cart.ProductId
                );

                insertCmd.Parameters.Add(
                    "@variantid",
                    NpgsqlTypes.NpgsqlDbType.Integer
                ).Value =
                    cart.VariantId.HasValue
                    ? cart.VariantId.Value
                    : DBNull.Value;

                insertCmd.Parameters.Add(
                    "@colorid",
                    NpgsqlTypes.NpgsqlDbType.Integer
                ).Value =
                    cart.ColorId.HasValue
                    ? cart.ColorId.Value
                    : DBNull.Value;

                insertCmd.Parameters.Add(
                    "@sizeid",
                    NpgsqlTypes.NpgsqlDbType.Integer
                ).Value =
                    cart.SizeId.HasValue
                    ? cart.SizeId.Value
                    : DBNull.Value;

                insertCmd.Parameters.AddWithValue(
                    "@quantity",
                    cart.Quantity
                );

                insertCmd.Parameters.AddWithValue(
                    "@ipaddress",
                    cart.UserId.HasValue
                    ? DBNull.Value
                    : cart.IpAddress ?? ""
                );

                var cartIdInserted =
                    await insertCmd
                    .ExecuteScalarAsync();

                return new OkObjectResult(
                    new
                    {
                        success = true,
                        message = "Item added to cart",
                        cartId = cartIdInserted,
                        productId = cart.ProductId,
                        variantId = cart.VariantId,
                        colorId = cart.ColorId,
                        sizeId = cart.SizeId,
                        quantity = cart.Quantity
                    });
            }
            catch (Exception ex)
            {
                return new ObjectResult(
                    new
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

        public async Task<IActionResult> GetCart(
            int? userId,
            string? ipAddress)
        {
            try
            {
                using var con =
                    new NpgsqlConnection(DbConnection);

                await con.OpenAsync();

                string query = @"
SELECT
    ac.id,
    ac.productid,
    ac.variantid,
    ac.quantity,
    p.productname,
    p.slug AS product_slug,
    COALESCE(pv.mrp, p.mrp) AS mrp,
    COALESCE(pv.saleprice, p.saleprice) AS saleprice,
    COALESCE(pv.variantimageurl, p.productimageurl) AS imageurl,
    COALESCE(ac.colorid, CAST(NULLIF(COALESCE(pv.color, p.color), '') AS INTEGER)) AS resolved_colorid,
    c.color_name,
    c.color_code,
    c.slug AS color_slug,
    COALESCE(
        ac.sizeid,
        (
            SELECT sz.id
            FROM sizes sz
            WHERE sz.id = ANY(COALESCE(pv.sizes, p.sizes, ARRAY[]::int[]))
            ORDER BY sz.id
            LIMIT 1
        )
    ) AS resolved_sizeid,
    s.size_name,
    s.slug AS size_slug,
    (ac.quantity * COALESCE(pv.saleprice, p.saleprice)) AS totalprice
FROM addcart ac
INNER JOIN products p ON p.id = ac.productid
LEFT JOIN product_variants pv ON pv.id = ac.variantid
LEFT JOIN colors c ON c.id = COALESCE(ac.colorid, CAST(NULLIF(COALESCE(pv.color, p.color), '') AS INTEGER))
LEFT JOIN sizes s ON s.id = COALESCE(
    ac.sizeid,
    (
        SELECT sz.id
        FROM sizes sz
        WHERE sz.id = ANY(COALESCE(pv.sizes, p.sizes, ARRAY[]::int[]))
        ORDER BY sz.id
        LIMIT 1
    )
)
WHERE ";

                if (userId.HasValue)
                {
                    query += " ac.userid = @userid ";
                }
                else
                {
                    query += " ac.userid IS NULL AND ac.ipaddress = @ipaddress ";
                }

                query += " ORDER BY ac.createdat DESC ";

                using var cmd =
                    new NpgsqlCommand(query, con);

                if (userId.HasValue)
                {
                    cmd.Parameters.AddWithValue(
                        "@userid",
                        userId.Value
                    );
                }
                else
                {
                    cmd.Parameters.AddWithValue(
                        "@ipaddress",
                        ipAddress ?? ""
                    );
                }

                var cartItems =
                    new List<object>();

                decimal grandTotal = 0;

                using var reader =
                    await cmd.ExecuteReaderAsync();

                while (await reader.ReadAsync())
                {
                    decimal itemTotal =
                        Convert.ToDecimal(
                            reader["totalprice"]
                        );

                    grandTotal += itemTotal;

                    cartItems.Add(new
                    {
                        CartId = Convert.ToInt32(reader["id"]),
                        ProductId = Convert.ToInt32(reader["productid"]),
                        VariantId = reader["variantid"] == DBNull.Value
                            ? (int?)null
                            : Convert.ToInt32(reader["variantid"]),
                        ProductName = reader["productname"]?.ToString(),
                        Slug = reader["product_slug"]?.ToString(),
                        Quantity = Convert.ToInt32(reader["quantity"]),
                        MRP = Convert.ToDecimal(reader["mrp"]),
                        SalePrice = Convert.ToDecimal(reader["saleprice"]),
                        TotalPrice = itemTotal,
                        ImageUrl = reader["imageurl"]?.ToString(),
                        ColorId = reader["resolved_colorid"] == DBNull.Value
                            ? (int?)null
                            : Convert.ToInt32(reader["resolved_colorid"]),
                        ColorName = reader["color_name"] == DBNull.Value
                            ? null
                            : reader["color_name"].ToString(),
                        ColorCode = reader["color_code"] == DBNull.Value
                            ? null
                            : reader["color_code"].ToString(),
                        ColorSlug = reader["color_slug"] == DBNull.Value
                            ? null
                            : reader["color_slug"].ToString(),
                        SizeId = reader["resolved_sizeid"] == DBNull.Value
                            ? (int?)null
                            : Convert.ToInt32(reader["resolved_sizeid"]),
                        SizeName = reader["size_name"] == DBNull.Value
                            ? null
                            : reader["size_name"].ToString(),
                        SizeSlug = reader["size_slug"] == DBNull.Value
                            ? null
                            : reader["size_slug"].ToString()
                    });
                }

                return new OkObjectResult(new
                {
                    success = true,
                    totalItems = cartItems.Count,
                    grandTotal,
                    data = cartItems
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

        // Implementation
        public async Task<IActionResult> UpdateCartQuantity(
            UpdateCartQuantityModel model)
        {
            try
            {
                using var con =
                    new NpgsqlConnection(DbConnection);

                await con.OpenAsync();

                // ====================================
                // Ownership + Current Quantity Fetch
                // ====================================

                string ownerCheckQuery;

                if (model.UserId.HasValue)
                {
                    ownerCheckQuery = @"
SELECT quantity
FROM addcart
WHERE
    id      = @cartid
    AND userid = @userid
LIMIT 1";
                }
                else
                {
                    ownerCheckQuery = @"
SELECT quantity
FROM addcart
WHERE
    id        = @cartid
    AND ipaddress = @ipaddress
LIMIT 1";
                }

                using var ownerCmd =
                    new NpgsqlCommand(
                        ownerCheckQuery,
                        con
                    );

                ownerCmd.Parameters.AddWithValue(
                    "@cartid",
                    model.CartId
                );

                if (model.UserId.HasValue)
                {
                    ownerCmd.Parameters.AddWithValue(
                        "@userid",
                        model.UserId.Value
                    );
                }
                else
                {
                    ownerCmd.Parameters.AddWithValue(
                        "@ipaddress",
                        model.IpAddress ?? ""
                    );
                }

                var currentQtyObj =
                    await ownerCmd
                    .ExecuteScalarAsync();

                // ====================================
                // Cart row mila hi nahi
                // ====================================

                if (currentQtyObj == null)
                {
                    return new NotFoundObjectResult(
                        new
                        {
                            success = false,
                            message =
                                "Cart item not found " +
                                "or access denied"
                        });
                }

                // ====================================
                // Final Quantity Calculate karo
                // ====================================

                int currentQty =
                    Convert.ToInt32(currentQtyObj);

                int finalQty =
                    currentQty + model.Quantity;

                // ====================================
                // Final Quantity 0 ya kam → DELETE
                // ====================================

                if (finalQty <= 0)
                {
                    string deleteQuery = @"
DELETE FROM addcart
WHERE id = @cartid";

                    using var deleteCmd =
                        new NpgsqlCommand(
                            deleteQuery,
                            con
                        );

                    deleteCmd.Parameters.AddWithValue(
                        "@cartid",
                        model.CartId
                    );

                    await deleteCmd
                        .ExecuteNonQueryAsync();

                    return new OkObjectResult(
                        new
                        {
                            success = true,
                            message = "Item removed from cart",
                            cartId = model.CartId,
                            quantity = 0
                        });
                }

                // ====================================
                // Quantity UPDATE karo
                // ====================================

                string updateQuery = @"
UPDATE addcart
SET
    quantity  = @finalqty,
    updatedat = NOW()
WHERE id = @cartid";

                using var updateCmd =
                    new NpgsqlCommand(
                        updateQuery,
                        con
                    );

                updateCmd.Parameters.AddWithValue(
                    "@finalqty",
                    finalQty
                );

                updateCmd.Parameters.AddWithValue(
                    "@cartid",
                    model.CartId
                );

                await updateCmd
                    .ExecuteNonQueryAsync();

                return new OkObjectResult(
                    new
                    {
                        success = true,
                        message = "Cart quantity updated",
                        cartId = model.CartId,
                        quantity = finalQty  // ← sahi final value
                    });
            }
            catch (Exception ex)
            {
                return new ObjectResult(
                    new
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




        public async Task<IActionResult> DeleteCartItem(int id, int? userId, string? ipAddress)
        {
            try
            {
                using var con =
                    new NpgsqlConnection(DbConnection);
                await con.OpenAsync();

                string deleteQuery = userId.HasValue
                    ? @"DELETE FROM addcart WHERE id = @cartid AND userid = @userid"
                    : @"DELETE FROM addcart WHERE id = @cartid AND userid IS NULL AND ipaddress = @ipaddress";

                using var deleteCmd =
                    new NpgsqlCommand(deleteQuery, con);

                deleteCmd.Parameters.AddWithValue("@cartid", id);

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
                        message = "Cart item not found or access denied"
                    });
                }

                return new OkObjectResult(
                    new
                    {
                        success = true,
                        message = "Cart item deleted successfully",
                        cartId = id
                    });
            }
            catch (Exception ex)
            {
                return new ObjectResult(
                    new
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

        public async Task<IActionResult> ClearCart(int? userId, string? ipAddress)
        {
            try
            {
                using var con = new NpgsqlConnection(DbConnection);
                await con.OpenAsync();

                string query;

                if (userId.HasValue)
                {
                    query = @"
                DELETE FROM addcart
                WHERE userid = @userid";
                }
                else
                {
                    query = @"
                    DELETE FROM addcart
                    WHERE ipaddress = @ipaddress";
                }

                using var cmd = new NpgsqlCommand(query, con);

                if (userId.HasValue)
                {
                    cmd.Parameters.AddWithValue("@userid", userId.Value);
                }
                else
                {
                    cmd.Parameters.AddWithValue("@ipaddress", ipAddress ?? "");
                }

                int rows = await cmd.ExecuteNonQueryAsync();

                return new OkObjectResult(new
                {
                    success = true,
                    message = "Cart cleared successfully",
                    deletedRows = rows
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



        public async Task MergeGuestCartToUser(int userId, string ipAddress)
        {
            using var con = new NpgsqlConnection(DbConnection);
            await con.OpenAsync();
            await CartHelper.MergeGuestCartToUserAsync(con, userId, ipAddress);
        }

    }
}