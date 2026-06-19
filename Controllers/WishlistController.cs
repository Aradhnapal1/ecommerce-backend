using Ecommerce_Backend.Helpers;
using Ecommerce_Backend.Models;
using Ecommerce_Backend.Models.BusinessLayer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Ecommerce_Backend.Controllers
{
    [ApiController]
    [Route("api/wishlist")]
    [AllowAnonymous]
    public class WishlistController : ControllerBase
    {
        private readonly IBusinessLayer _businessLayer;

        public WishlistController(IBusinessLayer businessLayer)
        {
            _businessLayer = businessLayer;
        }

        [HttpPost("add")]
        public async Task<IActionResult> AddWishlist([FromForm] WishlistModel wishlist)
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

            wishlist.UserId = userId;
            wishlist.IpAddress = ipAddress;

            return await _businessLayer.AddWishlist(wishlist);
        }

        [HttpGet("get")]
        public async Task<IActionResult> GetWishlist()
        {
            var userId = UserContextHelper.GetUserId(User);
            var ipAddress = userId.HasValue
                ? null
                : UserContextHelper.GetClientIp(HttpContext);

            if (!userId.HasValue && string.IsNullOrWhiteSpace(ipAddress))
            {
                return Ok(new { success = true, count = 0, data = Array.Empty<object>() });
            }

            return await _businessLayer.GetWishlist(userId, ipAddress);
        }

        [HttpDelete("delete/{id}")]
        public async Task<IActionResult> WishlistDelete(int id)
        {
            var userId = UserContextHelper.GetUserId(User);
            var ipAddress = userId.HasValue
                ? null
                : UserContextHelper.GetClientIp(HttpContext);

            return await _businessLayer.WishlistDelete(id, userId, ipAddress);
        }
    }
}
