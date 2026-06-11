using Ecommerce_Backend.Models;
using Ecommerce_Backend.Models.BusinessLayer;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace Ecommerce_Backend.Controllers
{
    [ApiController]
    [Route("api/addcart")]
    public class AddCartController : ControllerBase
    {
        private readonly IBusinessLayer _businessLayer;

        public AddCartController(IBusinessLayer businessLayer)
        {
            _businessLayer = businessLayer;
        }

        [HttpPost("add")]
        public async Task<IActionResult> AddCart(
            [FromForm] AddCartModel cart)
        {
            var userIdClaim =
                User.FindFirst(
                    ClaimTypes.NameIdentifier
                );

            cart.IpAddress =
                Request.Headers["X-Forwarded-For"]
                .FirstOrDefault()
                ??
                HttpContext.Connection
                .RemoteIpAddress
                ?.ToString();

            if (userIdClaim != null)
            {
                cart.UserId =
                    Convert.ToInt32(
                        userIdClaim.Value
                    );
            }

            return await _businessLayer
                .AddCart(cart);
        }

        [HttpGet("list")]
        public async Task<IActionResult> GetCart()
        {
            int? userId = null;
            string? ipAddress = null;

            var userClaim =
                User.FindFirst(
                    ClaimTypes.NameIdentifier
                );

            if (userClaim != null)
            {
                userId =
                    Convert.ToInt32(
                        userClaim.Value
                    );
            }
            else
            {
                ipAddress =
                    Request.Headers["X-Forwarded-For"]
                    .FirstOrDefault()
                    ?? HttpContext.Connection
                    .RemoteIpAddress?.ToString();
            }

            return await _businessLayer
                .GetCart(
                    userId,
                    ipAddress
                );
        }


        [HttpPut("update-quantity")]
        public async Task<IActionResult> UpdateCartQuantity(
    [FromForm] UpdateCartQuantityModel model)
        {
            var userIdClaim =
                User.FindFirst(
                    ClaimTypes.NameIdentifier
                );

            model.IpAddress =
                Request.Headers["X-Forwarded-For"]
                .FirstOrDefault()
                ??
                HttpContext.Connection
                .RemoteIpAddress
                ?.ToString();

            if (userIdClaim != null)
            {
                model.UserId =
                    Convert.ToInt32(
                        userIdClaim.Value
                    );
            }

            return await _businessLayer
                .UpdateCartQuantity(model);
        }


        [HttpDelete("delete/{id}")]

        public async Task<IActionResult> DeleteCartItem(int id)
        {
            return await _businessLayer
                .DeleteCartItem(id);
        }
    }
}