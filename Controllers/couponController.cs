using Ecommerce_Backend.Helpers;
using Ecommerce_Backend.Models;
using Ecommerce_Backend.Models.BusinessLayer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Ecommerce_Backend.Controllers
{
    [ApiController]
    [Route("api/coupons")]
    public class couponController : ControllerBase
    {
        private readonly IBusinessLayer _businessLayer;

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
        [Authorize(Roles = AuthRoles.Admin)]
        public async Task<IActionResult> AddCoupon([FromBody] CouponModel coupon)
        {
            return await _businessLayer.AddCoupon(coupon);
        }

        [HttpPut("edit/{id}")]
        [Authorize(Roles = AuthRoles.Admin)]
        public async Task<IActionResult> EditCoupun(int id, [FromBody] CouponModel coupon)
        {
            return await _businessLayer.EditCoupun(id, coupon);
        }

        [HttpDelete("delete/{id}")]
        [Authorize(Roles = AuthRoles.Admin)]
        public async Task<IActionResult> DeleteCoupon(int id)
        {
            return await _businessLayer.DeleteCoupon(id);
        }

        [HttpPost("apply")]
        [Authorize]
        public async Task<IActionResult> ApplyCoupon([FromForm] ApplyCouponModel model)
        {
            var userId = UserContextHelper.GetUserId(User)!.Value;

            return await _businessLayer.ApplyCoupon(model, userId, null);
        }
    }
}
