using Microsoft.AspNetCore.Mvc;

namespace Ecommerce_Backend.Models.BusinessLayer
{
   public partial interface IBusinessLayer
    {
        Task<IActionResult> AddCategory([FromForm] CategoryModel category);
        Task<IActionResult> GetCategories();
    }
    public partial class BusinessLayer : IBusinessLayer
    {
        public async Task<IActionResult> AddCategory([FromForm] CategoryModel category)
        {
            try
            {
                var result = await _databaseLayer.AddCategory(category);
                if (result != null)
                {
                    return new OkObjectResult(result);
                }
                else
                {
                    return new BadRequestObjectResult("Failed to add category.");
                }
            }
            catch (Exception ex)
            {
                // Log the exception (not implemented here)
                return new StatusCodeResult(500); // Internal Server Error
            }
        }


        public async Task<IActionResult> GetCategories()
        {
            try
            {
                var result = await _databaseLayer.GetCategories();
                if (result != null)
                {
                    return new OkObjectResult(result);
                }
                else
                {
                    return new BadRequestObjectResult("Failed to retrieve categories.");
                }
            }
            catch (Exception ex)
            {
                // Log the exception (not implemented here)
                return new StatusCodeResult(500); // Internal Server Error
            }
        }
    }
}
