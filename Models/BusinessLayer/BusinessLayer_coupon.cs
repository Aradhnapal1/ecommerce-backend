using Microsoft.AspNetCore.Mvc;

namespace Ecommerce_Backend.Models.BusinessLayer
{

    public partial interface IBusinessLayer
    {
        Task<IActionResult> GetCoupons();
        Task<IActionResult> AddCoupon([FromBody] CouponModel coupon);
        Task<IActionResult> EditCoupun(int id, [FromBody] CouponModel coupon);
        Task<IActionResult> DeleteCoupon(int id);
    }
    public partial class  BusinessLayer : IBusinessLayer
    {
        public async Task<IActionResult> GetCoupons()
        {
            var result = await _databaseLayer.GetCoupons();
            return result;
        }

        public async Task<IActionResult> AddCoupon([FromBody] CouponModel coupon)
        {
            var result = await _databaseLayer.AddCoupon(coupon);
            return result;
        }

        public async Task<IActionResult> EditCoupun(int id, [FromBody] CouponModel coupon)
        {
            var result = await _databaseLayer.EditCoupun(id, coupon);
            return result;
        }

        public async Task<IActionResult> DeleteCoupon(int id)
        {
            var result = await _databaseLayer.DeleteCoupon(id);
            return result;
        }
    }
}
