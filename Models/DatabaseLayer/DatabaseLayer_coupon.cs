using Microsoft.AspNetCore.Mvc;
using Npgsql;

namespace Ecommerce_Backend.Models.DatabaseLayer
{
    public partial interface IDatabaseLayer
    {
        Task<IActionResult> GetCoupons();
        Task<IActionResult> AddCoupon([FromBody] CouponModel coupon);
        Task<IActionResult> EditCoupun(int id, [FromBody] CouponModel coupon);
        Task<IActionResult> DeleteCoupon(int id);
        Task<IActionResult> ApplyCoupon(ApplyCouponModel model, int? userId, string? ipAddress);

    }


    public partial class DataBaseLayer : IDatabaseLayer
    {
        public async Task<IActionResult> GetCoupons()
        {
            try
            {
                var coupons = new List<CouponModel>();

                using var connection = new NpgsqlConnection(DbConnection);
                await connection.OpenAsync();

                var command = new NpgsqlCommand(@"
    SELECT
        id,
        coupon_code,
        coupon_type,
        coupon_value,
        min_order_amount,
        usage_limit,
        start_date,
        end_date,
        is_active,
        created_at
    FROM coupons
    ORDER BY id DESC
", connection);

                using var reader = await command.ExecuteReaderAsync();

                while (await reader.ReadAsync())
                {
                    coupons.Add(new CouponModel
                    {
                        Id = Convert.ToInt32(reader["id"]),

                        CouponCode = reader["coupon_code"]?.ToString(),

                        CouponType = reader["coupon_type"]?.ToString(),

                        DiscountAmount = Convert.ToDecimal(reader["coupon_value"]),

                        MinimumPurchaseAmount = Convert.ToDecimal(reader["min_order_amount"]),


                        UsageLimit = Convert.ToInt32(reader["usage_limit"]),

                        StartDate = Convert.ToDateTime(reader["start_date"]),

                        ExpiryDate = Convert.ToDateTime(reader["end_date"]),

                        IsActive = Convert.ToBoolean(reader["is_active"]),

                        CreatedAt = Convert.ToDateTime(reader["created_at"])
                    });
                }

                return new OkObjectResult(coupons);
            }
            catch (Exception ex)
            {
                return new BadRequestObjectResult(ex.Message);
            }
        }

        public async Task<IActionResult> AddCoupon([FromBody] CouponModel coupon)
        {
            try
            {
                using var connection = new NpgsqlConnection(DbConnection);
                await connection.OpenAsync();

                // Check if coupon already exists
                var checkCommand = new NpgsqlCommand(
                    "SELECT COUNT(*) FROM coupons WHERE coupon_code = @coupon_code",
                    connection);

                checkCommand.Parameters.AddWithValue("@coupon_code", coupon.CouponCode);

                var exists = Convert.ToInt32(await checkCommand.ExecuteScalarAsync());

                if (exists > 0)
                {
                    return new BadRequestObjectResult(new
                    {
                        success = false,
                        message = "Coupon code already exists."
                    });
                }

                // Insert coupon
                var command = new NpgsqlCommand(@"
            INSERT INTO coupons
            (
                coupon_code,
                coupon_type,
                coupon_value,
                min_order_amount,
                usage_limit,
                start_date,
                end_date,
                is_active
            )
            VALUES
            (
                @coupon_code,
                @coupon_type,
                @coupon_value,
                @min_order_amount,
                @usage_limit,
                @start_date,
                @end_date,
                @is_active
            )
            RETURNING id;
        ", connection);

                command.Parameters.AddWithValue("@coupon_code", coupon.CouponCode);
                command.Parameters.AddWithValue("@coupon_type", coupon.CouponType);
                command.Parameters.AddWithValue("@coupon_value", coupon.DiscountAmount);
                command.Parameters.AddWithValue("@min_order_amount", coupon.MinimumPurchaseAmount);
                command.Parameters.AddWithValue("@usage_limit", coupon.UsageLimit);
                command.Parameters.AddWithValue("@start_date", coupon.StartDate);
                command.Parameters.AddWithValue("@end_date", coupon.ExpiryDate);
                command.Parameters.AddWithValue("@is_active", coupon.IsActive);

                var couponId = Convert.ToInt32(await command.ExecuteScalarAsync());

                return new OkObjectResult(new
                {
                    success = true,
                    message = "Coupon added successfully.",
                    couponId
                });
            }
            catch (Exception ex)
            {
                return new BadRequestObjectResult(new
                {
                    success = false,
                    message = ex.Message
                });
            }


        }

        public async Task<IActionResult> EditCoupun(int id, CouponModel coupon)
        {
            try
            {
                using var connection = new NpgsqlConnection(DbConnection);
                await connection.OpenAsync();

                // Check record exists
                var existsCommand = new NpgsqlCommand(
                    "SELECT COUNT(*) FROM coupons WHERE id = @id",
                    connection);

                existsCommand.Parameters.AddWithValue("@id", id);

                var exists = Convert.ToInt32(await existsCommand.ExecuteScalarAsync());

                if (exists == 0)
                {
                    return new NotFoundObjectResult(new
                    {
                        success = false,
                        message = $"Coupon not found with id {id}"
                    });
                }

                // Check duplicate coupon code
                var duplicateCommand = new NpgsqlCommand(@"
            SELECT COUNT(*)
            FROM coupons
            WHERE coupon_code = @coupon_code
            AND id <> @id",
                    connection);

                duplicateCommand.Parameters.AddWithValue("@coupon_code", coupon.CouponCode);
                duplicateCommand.Parameters.AddWithValue("@id", id);

                var duplicateExists =
                    Convert.ToInt32(await duplicateCommand.ExecuteScalarAsync());

                if (duplicateExists > 0)
                {
                    return new BadRequestObjectResult(new
                    {
                        success = false,
                        message = "Coupon code already exists."
                    });
                }

                coupon.CouponType = coupon.CouponType?.Trim().ToUpper();

                if (coupon.CouponType != "FLAT" &&
                    coupon.CouponType != "PERCENTAGE")
                {
                    return new BadRequestObjectResult(new
                    {
                        success = false,
                        message = "CouponType must be FLAT or PERCENTAGE."
                    });
                }

                var command = new NpgsqlCommand(@"
            UPDATE coupons
            SET
                coupon_code = @coupon_code,
                coupon_type = @coupon_type,
                coupon_value = @coupon_value,
                min_order_amount = @min_order_amount,
                usage_limit = @usage_limit,
                start_date = @start_date,
                end_date = @end_date,
                is_active = @is_active
            WHERE id = @id",
                    connection);

                command.Parameters.AddWithValue("@id", id);
                command.Parameters.AddWithValue("@coupon_code", coupon.CouponCode);
                command.Parameters.AddWithValue("@coupon_type", coupon.CouponType);
                command.Parameters.AddWithValue("@coupon_value", coupon.DiscountAmount);
                command.Parameters.AddWithValue("@min_order_amount", coupon.MinimumPurchaseAmount);
                command.Parameters.AddWithValue("@usage_limit", coupon.UsageLimit);
                command.Parameters.AddWithValue("@start_date", coupon.StartDate);
                command.Parameters.AddWithValue("@end_date", coupon.ExpiryDate);
                command.Parameters.AddWithValue("@is_active", coupon.IsActive);

                var rowsAffected = await command.ExecuteNonQueryAsync();

                if (rowsAffected == 0)
                {
                    return new BadRequestObjectResult(new
                    {
                        success = false,
                        message = "No record updated."
                    });
                }

                return new OkObjectResult(new
                {
                    success = true,
                    message = "Coupon updated successfully."
                });
            }
            catch (Exception ex)
            {
                return new BadRequestObjectResult(new
                {
                    success = false,
                    message = ex.Message,
                    innerException = ex.InnerException?.Message
                });
            }
        }

        public async Task<IActionResult> DeleteCoupon(int id)
        {
            try
            {
                using var connection = new NpgsqlConnection(DbConnection);
                await connection.OpenAsync();
                var command = new NpgsqlCommand(
                    "DELETE FROM coupons WHERE id = @id",
                    connection);
                command.Parameters.AddWithValue("@id", id);
                var rowsAffected = await command.ExecuteNonQueryAsync();
                if (rowsAffected == 0)
                {
                    return new NotFoundObjectResult(new
                    {
                        success = false,
                        message = $"Coupon not found with id {id}"
                    });
                }
                return new OkObjectResult(new
                {
                    success = true,
                    message = "Coupon deleted successfully."
                });
            }
            catch (Exception ex)
            {
                return new BadRequestObjectResult(new
                {
                    success = false,
                    message = ex.Message,
                    innerException = ex.InnerException?.Message
                });
            }
        }

        public async Task<IActionResult> ApplyCoupon(
    ApplyCouponModel model,
    int? userId,
    string? ipAddress)
        {
            try
            {
                using var con =
                    new NpgsqlConnection(
                        DbConnection
                    );

                await con.OpenAsync();

                // ==========================
                // Cart Grand Total
                // ==========================

                string cartQuery;

                if (userId.HasValue)
                {
                    cartQuery = @"
SELECT
COALESCE(
SUM(
    ac.quantity *
    COALESCE(
        pv.saleprice,
        p.saleprice
    )
),0)
FROM addcart ac
INNER JOIN products p
ON p.id = ac.productid
LEFT JOIN product_variants pv
ON pv.id = ac.variantid
WHERE ac.userid = @userid";
                }
                else
                {
                    cartQuery = @"
SELECT
COALESCE(
SUM(
    ac.quantity *
    COALESCE(
        pv.saleprice,
        p.saleprice
    )
),0)
FROM addcart ac
INNER JOIN products p
ON p.id = ac.productid
LEFT JOIN product_variants pv
ON pv.id = ac.variantid
WHERE ac.ipaddress = @ipaddress";
                }

                using var cartCmd =
                    new NpgsqlCommand(
                        cartQuery,
                        con
                    );

                if (userId.HasValue)
                {
                    cartCmd.Parameters.AddWithValue(
                        "@userid",
                        userId.Value
                    );
                }
                else
                {
                    cartCmd.Parameters.AddWithValue(
                        "@ipaddress",
                        ipAddress ?? ""
                    );
                }

                decimal grandTotal =
                    Convert.ToDecimal(
                        await cartCmd.ExecuteScalarAsync()
                    );

                if (grandTotal <= 0)
                {
                    return new BadRequestObjectResult(new
                    {
                        success = false,
                        message = "Cart is empty"
                    });
                }

                // ==========================
                // Coupon Check
                // ==========================

                string couponQuery = @"
SELECT *
FROM coupons
WHERE
coupon_code = @couponcode
AND is_active = true
AND NOW()
BETWEEN start_date
AND end_date";

                using var couponCmd =
                    new NpgsqlCommand(
                        couponQuery,
                        con
                    );

                couponCmd.Parameters.AddWithValue(
                    "@couponcode",
                    model.CouponCode.Trim()
                );

                using var reader =
                    await couponCmd.ExecuteReaderAsync();

                if (!await reader.ReadAsync())
                {
                    return new BadRequestObjectResult(new
                    {
                        success = false,
                        message = "Invalid Coupon Code"
                    });
                }

                int couponId =
                    Convert.ToInt32(
                        reader["id"]
                    );

                string couponType =
                    reader["coupon_type"]
                    .ToString();

                decimal couponValue =
                    Convert.ToDecimal(
                        reader["coupon_value"]
                    );

                decimal minOrderAmount =
                    Convert.ToDecimal(
                        reader["min_order_amount"]
                    );

                int usageLimit =
                    Convert.ToInt32(
                        reader["usage_limit"]
                    );

                await reader.CloseAsync();

                // ==========================
                // Minimum Amount Check
                // ==========================

                if (grandTotal < minOrderAmount)
                {
                    return new BadRequestObjectResult(new
                    {
                        success = false,
                        message =
                        $"Minimum order amount should be ₹{minOrderAmount}"
                    });
                }

                // ==========================
                // Usage Check
                // ==========================

                if (userId.HasValue)
                {
                    string usageQuery = @"
SELECT COUNT(*)
FROM coupon_usage
WHERE
couponid = @couponid
AND userid = @userid";

                    using var usageCmd =
                        new NpgsqlCommand(
                            usageQuery,
                            con
                        );

                    usageCmd.Parameters.AddWithValue(
                        "@couponid",
                        couponId
                    );

                    usageCmd.Parameters.AddWithValue(
                        "@userid",
                        userId.Value
                    );

                    int usedCount =
                        Convert.ToInt32(
                            await usageCmd.ExecuteScalarAsync()
                        );

                    if (usedCount >= usageLimit)
                    {
                        return new BadRequestObjectResult(new
                        {
                            success = false,
                            message = "Coupon already used"
                        });
                    }
                }

                // ==========================
                // Discount Calculation
                // ==========================

                decimal discountAmount = 0;

                if (couponType == "FLAT")
                {
                    discountAmount =
                        couponValue;
                }
                else
                {
                    discountAmount =
                        (grandTotal * couponValue)
                        / 100;
                }

                decimal finalAmount =
                    grandTotal -
                    discountAmount;

                if (finalAmount < 0)
                {
                    finalAmount = 0;
                }

                return new OkObjectResult(new
                {
                    success = true,

                    couponId,

                    couponCode =
                        model.CouponCode,

                    couponType,

                    couponValue,

                    grandTotal,

                    discountAmount,

                    finalAmount
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
