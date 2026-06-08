using Ecommerce_Backend.Models;
using Ecommerce_Backend.Models.BusinessLayer;
using Microsoft.AspNetCore.Mvc;

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
    }

}
