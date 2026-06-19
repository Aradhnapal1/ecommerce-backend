using Ecommerce_Backend.Helpers;
using Ecommerce_Backend.Models;
using Ecommerce_Backend.Models.BusinessLayer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Ecommerce_Backend.Controllers
{
    [ApiController]
    [Route("api")]
    public class ContactController : ControllerBase
    {
        private readonly IBusinessLayer _businessLayer;

        public ContactController(IBusinessLayer businessLayer)
        {
            _businessLayer = businessLayer;
        }
        [HttpGet("getcontact")]
        [Authorize(Roles = AuthRoles.Admin)]
        public async Task<IActionResult> GetContacts()
        {
            return await _businessLayer.GetContacts();
        }


        [HttpPost("addcontact")]
        [EnableRateLimiting("contact")]
        public async Task<IActionResult> CreateContact([FromForm] ContactModel contact)
        {
            return await _businessLayer.CreateContact(contact);
        }

        [HttpDelete("deletecontact/{id}")]
        [Authorize(Roles = AuthRoles.Admin)]
        public async Task<IActionResult> DeleteContact(int id)
        {
            return await _businessLayer.DeleteContact(id);
        }
    }
}
