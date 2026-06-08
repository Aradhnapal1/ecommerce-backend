using Microsoft.AspNetCore.Mvc;
using Npgsql;

namespace Ecommerce_Backend.Models.DatabaseLayer
{
    public partial interface IDatabaseLayer
    {
        Task<IActionResult> GetCoupons();
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
        max_discount,
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

                        MaximumDiscountAmount = reader["max_discount"] == DBNull.Value
                            ? null
                            : Convert.ToDecimal(reader["max_discount"]),

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
    }


}
