using Ecommerce_Backend.Models;
using Ecommerce_Backend.Models.BusinessLayer;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace Ecommerce_Backend.Controllers
{
    [ApiController]
    [Route("api/wishlist")]
    public class WishlistController : ControllerBase
    {
        private readonly IBusinessLayer _businessLayer;

        public WishlistController(
            IBusinessLayer businessLayer)
        {
            _businessLayer = businessLayer;
        }

        [HttpPost("add")]
        public async Task<IActionResult> AddWishlist(
            [FromForm] WishlistModel wishlist)
        {
            string currentIp =
                Request.Headers["X-Forwarded-For"]
                .FirstOrDefault()
                ?? HttpContext.Connection
                .RemoteIpAddress?.ToString();

            var userIdClaim =
                User.FindFirst(ClaimTypes.NameIdentifier);

            if (userIdClaim != null)
            {
                wishlist.UserId =
                    Convert.ToInt32(userIdClaim.Value);

                // IMPORTANT
                wishlist.IpAddress = currentIp;
            }
            else
            {
                wishlist.UserId = null;
                wishlist.IpAddress = currentIp;
            }

            return await _businessLayer
                .AddWishlist(wishlist);
        }

        [HttpGet("get")]
        public async Task<IActionResult> GetWishlist()
        {
            return await _businessLayer.GetWishlist();
        }

        [HttpDelete("delete/{id}")]
        public async Task<IActionResult>WishlistDelete(int id)
        {
            return await _businessLayer.WishlistDelete(id);
        }
}
}