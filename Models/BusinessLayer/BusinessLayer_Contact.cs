using Microsoft.AspNetCore.Mvc;

namespace Ecommerce_Backend.Models.BusinessLayer
{
   public partial interface IBusinessLayer
    {
        Task<IActionResult> GetContacts();
        Task<IActionResult> CreateContact([FromForm] ContactModel contact);
        Task<IActionResult> DeleteContact(int id);
    }
    public partial class BusinessLayer : IBusinessLayer
    {

        public async Task<IActionResult> GetContacts()
        {
            var contacts = await _databaseLayer.GetContacts();
            return new JsonResult(contacts);
        }
        public async Task<IActionResult> CreateContact([FromForm] ContactModel contact)
        {
            var result = await _databaseLayer.CreateContact(contact);
            return result;
        }
        public async Task<IActionResult> DeleteContact(int id)
        {
            var result = await _databaseLayer.DeleteContact(id);
            return result;
        }
    }
}
