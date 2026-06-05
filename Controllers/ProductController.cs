using Ecommerce_Backend.Models;
using Ecommerce_Backend.Models.BusinessLayer;
using Ecommerce_Backend.Services;
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
        public async Task<IActionResult> GetAllProducts()
        {
            return await _businessLayer.GetAllProducts();
        }

        [HttpPost("addproduct")]
        public async Task<IActionResult> AddProduct([FromForm] ProductModel product)
        {
           var result = await _businessLayer.AddProduct(product);
            return Ok(result);
        }

        [HttpPut("updateproduct/{id}")]
        public async Task<IActionResult> UpdateProduct(int id, [FromForm] ProductModel product)
        {
            var result = await _businessLayer.UpdateProduct(id, product);
            return Ok(result);
        }


        [HttpDelete("deleteproduct/{id}")]
        public async Task<IActionResult> DeleteProduct(int id)
        {
            var result = await _businessLayer.DeleteProduct(id);
            return Ok(result);
        }

    }
}
