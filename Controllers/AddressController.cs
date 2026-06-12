using Ecommerce_Backend.Models;
using Ecommerce_Backend.Models.BusinessLayer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace Ecommerce_Backend.Controllers
{
    [ApiController]
    [Route("api/address")]
    public class AddressController : ControllerBase
    {
        private readonly IBusinessLayer _businessLayer;

        public AddressController(
            IBusinessLayer businessLayer)
        {
            _businessLayer = businessLayer;
        }

        [HttpPost("add")]
        [Authorize]
        public async Task<IActionResult> AddAddress(
            [FromBody] UserAddressModel model)
        {
            var userId =
                Convert.ToInt32(
                    User.FindFirst(
                        ClaimTypes.NameIdentifier
                    )?.Value
                );

            return await _businessLayer
                .AddAddress(
                    model,
                    userId
                );
        }

        [HttpGet("list")]
        [Authorize]
        public async Task<IActionResult> GetAddressList()
        {
            int userId =
                Convert.ToInt32(
                    User.FindFirst(
                        ClaimTypes.NameIdentifier
                    )?.Value
                );

            return await _businessLayer
                .GetAddressList(userId);
        }



    }
}