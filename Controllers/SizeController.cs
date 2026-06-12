using Ecommerce_Backend.Helpers;
using Ecommerce_Backend.Models;
using Ecommerce_Backend.Models.BusinessLayer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Ecommerce_Backend.Controllers
{
    [ApiController]
    [Route("api/size")]
    public class SizeController : ControllerBase
    {
        private readonly IBusinessLayer _businessLayer;

        public SizeController(IBusinessLayer businessLayer)
        {
            _businessLayer = businessLayer;
        }

        [HttpGet("getallsize")]
        public async Task<IActionResult> GetAllSizes()
        {
            var result = await _businessLayer.GetAllSizes();
            return Ok(result);
        }

        [HttpPost("addsize")]
        [Authorize(Roles = AuthRoles.Admin)]
        public async Task<IActionResult> AddSize([FromForm] SizeModel size)
        {
            return await _businessLayer.AddSize(size);
        }

        [HttpPut("updatesize/{id}")]
        [Authorize(Roles = AuthRoles.Admin)]
        public async Task<IActionResult> UpdateSize(int id, [FromForm] SizeModel size)
        {
            return await _businessLayer.UpdateSize(id, size);
        }

        [HttpDelete("deletesize/{id}")]
        [Authorize(Roles = AuthRoles.Admin)]
        public async Task<IActionResult> DeleteSize(int id)
        {
            return await _businessLayer.DeleteSize(id);
        }
    }
}
