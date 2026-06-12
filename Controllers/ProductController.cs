using Ecommerce_Backend.Helpers;
using Ecommerce_Backend.Models;
using Ecommerce_Backend.Models.BusinessLayer;
using Ecommerce_Backend.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Ecommerce_Backend.Controllers
{
    [ApiController]
    [Route("api/product")]
    public class ProductController : ControllerBase
    {

        private readonly IBusinessLayer _businessLayer;
        private readonly CloudinaryService _cloudinary;



        public ProductController(IBusinessLayer businessLayer, CloudinaryService cloudinary)
        {
            _businessLayer = businessLayer;
            _cloudinary = cloudinary;
        }

        [HttpGet("getallproducts")]
        public async Task<IActionResult> GetAllProducts([FromQuery] ProductFilterRequest? filter)
        {
            if (filter != null && filter.HasFilters())
                return await _businessLayer.GetFilteredProducts(filter);

            return await _businessLayer.GetAllProducts();
        }

        /// <summary>
        /// Filter products by category, brand, color, size, price, discount, search, sort.
        /// </summary>
        [HttpGet("filter")]
        public async Task<IActionResult> FilterProducts([FromQuery] ProductFilterRequest filter)
        {
            return await _businessLayer.GetFilteredProducts(filter);
        }

        /// <summary>
        /// Global product search — name, SKU, slug, description, brand, category, color.
        /// Combine with multi-select filters: categoryIds, brandIds, colorIds, sizeIds, discountPercents.
        /// </summary>
        [HttpGet("search")]
        public async Task<IActionResult> SearchProducts(
            [FromQuery] string q,
            [FromQuery] ProductFilterRequest? filter)
        {
            return await _businessLayer.SearchProducts(q, filter);
        }

        [HttpPost("addproduct")]
        [Authorize(Roles = AuthRoles.Admin)]
        public async Task<IActionResult> AddProduct([FromForm] ProductModel product)
        {
            var result = await _businessLayer.AddProduct(product);
            return Ok(result);
        }

        [HttpPut("updateproduct/{id}")]
        [Authorize(Roles = AuthRoles.Admin)]
        public async Task<IActionResult> UpdateProduct(int id, [FromForm] ProductModel product)
        {
            var result = await _businessLayer.UpdateProduct(id, product);
            return Ok(result);
        }


        [HttpDelete("deleteproduct/{id}")]
        [Authorize(Roles = AuthRoles.Admin)]
        public async Task<IActionResult> DeleteProduct(int id)
        {
            var result = await _businessLayer.DeleteProduct(id);
            return Ok(result);
        }


        [HttpGet("getproductbyid/{id}")]
        public async Task<IActionResult> GetProductById(int id)
        {
            var result = await _businessLayer.GetProductById(id);

            if (result == null)
            {
                return NotFound(new
                {
                    status = false,
                    message = "Product not found"
                });
            }

            return Ok(new
            {
                status = true,
                data = result
            });
        }
    }
}
