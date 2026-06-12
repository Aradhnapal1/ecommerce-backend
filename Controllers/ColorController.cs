using Ecommerce_Backend.Helpers;
using Ecommerce_Backend.Models;
using Ecommerce_Backend.Models.BusinessLayer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Ecommerce_Backend.Controllers
{
    [Route("api/colors")]
    [ApiController]
    public class ColorController : ControllerBase
    {
        private readonly IBusinessLayer businessLayer;

        public ColorController(IBusinessLayer businessLayer)
        {
            this.businessLayer = businessLayer;
        }

        [HttpGet("get-all")]
        public async Task<IActionResult> GetAllColors()
        {
            var colors = await businessLayer.GetAllColors();

            return Ok(colors);
        }

        [HttpPost("create")]
        [Authorize(Roles = AuthRoles.Admin)]
        public async Task<IActionResult> CreateColor([FromBody] ColorResponse color)
        {
            try
            {
                var createdColor = await businessLayer.CreateColor(color);
                return Ok(new
                {
                    success = true,
                    message = "Color added successfully.",
                    data = createdColor
                });
            }
            catch (Exception ex)
            {
                return BadRequest(new { success = false, message = ex.Message });
            }
        }

        [HttpPut("update/{id}")]
        [Authorize(Roles = AuthRoles.Admin)]
        public async Task<IActionResult> UpdateColor(int id, [FromBody] ColorResponse color)
        {
            try
            {
                var updatedColor = await businessLayer.UpdateColor(id, color);
                return Ok(new
                {
                    success = true,
                    message = "Color updated successfully.",
                });
            }
            catch (Exception ex)
            {
                return BadRequest(new { success = false, message = ex.Message });
            }
        }

        [HttpDelete("delete/{id}")]
        [Authorize(Roles = AuthRoles.Admin)]
        public async Task<IActionResult> DeleteColor(int id)
        {
            try
            {
                await businessLayer.DeleteColor(id);
                return Ok(new
                {
                    success = true,
                    message = "Color deleted successfully."
                });
            }
            catch (Exception ex)
            {
                return BadRequest(new { success = false, message = ex.Message });
            }


        }
    }
}