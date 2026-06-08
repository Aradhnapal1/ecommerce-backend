using Microsoft.AspNetCore.Mvc;

namespace Ecommerce_Backend.Models.BusinessLayer
{

    public partial interface IBusinessLayer
    {
        Task<IActionResult> GetCoupons();
    }
    public partial class  BusinessLayer : IBusinessLayer
    {
        public async Task<IActionResult> GetCoupons()
        {
            var result = await _databaseLayer.GetCoupons();
            return result;
        }
    }
}
