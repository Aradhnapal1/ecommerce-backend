using Ecommerce_Backend.Models.BusinessLayer;
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
    }
}