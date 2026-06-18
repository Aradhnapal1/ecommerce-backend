using Microsoft.AspNetCore.Mvc;
using Npgsql;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Ecommerce_Backend.Models;

namespace Ecommerce_Backend.Models.DatabaseLayer
{
    public partial interface IDatabaseLayer
    {
        Task<IActionResult> GetAdminDashboardStats();
    }

    public partial class DataBaseLayer
    {
        public async Task<IActionResult> GetAdminDashboardStats()
        {
            try
            {
                var stats = new DashboardStatsResponse();

                using var con = new NpgsqlConnection(DbConnection);
                await con.OpenAsync();

                // 1. Total Revenue (Exclude Cancelled and Returned Orders)
                var revenueQuery = "SELECT COALESCE(SUM(final_amount), 0) FROM orders WHERE order_status NOT IN ('CANCELLED', 'RETURNED', 'RETURN_REQUESTED')";
                using var cmdRevenue = new NpgsqlCommand(revenueQuery, con);
                stats.TotalRevenue = Convert.ToDecimal(await cmdRevenue.ExecuteScalarAsync());

                // 2. Total Orders
                var ordersQuery = "SELECT COUNT(*) FROM orders";
                using var cmdOrders = new NpgsqlCommand(ordersQuery, con);
                stats.TotalOrders = Convert.ToInt32(await cmdOrders.ExecuteScalarAsync());

                // 3. Total Users (Only customers, excluding Admins)
                var usersQuery = "SELECT COUNT(*) FROM user_register WHERE role = 'USER'";
                using var cmdUsers = new NpgsqlCommand(usersQuery, con);
                stats.TotalUsers = Convert.ToInt32(await cmdUsers.ExecuteScalarAsync());

                // 4. Total Products
                var productsQuery = "SELECT COUNT(*) FROM products";
                using var cmdProducts = new NpgsqlCommand(productsQuery, con);
                stats.TotalProducts = Convert.ToInt32(await cmdProducts.ExecuteScalarAsync());

                // 5. Orders Stats based on status
                var statusQuery = "SELECT order_status, COUNT(*) FROM orders GROUP BY order_status";
                using var cmdStatus = new NpgsqlCommand(statusQuery, con);
                using var statusReader = await cmdStatus.ExecuteReaderAsync();
                while (await statusReader.ReadAsync())
                {
                    var status = statusReader.GetString(0).ToUpper();
                    var count = statusReader.GetInt32(1);

                    if (status == "PLACED" || status == "PENDING" || status == "PROCESSING" || status == "SHIPPED") 
                        stats.TotalPendingOrders += count;
                    else if (status == "DELIVERED") 
                        stats.TotalDeliveredOrders += count;
                else if (status == "CANCELLED") 
                        stats.TotalCancelledOrders += count;
                else if (status == "RETURNED" || status == "RETURN_REQUESTED" || status == "RETURN_REJECTED") 
                    stats.TotalReturnedOrders += count;
                }
                await statusReader.CloseAsync();

                // 6. Recent 5 Orders
                var recentOrdersQuery = @"
                    SELECT id, order_number, full_name, final_amount, order_status, createdat 
                    FROM orders 
                    ORDER BY createdat DESC 
                    LIMIT 5";
                using var cmdRecent = new NpgsqlCommand(recentOrdersQuery, con);
                using var reader = await cmdRecent.ExecuteReaderAsync();
                
                while (await reader.ReadAsync())
                {
                    stats.RecentOrders.Add(new RecentOrderModel
                    {
                        Id = (int)reader["id"],
                        OrderNumber = reader["order_number"].ToString()!,
                        FullName = reader["full_name"].ToString()!,
                        FinalAmount = (decimal)reader["final_amount"],
                        OrderStatus = reader["order_status"].ToString()!,
                        CreatedAt = (DateTime)reader["createdat"]
                    });
                }
                
                await reader.CloseAsync(); // <--- This line fixes the error

                // 7. Total Contact Enquiries
                var contactsQuery = "SELECT COUNT(*) FROM contacts";
                using var cmdContacts = new NpgsqlCommand(contactsQuery, con);
                stats.TotalContactEnquiries = Convert.ToInt32(await cmdContacts.ExecuteScalarAsync());

                return new OkObjectResult(new
                {
                    success = true,
                    message = "Dashboard stats fetched successfully.",
                    data = stats
                });
            }
            catch (Exception ex)
            {
                return new ObjectResult(new
                {
                    success = false,
                    message = $"Error fetching dashboard stats: {ex.Message}"
                }) { StatusCode = 500 };
            }
        }
    }
}