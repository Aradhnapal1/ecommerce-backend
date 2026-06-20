using Ecommerce_Backend.Helpers;
using Ecommerce_Backend.Models;
using Ecommerce_Backend.Models.BusinessLayer;
using Ecommerce_Backend.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Ecommerce_Backend.Controllers
{
    [ApiController]
    [Route("api/variant")]
    public class VariantController : Controller
    {

        private readonly IBusinessLayer _businessLayer;
        private readonly CloudinaryService _cloudinary;


        public VariantController(IBusinessLayer businessLayer, CloudinaryService cloudinary)
        {
            _businessLayer = businessLayer;
            _cloudinary = cloudinary;
        }
        [HttpGet("getallvariants")]


        public async Task<IActionResult> GetAllVariants()
        {
            try
            {
                var data = await _businessLayer.GetAllVariants();

                return Ok(new
                {
                    status = true,
                    data = data
                });
            }
            catch (Exception ex)
            {
                return BadRequest(new
                {
                    status = false,
                    message = ex.Message
                });
            }
        }




        [HttpPost("addvariant")]
        [Authorize(Roles = AuthRoles.Admin)]
        public async Task<IActionResult> AddVariant([FromForm] ProductVariantModel variant)
        {
            try
            {
                var result = await _businessLayer.AddVariant(variant);

                return Ok(new
                {
                    status = true,
                    data = result
                });
            }
            catch (Exception ex)
            {
                return BadRequest(new
                {
                    status = false,
                    message = ex.Message
                });
            }
        }



        [HttpPut("updatevariant/{id}")]
        [Authorize(Roles = AuthRoles.Admin)]
        public async Task<IActionResult> UpdateVariant(int id, [FromForm] ProductVariantModel variant)
        {
            try
            {
                var result = await _businessLayer.UpdateVariant(id, variant);

                return Ok(new
                {
                    status = true,
                    data = result
                });
            }
            catch (Exception ex)
            {
                return BadRequest(new
                {
                    status = false,
                    message = ex.Message
                });
            }
        }



        [HttpDelete("deletevariant/{id}")]
        [Authorize(Roles = AuthRoles.Admin)]
        public async Task<IActionResult> DeleteVariant(int id)
        {
            var result = await _businessLayer.DeleteVariant(id);

            return Ok(result);
        }



        [HttpGet("by-product/{productId:int}")]
        public async Task<IActionResult> GetVariantsByProduct(int productId)
        {
            return await _businessLayer.GetProductVariants(productId);
        }

        [HttpGet("by-slug/{slug}")]
        public async Task<IActionResult> GetVariantBySlug(string slug)
        {
            var variant = await _businessLayer.GetVariantBySlug(slug);
            if (variant == null)
            {
                return NotFound(new { success = false, message = "Variant not found" });
            }

            return Ok(new { success = true, data = variant });
        }

        [HttpGet("getvariantbyid/{id}")]
        public async Task<IActionResult> GetVariantById(int id)
        {
            try
            {
                var data = await _businessLayer.GetVariantById(id);

                return Ok(new
                {
                    status = true,
                    data = data
                });
            }
            catch (Exception ex)
            {
                return BadRequest(new
                {
                    status = false,
                    message = ex.Message
                });
            }
        }
    }
}
