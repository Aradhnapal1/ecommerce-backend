using Ecommerce_Backend.Models;
using Ecommerce_Backend.Models.BusinessLayer;
using Ecommerce_Backend.Services;
using Microsoft.AspNetCore.Mvc;

namespace Ecommerce_Backend.Controllers
{
    [ApiController]
    [Route("api/brand")]
    public class BrandController : ControllerBase
    {
        private readonly IBusinessLayer _businessLayer;
        private readonly CloudinaryService _cloudinary;


        public BrandController(IBusinessLayer businessLayer, CloudinaryService cloudinary)
        {
            _businessLayer = businessLayer;
            _cloudinary = cloudinary;

        }

        [HttpPost("addbrand")]
        public async Task<IActionResult> AddBrand([FromForm] BrandModel brand)
        {
            return await _businessLayer.AddBrand(brand);
        }



        [HttpGet("getallbrands")]
        public async Task<IActionResult> GetAllBrands()
        {
            return await _businessLayer.GetAllBrands();
        }

        [HttpPut("updatebrand/{id}")]
        public async Task<IActionResult> UpdateBrand(int id, [FromForm] BrandModel brand)
        {
            return await _businessLayer.UpdateBrand(id, brand);

        }

        [HttpDelete("deletebrand/{id}")]
        public async Task<IActionResult> DeleteBrand(int id)
        {
            return await _businessLayer.DeleteBrand(id);


        }
    }
}