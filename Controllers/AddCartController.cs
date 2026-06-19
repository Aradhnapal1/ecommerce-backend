using Ecommerce_Backend.Helpers;
using Ecommerce_Backend.Models;
using Ecommerce_Backend.Models.BusinessLayer;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Ecommerce_Backend.Controllers
{
    [ApiController]
    [Route("api/addcart")]
    public class AddCartController : ControllerBase
    {
        private readonly IBusinessLayer _businessLayer;
        private readonly IConfiguration _configuration;

        public AddCartController(IBusinessLayer businessLayer, IConfiguration configuration)
        {
            _businessLayer = businessLayer;
            _configuration = configuration;
        }

        [HttpPost("add")]
        public async Task<IActionResult> AddCart([FromForm] AddCartModel cart)
        {
            cart.UserId = UserContextHelper.GetUserId(User);
            cart.IpAddress = cart.UserId.HasValue
                ? null
                : GuestSessionHelper.GetOrCreateGuestSessionId(HttpContext, _configuration);

            return await _businessLayer.AddCart(cart);
        }

        [HttpGet("list")]
        public async Task<IActionResult> GetCart()
        {
            var userId = UserContextHelper.GetUserId(User);
            var guestId = userId.HasValue
                ? null
                : GuestSessionHelper.ResolveGuestIdentifier(HttpContext, _configuration);

            return await _businessLayer.GetCart(userId, guestId);
        }

        [HttpPut("update-quantity")]
        public async Task<IActionResult> UpdateCartQuantity([FromForm] UpdateCartQuantityModel model)
        {
            model.UserId = UserContextHelper.GetUserId(User);
            model.IpAddress = model.UserId.HasValue
                ? null
                : GuestSessionHelper.ResolveGuestIdentifier(HttpContext, _configuration);

            return await _businessLayer.UpdateCartQuantity(model);
        }

        [HttpDelete("delete/{id}")]
        public async Task<IActionResult> DeleteCartItem(int id)
        {
            var userId = UserContextHelper.GetUserId(User);
            var guestId = userId.HasValue
                ? null
                : GuestSessionHelper.ResolveGuestIdentifier(HttpContext, _configuration);

            return await _businessLayer.DeleteCartItem(id, userId, guestId);
        }

        [HttpDelete("clear")]
        public async Task<IActionResult> ClearCart()
        {
            var userId = UserContextHelper.GetUserId(User);
            var guestId = userId.HasValue
                ? null
                : GuestSessionHelper.ResolveGuestIdentifier(HttpContext, _configuration);

            return await _businessLayer.ClearCart(userId, guestId);
        }
    }
}
