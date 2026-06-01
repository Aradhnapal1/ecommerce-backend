using Microsoft.AspNetCore.Mvc;

namespace Ecommerce_Backend.Models.BusinessLayer
{
    public partial interface IBusinessLayer
    {
        Task<IActionResult> AddBrand(BrandModel brand);
        Task<IActionResult> GetAllBrands();
        Task<IActionResult> UpdateBrand(int id, [FromForm] BrandModel brand);
        Task<IActionResult> DeleteBrand(int id);
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


        public async Task<IActionResult> UpdateBrand(int id, [FromForm] BrandModel brand)
        {
            if (id <= 0)
            {
                return new BadRequestObjectResult(new
                {
                    status = false,
                    message = "Invalid brand ID"
                });
            }
            if (string.IsNullOrWhiteSpace(brand.BrandName))
            {
                return new BadRequestObjectResult(new
                {
                    status = false,
                    message = "Brand name is required"
                });
            }
            // Image update is optional, so no need to check for it
            return await _databaseLayer.UpdateBrand(id, brand);
        }



        public async Task<IActionResult> DeleteBrand(int id)
        {
            if (id <= 0)
            {
                return new BadRequestObjectResult(new
                {
                    status = false,
                    message = "Invalid brand ID"
                });
            }
            return await _databaseLayer.DeleteBrand(id);
        }
    }
}