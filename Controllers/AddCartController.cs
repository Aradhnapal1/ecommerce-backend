using Ecommerce_Backend.Helpers;
using Ecommerce_Backend.Models;
using Ecommerce_Backend.Models.BusinessLayer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Ecommerce_Backend.Controllers
{
    [ApiController]
    [Route("api/addcart")]
    [AllowAnonymous]
    public class AddCartController : ControllerBase
    {
        private readonly IBusinessLayer _businessLayer;

        public AddCartController(IBusinessLayer businessLayer)
        {
            _businessLayer = businessLayer;
        }

        [HttpPost("add")]
        public async Task<IActionResult> AddCart([FromForm] AddCartModel cart)
        {
            var userId = UserContextHelper.GetUserId(User);
            var ipAddress = UserContextHelper.GetClientIp(HttpContext);

            if (!userId.HasValue && string.IsNullOrWhiteSpace(ipAddress))
            {
                return BadRequest(new
                {
                    success = false,
                    message = "Could not identify guest session. IP address is required."
                });
            }

            cart.UserId = userId;
            cart.IpAddress = ipAddress;

            return await _businessLayer.AddCart(cart);
        }

        [HttpGet("list")]
        public async Task<IActionResult> GetCart()
        {
            var userId = UserContextHelper.GetUserId(User);
            var ipAddress = userId.HasValue
                ? null
                : UserContextHelper.GetClientIp(HttpContext);

            if (!userId.HasValue && string.IsNullOrWhiteSpace(ipAddress))
            {
                return Ok(new
                {
                    success = true,
                    totalItems = 0,
                    grandTotal = 0m,
                    data = Array.Empty<object>()
                });
            }

            return await _businessLayer.GetCart(userId, ipAddress);
        }

        [HttpPut("update-quantity")]
        public async Task<IActionResult> UpdateCartQuantity([FromForm] UpdateCartQuantityModel model)
        {
            var userId = UserContextHelper.GetUserId(User);
            var ipAddress = UserContextHelper.GetClientIp(HttpContext);

            model.UserId = userId;
            model.IpAddress = userId.HasValue ? null : ipAddress;

            return await _businessLayer.UpdateCartQuantity(model);
        }

        [HttpDelete("delete/{id}")]
        public async Task<IActionResult> DeleteCartItem(int id)
        {
            var userId = UserContextHelper.GetUserId(User);
            var ipAddress = userId.HasValue
                ? null
                : UserContextHelper.GetClientIp(HttpContext);

            return await _businessLayer.DeleteCartItem(id, userId, ipAddress);
        }

        [HttpDelete("clear")]
        public async Task<IActionResult> ClearCart()
        {
            var userId = UserContextHelper.GetUserId(User);
            var ipAddress = userId.HasValue
                ? null
                : UserContextHelper.GetClientIp(HttpContext);

            return await _businessLayer.ClearCart(userId, ipAddress);
        }
    }
}
