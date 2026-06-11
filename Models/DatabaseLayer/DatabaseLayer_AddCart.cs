using Microsoft.AspNetCore.Mvc;
using Npgsql;
using System.Data.Common;

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
        Task<IActionResult> UpdateCartQuantity(
    UpdateCartQuantityModel model
);
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

                // ====================================
                // Variant -> Product Mapping
                // ====================================

                if (cart.VariantId.HasValue)
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
                        cart.VariantId.Value
                    );

                    var productId =
                        await variantCmd
                        .ExecuteScalarAsync();

                    if (productId == null)
                    {
                        return new BadRequestObjectResult(
                            new
                            {
                                success = false,
                                message = "Variant not found"
                            });
                    }

                    cart.ProductId =
                        Convert.ToInt32(
                            productId
                        );
                }

                // ====================================
                // Guest Cart -> User Cart Migration
                // ====================================

                if (cart.UserId.HasValue &&
                    !string.IsNullOrWhiteSpace(
                        cart.IpAddress
                    ))
                {
                    string migrateQuery = @"
UPDATE addcart
SET
    userid = @userid,
    ipaddress = NULL,
    updatedat = NOW()
WHERE
    userid IS NULL
    AND ipaddress = @ipaddress";

                    using var migrateCmd =
                        new NpgsqlCommand(
                            migrateQuery,
                            con
                        );

                    migrateCmd.Parameters.AddWithValue(
                        "@userid",
                        cart.UserId.Value
                    );

                    migrateCmd.Parameters.AddWithValue(
                        "@ipaddress",
                        cart.IpAddress
                    );

                    await migrateCmd
                        .ExecuteNonQueryAsync();
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
                            message =
                                "Cart quantity updated"
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
                        message =
                            "Item added to cart",
                        cartId =
                            cartIdInserted
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
    p.slug,

    COALESCE(
        pv.mrp,
        p.mrp
    ) AS mrp,

    COALESCE(
        pv.saleprice,
        p.saleprice
    ) AS saleprice,

    COALESCE(
        pv.variantimageurl,
        p.productimageurl
    ) AS imageurl,

    c.id AS colorid,
    c.color_name,
    c.color_code,

    s.id AS sizeid,
    s.size_name,

    (
        ac.quantity
        *
        COALESCE(
            pv.saleprice,
            p.saleprice
        )
    ) AS totalprice

FROM addcart ac

INNER JOIN products p
ON p.id = ac.productid

LEFT JOIN product_variants pv
ON pv.id = ac.variantid

LEFT JOIN colors c
ON c.id = CAST(
    COALESCE(
        pv.color,
        p.color
    ) AS INTEGER
)

LEFT JOIN sizes s
ON s.id = CAST(
    COALESCE(
        pv.sizes[1],
        p.sizes[1]
    ) AS INTEGER
)

WHERE ";

                if (userId.HasValue)
                {
                    query += " ac.userid = @userid ";
                }
                else
                {
                    query += " ac.ipaddress = @ipaddress ";
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
                        CartId =
        Convert.ToInt32(
            reader["id"]
        ),

                        ProductId =
        Convert.ToInt32(
            reader["productid"]
        ),

                        VariantId =
        reader["variantid"] == DBNull.Value
        ? (int?)null
        : Convert.ToInt32(
            reader["variantid"]
        ),

                        ProductName =
        reader["productname"]
        ?.ToString(),

                        Slug =
        reader["slug"]
        ?.ToString(),

                        Quantity =
        Convert.ToInt32(
            reader["quantity"]
        ),

                        MRP =
        Convert.ToDecimal(
            reader["mrp"]
        ),

                        SalePrice =
        Convert.ToDecimal(
            reader["saleprice"]
        ),

                        TotalPrice =
        itemTotal,

                        ImageUrl =
        reader["imageurl"]
        ?.ToString(),

                        ColorId =
        reader["colorid"] == DBNull.Value
        ? (int?)null
        : Convert.ToInt32(
            reader["colorid"]
        ),

                        ColorName =
        reader["color_name"] == DBNull.Value
        ? null
        : reader["color_name"]
        .ToString(),

                        ColorCode =
        reader["color_code"] == DBNull.Value
        ? null
        : reader["color_code"]
        .ToString(),

                        SizeId =
        reader["sizeid"] == DBNull.Value
        ? (int?)null
        : Convert.ToInt32(
            reader["sizeid"]
        ),

                        SizeName =
        reader["size_name"] == DBNull.Value
        ? null
        : reader["size_name"]
        .ToString()
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

        public async Task<IActionResult> UpdateCartQuantity(
            UpdateCartQuantityModel model)
        {
            try
            {
                using var con =
                    new NpgsqlConnection(DbConnection);

                await con.OpenAsync();

                string checkQuery = @"
SELECT quantity
FROM addcart
WHERE id = @id";

                using var checkCmd =
                    new NpgsqlCommand(
                        checkQuery,
                        con
                    );

                checkCmd.Parameters.AddWithValue(
                    "@id",
                    model.CartId
                );

                var qtyObj =
                    await checkCmd.ExecuteScalarAsync();

                if (qtyObj == null)
                {
                    return new NotFoundObjectResult(new
                    {
                        success = false,
                        cartId = model.CartId,
                        message = "Cart item not found"
                    });
                }

                int currentQty =
                    Convert.ToInt32(
                        qtyObj
                    );

                int newQty =
                    currentQty + model.Action;

                if (newQty <= 0)
                {
                    string deleteQuery = @"
DELETE FROM addcart
WHERE id = @id";

                    using var deleteCmd =
                        new NpgsqlCommand(
                            deleteQuery,
                            con
                        );

                    deleteCmd.Parameters.AddWithValue(
                        "@id",
                        model.CartId
                    );

                    await deleteCmd.ExecuteNonQueryAsync();

                    return new OkObjectResult(new
                    {
                        success = true,
                        deleted = true,
                        quantity = 0,
                        message = "Item removed from cart"
                    });
                }

                string updateQuery = @"
UPDATE addcart
SET
    quantity = @qty,
    updatedat = NOW()
WHERE id = @id
RETURNING quantity";

                using var updateCmd =
                    new NpgsqlCommand(
                        updateQuery,
                        con
                    );

                updateCmd.Parameters.AddWithValue(
                    "@qty",
                    newQty
                );

                updateCmd.Parameters.AddWithValue(
                    "@id",
                    model.CartId
                );

                var updatedQty =
                    await updateCmd.ExecuteScalarAsync();

                return new OkObjectResult(new
                {
                    success = true,
                    deleted = false,
                    cartId = model.CartId,
                    quantity = Convert.ToInt32(updatedQty),
                    message =
                        model.Action > 0
                        ? "Quantity increased"
                        : "Quantity decreased"
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
    }
}
   
