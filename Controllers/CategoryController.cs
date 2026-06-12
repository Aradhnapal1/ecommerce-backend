using Ecommerce_Backend.Helpers;
using Ecommerce_Backend.Models;
using Ecommerce_Backend.Models.BusinessLayer;
using Ecommerce_Backend.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Ecommerce_Backend.Controllers
{
    [ApiController]
    [Route("api")]
    public class CategoryController : ControllerBase
    {
      
        private readonly IBusinessLayer _businessLayer;
        private readonly CloudinaryService _cloudinary;

        public CategoryController(IBusinessLayer businessLayer, CloudinaryService cloudinary)
        {
            _businessLayer = businessLayer;
            _cloudinary = cloudinary;
        }

        [HttpPost("addcategory")]
        [Authorize(Roles = AuthRoles.Admin)]
        public async Task<IActionResult> AddCategory([FromForm] CategoryModel category)
        {
            return await _businessLayer.AddCategory(category);
        }

        [HttpGet("getcategories")]
        public async Task<IActionResult> GetCategories()
        {
            return await _businessLayer.GetCategories();
        }

        [HttpPut("updatecategory/{id}")]
        [Authorize(Roles = AuthRoles.Admin)]
        public async Task<IActionResult> UpdateCategory(int id,[FromForm] CategoryModel category)
        {
            return await _businessLayer.UpdateCategory(id, category);
        }

        [HttpDelete("deletecategory/{id}")]
        [Authorize(Roles = AuthRoles.Admin)]
        public async Task<IActionResult> DeleteCategory(int id)
        {
            return await _businessLayer.DeleteCategory(id);
        }
    }
}
