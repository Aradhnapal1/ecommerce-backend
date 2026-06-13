using Microsoft.AspNetCore.Mvc;

namespace Ecommerce_Backend.Models.BusinessLayer
{
   public partial interface IBusinessLayer
    {
        Task<IActionResult> AddCategory([FromForm] CategoryModel category);
        Task<IActionResult> GetCategories();
        Task<IActionResult> UpdateCategory(int id, [FromForm] CategoryModel category);
        Task<IActionResult> DeleteCategory(int id);
        Task<IActionResult> GetCategoriesByTypeHome();
        Task<IActionResult> GetCategoriesByHeroSection();
        Task<IActionResult> GetCategoriesByBrowseCategory();
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

        public async Task<IActionResult> GetCategoriesByTypeHome()
        {
            var result = await _databaseLayer.GetCategoriesByTypeHome();
            return result;
        }

        public async Task<IActionResult> GetCategoriesByHeroSection()
        {
            var result = await _databaseLayer.GetCategoriesByHeroSection();
            return result;
        }

        public async Task<IActionResult> GetCategoriesByBrowseCategory()
        {
            var result = await _databaseLayer.GetCategoriesByBrowseCategory();
            return result;
        }






        public async Task<IActionResult> UpdateCategory(int id, [FromForm] CategoryModel category)
        {
            try
            {
                var result = await _databaseLayer.UpdateCategory(id, category);
                if (result != null)
                {
                    return new OkObjectResult(result);
                }
                else
                {
                    return new BadRequestObjectResult("Failed to update category.");
                }
            }
            catch (Exception ex)
            {
                // Log the exception (not implemented here)
                return new StatusCodeResult(500); // Internal Server Error
            }
        }

        public async Task<IActionResult> DeleteCategory(int id)
        {
            try
            {
                var result = await _databaseLayer.DeleteCategory(id);
                if (result != null)
                {
                    return new OkObjectResult(result);
                }
                else
                {
                    return new BadRequestObjectResult("Failed to delete category.");
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
