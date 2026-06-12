using Ecommerce_Backend.Models.DatabaseLayer;
using Microsoft.AspNetCore.Mvc;

namespace Ecommerce_Backend.Models.BusinessLayer
{
    public partial interface IBusinessLayer
    {
        Task<List<SizeModel>> GetAllSizes();
        Task<IActionResult> AddSize([FromForm] SizeModel size);
        Task<IActionResult> UpdateSize(int id, [FromForm] SizeModel size);
        Task<IActionResult> DeleteSize(int id);
    }

    public partial class BusinessLayer : IBusinessLayer
    {

        public async Task<List<SizeModel>> GetAllSizes()
        {
            return await _databaseLayer.GetAllSizes();
        }

        public async Task<IActionResult> AddSize(SizeModel size)
        {
            return await _databaseLayer.AddSize(size);
        }

        public async Task<IActionResult> UpdateSize(int id, [FromForm] SizeModel size)
        {
            if (string.IsNullOrEmpty(size.SizeName))
            {
                return new BadRequestObjectResult("Size name is required.");
            }
            return await _databaseLayer.UpdateSize(id, size);
        }

        public async Task<IActionResult> DeleteSize(int id)
        {
            return await _databaseLayer.DeleteSize(id);
        }
    }
}