using Ecommerce_Backend.Helpers;
using Ecommerce_Backend.Models;
using Ecommerce_Backend.Models.BusinessLayer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Ecommerce_Backend.Controllers
{
    [ApiController]
    [Route("api/address")]
    [Authorize]
    public class AddressController : ControllerBase
    {
        private readonly IBusinessLayer _businessLayer;

        public AddressController(
            IBusinessLayer businessLayer)
        {
            _businessLayer = businessLayer;
        }

        [HttpPost("add")]
        public async Task<IActionResult> AddAddress(
            [FromBody] UserAddressModel model)
        {
            var userId = UserContextHelper.GetUserId(User)!.Value;

            return await _businessLayer
                .AddAddress(
                    model,
                    userId
                );
        }

        [HttpGet("list")]
        public async Task<IActionResult> GetAddressList()
        {
            int userId = UserContextHelper.GetUserId(User)!.Value;

            return await _businessLayer
                .GetAddressList(userId);
        }


        [HttpDelete("delete/{id}")]
        public async Task<IActionResult> DeleteAddress(int id)
        {
            int userId = UserContextHelper.GetUserId(User)!.Value;
            return await _businessLayer
                .DeleteAddress(id, userId);
        }
        [HttpPut("update/{id}")]
        public async Task<IActionResult> UpdateAddress(
    int id,
    [FromBody] UserAddressModel model)
        {
            int userId = UserContextHelper.GetUserId(User)!.Value;

            model.Id = id;

            return await _businessLayer
                .UpdateAddress(
                    model,
                    userId
                );
        }

    }
}