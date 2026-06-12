using Ecommerce_Backend.Helpers;
using Ecommerce_Backend.Models;
using Ecommerce_Backend.Models.BusinessLayer;
using Microsoft.AspNetCore.Mvc;

namespace Ecommerce_Backend.Controllers
{
    [ApiController]
    [Route("api/wishlist")]
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
            var currentIp = UserContextHelper.GetClientIp(HttpContext);
            var userId = UserContextHelper.GetUserId(User);

            wishlist.UserId = userId;
            wishlist.IpAddress = currentIp;

            return await _businessLayer.AddWishlist(wishlist);
        }

        [HttpGet("get")]
        public async Task<IActionResult> GetWishlist()
        {
            var userId = UserContextHelper.GetUserId(User);
            var ipAddress = userId.HasValue ? null : UserContextHelper.GetClientIp(HttpContext);

            return await _businessLayer.GetWishlist(userId, ipAddress);
        }

        [HttpDelete("delete/{id}")]
        public async Task<IActionResult> WishlistDelete(int id)
        {
            var userId = UserContextHelper.GetUserId(User);
            var ipAddress = userId.HasValue ? null : UserContextHelper.GetClientIp(HttpContext);

            return await _businessLayer.WishlistDelete(id, userId, ipAddress);
        }
    }
}
