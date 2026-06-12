using Microsoft.AspNetCore.Mvc;

namespace Ecommerce_Backend.Models.BusinessLayer
{
   public partial interface IBusinessLayer
    {
        Task<IActionResult> CreateOrder(CreateOrderModel model, int userId);
        Task<List<OrderDetailsModel>> GetAllOrders(int userId, bool isAdmin);
        Task<OrderDetailsModel?> GetOrderById(int orderId, int userId, bool isAdmin);
        Task<IActionResult> UpdateOrderStatus(int orderId, string status);
        Task<List<OrderItemModel>?> GetOrderItems(int orderId, int userId, bool isAdmin);
    }

    public partial class BusinessLayer : IBusinessLayer
    {
        public async Task<IActionResult> CreateOrder(CreateOrderModel model, int userId)
        {
            if (userId <= 0)
            {
                return new BadRequestObjectResult(new
                {
                    success = false,
                    message = "Invalid user ID"
                });
            }

            if (model.AddressId <= 0)
            {
                return new BadRequestObjectResult(new
                {
                    success = false,
                    message = "Invalid address ID"
                });
            }

            if (string.IsNullOrWhiteSpace(model.PaymentMethod))
            {
                return new BadRequestObjectResult(new
                {
                    success = false,
                    message = "Payment method is required"
                });
            }

            return await _databaseLayer.CreateOrder(model, userId);
        }

        public async Task<List<OrderDetailsModel>> GetAllOrders(int userId, bool isAdmin)
        {
            return await _databaseLayer.GetAllOrders(userId, isAdmin);
        }

        public async Task<OrderDetailsModel?> GetOrderById(int orderId, int userId, bool isAdmin)
        {
            return await _databaseLayer.GetOrderById(orderId, userId, isAdmin);
        }

        public async Task<IActionResult> UpdateOrderStatus(int orderId, string status)
        {
            if (orderId <= 0)
            {
                return new BadRequestObjectResult(new
                {
                    success = false,
                    message = "Invalid order ID"
                });
            }

            if (string.IsNullOrWhiteSpace(status))
            {
                return new BadRequestObjectResult(new
                {
                    success = false,
                    message = "Order status is required"
                });
            }

            return await _databaseLayer.UpdateOrderStatus(orderId, status);
        }

        public async Task<List<OrderItemModel>?> GetOrderItems(int orderId, int userId, bool isAdmin)
        {
            var order = await _databaseLayer.GetOrderById(orderId, userId, isAdmin);
            if (order == null)
                return null;

            return await _databaseLayer.GetOrderItems(orderId);
        }
    }
}
