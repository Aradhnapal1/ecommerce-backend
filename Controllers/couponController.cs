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

    }
}
