using Microsoft.AspNetCore.Mvc;

namespace Ecommerce_Backend.Models.BusinessLayer
{
    public partial interface IBusinessLayer
    {
        Task<IActionResult> GetBanner();
        Task<IActionResult> AddBanner([FromForm] BannerModel banner);
    }

    public partial class BusinessLayer : IBusinessLayer
    {
        public async Task<IActionResult> GetBanner()
        {
            var result = await _databaseLayer.GetBanner();
            return new OkObjectResult(result);
        }

        public async Task<IActionResult> AddBanner([FromForm] BannerModel banner)
        {
            var result = await _databaseLayer.AddBanner(banner);
            return result;
        }
    }
}
