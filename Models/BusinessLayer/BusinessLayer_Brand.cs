using Microsoft.AspNetCore.Mvc;

namespace Ecommerce_Backend.Models.BusinessLayer
{
    public partial interface IBusinessLayer
    {
        Task<IActionResult> AddBrand(BrandModel brand);
        Task<IActionResult> GetAllBrands();
    }

    public partial class BusinessLayer : IBusinessLayer
    {
        public async Task<IActionResult> AddBrand(BrandModel brand)
        {
            if (string.IsNullOrWhiteSpace(brand.BrandName))
            {
                return new BadRequestObjectResult(new
                {
                    status = false,
                    message = "Brand name is required"
                });
            }

            if (brand.BrandFile == null || brand.BrandFile.Length == 0)
            {
                return new BadRequestObjectResult(new
                {
                    status = false,
                    message = "Brand image is required"
                });
            }

            return await _databaseLayer.AddBrand(brand);
        }



        public async Task<IActionResult> GetAllBrands()
        {
            return await _databaseLayer.GetAllBrands();
        }
    }
}