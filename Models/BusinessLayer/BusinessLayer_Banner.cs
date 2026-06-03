using Microsoft.AspNetCore.Mvc;

namespace Ecommerce_Backend.Models.BusinessLayer
{
    public partial interface IBusinessLayer
    {
        Task<IActionResult> GetBanner();
    }

    public partial class BusinessLayer : IBusinessLayer
    {
        public async Task<IActionResult> GetBanner()
        {
            var result = await _databaseLayer.GetBanner();
            return result;
        }
    }
}
