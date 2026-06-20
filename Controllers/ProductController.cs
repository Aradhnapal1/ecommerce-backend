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

        [HttpGet("filter")]
        public async Task<IActionResult> FilterProducts([FromQuery] ProductFilterRequest filter)
        {
            return await _businessLayer.GetFilteredProducts(filter);
        }

        [HttpGet("search")]
        public async Task<IActionResult> SearchProducts(
            [FromQuery] string q,
            [FromQuery] ProductFilterRequest? filter)
        {
            return await _businessLayer.SearchProducts(q, filter);
        }

        [HttpGet("top-discounted")]
        public async Task<IActionResult> GetTopDiscountedProducts()
        {
            return await _businessLayer.GetTopDiscountedProducts();
        }

        /// <summary>Products by category slug (supports pagination &amp; filters).</summary>
        [HttpGet("category/{categorySlug}")]
        public async Task<IActionResult> GetByCategorySlug(
            string categorySlug,
            [FromQuery] ProductFilterRequest? filter)
        {
            return await _businessLayer.GetProductsByCategorySlug(categorySlug, filter);
        }

        /// <summary>Products by brand slug (supports pagination &amp; filters).</summary>
        [HttpGet("brand/{brandSlug}")]
        public async Task<IActionResult> GetByBrandSlug(
            string brandSlug,
            [FromQuery] ProductFilterRequest? filter)
        {
            return await _businessLayer.GetProductsByBrandSlug(brandSlug, filter);
        }

        /// <summary>Single product by URL slug.</summary>
        [HttpGet("by-slug/{slug}")]
        public async Task<IActionResult> GetProductBySlug(string slug)
        {
            var result = await _businessLayer.GetProductBySlug(slug);
            if (result == null)
            {
                return NotFound(new { success = false, message = "Product not found" });
            }

            return Ok(new { success = true, data = result });
        }

        /// <summary>Full product page: product + variants + reviews summary + related products.</summary>
        [HttpGet("detail/slug/{slug}")]
        public async Task<IActionResult> GetProductDetailBySlug(string slug)
        {
            var detail = await _businessLayer.GetProductDetail(null, slug);
            if (detail == null)
            {
                return NotFound(new { success = false, message = "Product not found" });
            }

            return Ok(new { success = true, data = detail });
        }

        [HttpGet("detail/{id:int}")]
        public async Task<IActionResult> GetProductDetailById(int id)
        {
            var detail = await _businessLayer.GetProductDetail(id, null);
            if (detail == null)
            {
                return NotFound(new { success = false, message = "Product not found" });
            }

            return Ok(new { success = true, data = detail });
        }

        /// <summary>Similar products (same category / brand).</summary>
        [HttpGet("{id:int}/related")]
        public async Task<IActionResult> GetRelatedProducts(int id, [FromQuery] int limit = 8)
        {
            return await _businessLayer.GetRelatedProducts(id, limit);
        }

        /// <summary>All active variants for a product (color, size, stock, price).</summary>
        [HttpGet("{productId:int}/variants")]
        public async Task<IActionResult> GetProductVariants(int productId)
        {
            return await _businessLayer.GetProductVariants(productId);
        }

        [HttpGet("getproductbyid/{id:int}")]
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
    }
}
