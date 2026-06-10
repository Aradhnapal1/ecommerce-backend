using Ecommerce_Backend.Models;
using Ecommerce_Backend.Models.BusinessLayer;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace Ecommerce_Backend.Controllers
{
    [ApiController]
    [Route("api/cart")]
    public class CartController : ControllerBase
    {
        private readonly IBusinessLayer _businessLayer;

        public CartController(IBusinessLayer businessLayer)
        {
            _businessLayer = businessLayer;
        }

        [HttpGet("getall")]
        public async Task<IActionResult> GetAllCartItems()
        {
            var data = await _businessLayer.GetAllCartItems();
            return Ok(new { success = true, count = data.Count, data });
        }

        [HttpPost("add")]
        public async Task<IActionResult> AddCartItem([FromForm] CartModel cartItem)
        {
            var userIdClaim = User.FindFirst("UserId")?.Value
                ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            if (!string.IsNullOrEmpty(userIdClaim) && int.TryParse(userIdClaim, out int uid))
            {
                cartItem.UserId = uid;
                cartItem.IpAddress = null;
            }
            else
            {
                cartItem.UserId = null;
                cartItem.IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString();
            }

            return await _businessLayer.AddCartItem(cartItem);
        }


    }
}