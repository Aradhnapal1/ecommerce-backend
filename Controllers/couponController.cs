using Ecommerce_Backend.Models;
using Ecommerce_Backend.Models.BusinessLayer;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace Ecommerce_Backend.Controllers
{
    [ApiController]
    [Route("api/coupons")]
    public class couponController : ControllerBase
    {
        private IBusinessLayer _businessLayer;

        public couponController(IBusinessLayer businessLayer)
        {
            _businessLayer = businessLayer;
        }

        [HttpGet("getcoupons")]
        public async Task<IActionResult> GetCoupons()
        {
            return await _businessLayer.GetCoupons();
        }

        [HttpPost("add")]

        public async Task <IActionResult> AddCoupon([FromBody] CouponModel coupon) { 
            return await _businessLayer.AddCoupon(coupon);
        }

        [HttpPut("edit/{id}")]
        public async Task <IActionResult>EditCoupun(int id, [FromBody]CouponModel coupon)
        {
            return await _businessLayer.EditCoupun(id, coupon);
        }

        [HttpDelete("delete/{id}")]
        public async Task <IActionResult> DeleteCoupon(int id)
        {
            return await _businessLayer.DeleteCoupon(id);
        }

        [HttpPost("apply")]
        public async Task<IActionResult> ApplyCoupon(
    [FromForm] ApplyCouponModel model)
        {
            int? userId = null;
            string? ipAddress = null;

            var userClaim =
                User.FindFirst(
                    System.Security.Claims.ClaimTypes.NameIdentifier
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
                    HttpContext.Connection
                    .RemoteIpAddress?
                    .ToString();
            }

            return await _businessLayer
                .ApplyCoupon(
                    model,
                    userId,
                    ipAddress
                );
        }

    }

}
