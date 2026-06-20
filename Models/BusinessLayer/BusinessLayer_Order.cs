using Microsoft.AspNetCore.Mvc;

namespace Ecommerce_Backend.Models.BusinessLayer
{
   public partial interface IBusinessLayer
    {
        Task<IActionResult> CreateOrder(CreateOrderModel model, int userId);
        Task<IActionResult> BuyNow(BuyNowModel model, int userId);
        Task<IActionResult> VerifyOnlinePayment(VerifyPaymentModel model, int userId);
        Task<IActionResult> InitiateOnlinePayment(int orderId, int userId);
        Task<List<OrderDetailsModel>> GetAllOrders(int userId, bool isAdmin);
        Task<OrderDetailsModel?> GetOrderById(int orderId, int userId, bool isAdmin);
        Task<IActionResult> UpdateOrderStatus(int orderId, string status);
        Task<IActionResult> UpdatePaymentStatus(int orderId, string paymentStatus);
        Task<IActionResult> CancelOrder(int orderId, int userId);
        Task<IActionResult> RequestReturn(int orderId, int userId, string reason);
        Task<IActionResult> ProcessRefund(int orderId, string action);
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

        public async Task<IActionResult> BuyNow(BuyNowModel model, int userId)
        {
            if (userId <= 0)
                return new BadRequestObjectResult(new { success = false, message = "Login required for buy now." });

            if (model.AddressId <= 0)
                return new BadRequestObjectResult(new { success = false, message = "Invalid address ID" });

            if (model.ProductId <= 0)
                return new BadRequestObjectResult(new { success = false, message = "Invalid product ID" });

            if (string.IsNullOrWhiteSpace(model.PaymentMethod))
                return new BadRequestObjectResult(new { success = false, message = "Payment method is required" });

            return await _databaseLayer.BuyNow(model, userId);
        }

        public async Task<IActionResult> VerifyOnlinePayment(VerifyPaymentModel model, int userId)
        {
            if (userId <= 0)
                return new BadRequestObjectResult(new { success = false, message = "Invalid user ID" });

            if (model.OrderId <= 0)
                return new BadRequestObjectResult(new { success = false, message = "Invalid order ID" });

            if (string.IsNullOrWhiteSpace(model.RazorpayOrderId) ||
                string.IsNullOrWhiteSpace(model.RazorpayPaymentId) ||
                string.IsNullOrWhiteSpace(model.RazorpaySignature))
            {
                return new BadRequestObjectResult(new { success = false, message = "Payment details are required." });
            }

            return await _databaseLayer.VerifyOnlinePayment(model, userId);
        }

        public async Task<IActionResult> InitiateOnlinePayment(int orderId, int userId)
        {
            if (orderId <= 0)
                return new BadRequestObjectResult(new { success = false, message = "Invalid order ID" });

            if (userId <= 0)
                return new BadRequestObjectResult(new { success = false, message = "Invalid user ID" });

            return await _databaseLayer.InitiateOnlinePayment(orderId, userId);
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

        public async Task<IActionResult> UpdatePaymentStatus(int orderId, string paymentStatus)
        {
            if (orderId <= 0)
            {
                return new BadRequestObjectResult(new
                {
                    success = false,
                    message = "Invalid order ID"
                });
            }

            if (string.IsNullOrWhiteSpace(paymentStatus))
            {
                return new BadRequestObjectResult(new
                {
                    success = false,
                    message = "Payment status is required"
                });
            }

            return await _databaseLayer.UpdatePaymentStatus(orderId, paymentStatus);
        }

        public async Task<IActionResult> CancelOrder(int orderId, int userId)
        {
            if (orderId <= 0)
            {
                return new BadRequestObjectResult(new
                {
                    success = false,
                    message = "Invalid order ID"
                });
            }

            if (userId <= 0)
            {
                return new BadRequestObjectResult(new
                {
                    success = false,
                    message = "Invalid user ID"
                });
            }

            return await _databaseLayer.CancelOrder(orderId, userId);
        }

        public async Task<IActionResult> RequestReturn(int orderId, int userId, string reason)
        {
            if (orderId <= 0)
            {
                return new BadRequestObjectResult(new { success = false, message = "Invalid order ID" });
            }
            if (userId <= 0)
            {
                return new BadRequestObjectResult(new { success = false, message = "Invalid user ID" });
            }
            if (string.IsNullOrWhiteSpace(reason))
            {
                return new BadRequestObjectResult(new { success = false, message = "Return reason is required" });
            }

            return await _databaseLayer.RequestReturn(orderId, userId, reason);
        }

        public async Task<IActionResult> ProcessRefund(int orderId, string action)
        {
            if (orderId <= 0)
            {
                return new BadRequestObjectResult(new { success = false, message = "Invalid order ID" });
            }
            if (string.IsNullOrWhiteSpace(action))
            {
                return new BadRequestObjectResult(new { success = false, message = "Action is required" });
            }

            return await _databaseLayer.ProcessRefund(orderId, action);
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
