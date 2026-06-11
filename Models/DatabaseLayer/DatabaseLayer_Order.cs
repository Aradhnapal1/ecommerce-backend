using Microsoft.AspNetCore.Mvc;
using Npgsql;

namespace Ecommerce_Backend.Models.DatabaseLayer
{
   public partial interface IDatabaseLayer
    {
        Task<IActionResult> CreateOrder(CreateOrderModel model);
    }

    public partial class DataBaseLayer : IDatabaseLayer
    {
        public async Task<IActionResult> CreateOrder(
    CreateOrderModel model)
        {
            try
            {
                using var con =
                    new NpgsqlConnection(DbConnection);

                await con.OpenAsync();

                using var transaction =
                    await con.BeginTransactionAsync();

                try
                {
                    // Step 1
                    // Address Fetch

                    // Step 2
                    // Cart Fetch

                    // Step 3
                    // Coupon Validate

                    // Step 4
                    // Calculate Totals

                    // Step 5
                    // Create Order

                    // Step 6
                    // Create Order Items

                    // Step 7
                    // Coupon Usage

                    // Step 8
                    // Stock Deduct

                    // Stepep 9
                    // Clear Cart

                    await transaction.CommitAsync();

                    return new OkObjectResult(new
                    {
                        success = true
                    });
                }
                catch
                {
                    await transaction.RollbackAsync();
                    throw;
                }
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
