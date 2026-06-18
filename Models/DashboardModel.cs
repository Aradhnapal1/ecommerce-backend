using System;
using System.Collections.Generic;

namespace Ecommerce_Backend.Models
{
    public class DashboardStatsResponse
    {
        public decimal TotalRevenue { get; set; }
        public int TotalOrders { get; set; }
        public int TotalUsers { get; set; }
        public int TotalProducts { get; set; }
        public int TotalPendingOrders { get; set; }
        public int TotalDeliveredOrders { get; set; }
        public int TotalCancelledOrders { get; set; }
        public int TotalReturnedOrders { get; set; }
        public int TotalContactEnquiries { get; set; }
        public List<RecentOrderModel> RecentOrders { get; set; } = new List<RecentOrderModel>();
    }

    public class RecentOrderModel
    {
        public int Id { get; set; }
        public string OrderNumber { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public decimal FinalAmount { get; set; }
        public string OrderStatus { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
    }
}