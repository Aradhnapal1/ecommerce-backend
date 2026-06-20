using Ecommerce_Backend.Helpers;
using Ecommerce_Backend.Models;
using Ecommerce_Backend.Models.BusinessLayer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Ecommerce_Backend.Controllers
{
    [ApiController]
    [Route("api/orders")]
    [Authorize]
    public class OrderController : ControllerBase
    {
        private readonly IBusinessLayer _businessLayer;
        
        public OrderController(IBusinessLayer businessLayer)
        {
            _businessLayer = businessLayer; 
        }

        [HttpPost("create")]
        public async Task<IActionResult> CreateOrder([FromBody] CreateOrderModel model)
        {
            var userId = UserContextHelper.GetUserId(User)!.Value;
            return await _businessLayer.CreateOrder(model, userId);
        }

        [HttpPost("buy-now")]
        public async Task<IActionResult> BuyNow([FromBody] BuyNowModel model)
        {
            var userId = UserContextHelper.GetUserId(User)!.Value;
            return await _businessLayer.BuyNow(model, userId);
        }

        [HttpPost("verify-payment")]
        public async Task<IActionResult> VerifyPayment([FromBody] VerifyPaymentModel model)
        {
            var userId = UserContextHelper.GetUserId(User)!.Value;
            return await _businessLayer.VerifyOnlinePayment(model, userId);
        }

        [HttpPost("initiate-payment/{orderId:int}")]
        public async Task<IActionResult> InitiatePayment(int orderId)
        {
            var userId = UserContextHelper.GetUserId(User)!.Value;
            return await _businessLayer.InitiateOnlinePayment(orderId, userId);
        }

        [HttpGet("all")]
        [Authorize]
        public async Task<IActionResult> GetAllOrders()
        {
            try
            {
                var userId = UserContextHelper.GetUserId(User)!.Value;
                var isAdmin = UserContextHelper.IsAdmin(User);

                var orders = await _businessLayer.GetAllOrders(userId, isAdmin);
                return Ok(new
                {
                    success = true,
                    data = orders,
                    count = orders.Count
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    success = false,
                    message = ex.Message
                });
            }
        }

        [HttpGet("{id:int}")]
        [Authorize]
        public async Task<IActionResult> GetOrderById(int id)
        {
            try
            {
                var userId = UserContextHelper.GetUserId(User)!.Value;
                var isAdmin = UserContextHelper.IsAdmin(User);

                var order = await _businessLayer.GetOrderById(id, userId, isAdmin);
                if (order == null)
                {
                    return NotFound(new
                    {
                        success = false,
                        message = "Order not found"
                    });
                }

                var items = await _businessLayer.GetOrderItems(id, userId, isAdmin);

                return Ok(new
                {
                    success = true,
                    data = new
                    {
                        order = order,
                        items = items
                    }
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    success = false,
                    message = ex.Message
                });
            }
        }

        [HttpGet("{orderId:int}/items")]
        public async Task<IActionResult> GetOrderItems(int orderId)
        {
            try
            {
                var userId = UserContextHelper.GetUserId(User)!.Value;
                var isAdmin = UserContextHelper.IsAdmin(User);

                var items = await _businessLayer.GetOrderItems(orderId, userId, isAdmin);
                if (items == null)
                {
                    return NotFound(new
                    {
                        success = false,
                        message = "Order not found"
                    });
                }

                return Ok(new
                {
                    success = true,
                    data = items,
                    count = items.Count
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    success = false,
                    message = ex.Message
                });
            }
        }

        [HttpPut("{id:int}/status")]
        [Authorize(Roles = AuthRoles.Admin)]
        public async Task<IActionResult> UpdateOrderStatus(int id, [FromBody] UpdateOrderStatusRequest request)
        {
            return await _businessLayer.UpdateOrderStatus(id, request.Status);
        }

        [HttpPut("{id:int}/payment-status")]
        [Authorize(Roles = AuthRoles.Admin)]
        public async Task<IActionResult> UpdatePaymentStatus(int id, [FromBody] UpdatePaymentStatusRequest request)
        {
            return await _businessLayer.UpdatePaymentStatus(id, request.PaymentStatus);
        }

        [HttpPut("{id:int}/cancel")]
        public async Task<IActionResult> CancelOrder(int id)
        {
            var userId = UserContextHelper.GetUserId(User)!.Value;
            return await _businessLayer.CancelOrder(id, userId);
        }

        [HttpPost("{id:int}/return")]
        public async Task<IActionResult> RequestReturn(int id, [FromBody] ReturnOrderRequest request)
        {
            var userId = UserContextHelper.GetUserId(User)!.Value;
            return await _businessLayer.RequestReturn(id, userId, request.Reason);
        }

        [HttpPut("{id:int}/process-refund")]
        [Authorize(Roles = AuthRoles.Admin)]
        public async Task<IActionResult> ProcessRefund(int id, [FromBody] ProcessRefundRequest request)
        {
            return await _businessLayer.ProcessRefund(id, request.Action);
        }
    }

    public class UpdateOrderStatusRequest
    {
        public string Status { get; set; } = string.Empty;
    }

    public class UpdatePaymentStatusRequest
    {
        public string PaymentStatus { get; set; } = string.Empty;
    }

    public class ReturnOrderRequest
    {
        public string Reason { get; set; } = string.Empty;
    }

    public class ProcessRefundRequest
    {
        public string Action { get; set; } = string.Empty; // "APPROVE" or "REJECT"
    }
}
