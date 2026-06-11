using Ecommerce_Backend.Models;
using Ecommerce_Backend.Models.BusinessLayer;
using Microsoft.AspNetCore.Mvc;

namespace Ecommerce_Backend.Controllers
{
    [ApiController]
    [Route("api/orders")]
    public class OrderController : ControllerBase
    {
        private readonly IBusinessLayer _businessLayer;
        
        public  OrderController(IBusinessLayer businessLayer)
        {
            _businessLayer = businessLayer; 
        }
        [HttpPost("create")]
        public async Task<IActionResult> CreateOrder([FromForm] CreateOrderModel model)
        {
            return await _businessLayer
                .CreateOrder(model);
        }
    }
}
