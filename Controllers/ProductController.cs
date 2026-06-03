using Ecommerce_Backend.Models.BusinessLayer;
using Microsoft.AspNetCore.Mvc;

namespace Ecommerce_Backend.Controllers
{
    [ApiController]
    [Route("api/product")]
    public class ProductController : ControllerBase
    {

        private readonly IBusinessLayer _businessLayer;
        public ProductController(IBusinessLayer businessLayer)
        {
            _businessLayer = businessLayer;
        }

        [HttpGet("getallproducts")]
        public async Task<IActionResult> GetAllProducts()
        {
            return await _businessLayer.GetAllProducts();
        }
    }
}
