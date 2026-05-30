using Ecommerce_Backend.Models;
using Ecommerce_Backend.Models.BusinessLayer;
using Microsoft.AspNetCore.Mvc;
using Microsoft.DotNet.Scaffolding.Shared.Messaging;
using NuGet.Protocol.Plugins;

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
        public async Task<IActionResult> AddSize([FromForm] SizeModel size)
        {
            return await _businessLayer.AddSize(size);
        }



        [HttpPut("updatesize/{id}")]
        public async Task<IActionResult> UpdateSize(int id, [FromForm] SizeModel size)
        {
            var result = await _businessLayer.UpdateSize(id, size);


            return Ok(new
            {
                status = true,
                message = "Size updated successfully"

            });
        }

        [HttpDelete("deletesize/{id}")]
        public async Task<IActionResult> DeleteSize(int id)
        {
            var result = await _businessLayer.DeleteSize(id);
            return Ok(new
            {
                status = true,
                message = "Size deleted successfully"
            });
        }
    }

}