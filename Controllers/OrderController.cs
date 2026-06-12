using Ecommerce_Backend.Models;
using Ecommerce_Backend.Models.BusinessLayer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace Ecommerce_Backend.Controllers
{
    [ApiController]
    [Route("api/orders")]
    public class OrderController : ControllerBase
    {
        private readonly IBusinessLayer _businessLayer;
        
        public OrderController(IBusinessLayer businessLayer)
        {
            _businessLayer = businessLayer; 
        }

        /// <summary>
        /// Create a new order (Requires Authentication)
        /// </summary>
        /// <param name="model">Order creation model with AddressId, PaymentMethod, and optional CouponCode</param>
        /// <returns>Order creation response with order details</returns>
        [HttpPost("create")]
        [Authorize]
        public async Task<IActionResult> CreateOrder([FromBody] CreateOrderModel model)
        {
            // Extract UserId from JWT token
            var userId = Convert.ToInt32(
                User.FindFirst(ClaimTypes.NameIdentifier)?.Value
            );

            return await _businessLayer.CreateOrder(model, userId);
        }

        /// <summary>
        /// Get all orders
        /// </summary>
        /// <returns>List of all orders</returns>
        [HttpGet("all")]        [Authorize]        public async Task<IActionResult> GetAllOrders()
        {
            try
            {
                var orders = await _businessLayer.GetAllOrders();
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

        /// <summary>
        /// Get order by ID (Requires Authentication)
        /// </summary>
        /// <param name="id">Order ID</param>
        /// <returns>Order details</returns>
        [HttpGet("{id}")]
        [Authorize]
        public async Task<IActionResult> GetOrderById(int id)
        {
            try
            {
                var order = await _businessLayer.GetOrderById(id);
                if (order == null)
                {
                    return NotFound(new
                    {
                        success = false,
                        message = "Order not found"
                    });
                }

                // Get order items
                var items = await _businessLayer.GetOrderItems(id);

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

        /// <summary>
        /// Get order items
        /// </summary>
        /// <param name="orderId">Order ID</param>
        /// <returns>List of order items</returns>
        [HttpGet("{orderId}/items")]
        [Authorize]
        public async Task<IActionResult> GetOrderItems(int orderId)
        {
            try
            {
                var items = await _businessLayer.GetOrderItems(orderId);
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

        /// <summary>
        /// Update order status
        /// </summary>
        /// <param name="id">Order ID</param>
        /// <param name="request">Request containing new status</param>
        /// <returns>Update response</returns>
        [HttpPut("{id}/status")]
        [Authorize]
        public async Task<IActionResult> UpdateOrderStatus(int id, [FromBody] UpdateOrderStatusRequest request)
        {
            return await _businessLayer.UpdateOrderStatus(id, request.Status);
        }
    }

    /// <summary>
    /// Request model for updating order status
    /// </summary>
    public class UpdateOrderStatusRequest
    {
        public string Status { get; set; }
    }
}
