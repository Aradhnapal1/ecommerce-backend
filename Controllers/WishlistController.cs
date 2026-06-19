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
        private readonly IConfiguration _configuration;

        public WishlistController(IBusinessLayer businessLayer, IConfiguration configuration)
        {
            _businessLayer = businessLayer;
            _configuration = configuration;
        }

        [HttpPost("add")]
        public async Task<IActionResult> AddWishlist([FromForm] WishlistModel wishlist)
        {
            wishlist.UserId = UserContextHelper.GetUserId(User);
            wishlist.IpAddress = wishlist.UserId.HasValue
                ? null
                : GuestSessionHelper.GetOrCreateGuestSessionId(HttpContext, _configuration);

            return await _businessLayer.AddWishlist(wishlist);
        }

        [HttpGet("get")]
        public async Task<IActionResult> GetWishlist()
        {
            var userId = UserContextHelper.GetUserId(User);
            var guestId = userId.HasValue
                ? null
                : GuestSessionHelper.ResolveGuestIdentifier(HttpContext, _configuration);

            return await _businessLayer.GetWishlist(userId, guestId);
        }

        [HttpDelete("delete/{id}")]
        public async Task<IActionResult> WishlistDelete(int id)
        {
            var userId = UserContextHelper.GetUserId(User);
            var guestId = userId.HasValue
                ? null
                : GuestSessionHelper.ResolveGuestIdentifier(HttpContext, _configuration);

            return await _businessLayer.WishlistDelete(id, userId, guestId);
        }
    }
}
