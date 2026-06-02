using Ecommerce_Backend.Models;
using Ecommerce_Backend.Models.BusinessLayer;
using Microsoft.AspNetCore.Mvc;

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
        public async Task<IActionResult> GetContacts()
        {
            return await _businessLayer.GetContacts();
        }


        [HttpPost("addcontact")]
        public async Task<IActionResult> CreateContact([FromForm] ContactModel contact)
        {
            return await _businessLayer.CreateContact(contact);
        }

        [HttpDelete("deletecontact/{id}")]
        public async Task<IActionResult> DeleteContact(int id)
        {
            return await _businessLayer.DeleteContact(id);
        }
    }
}
