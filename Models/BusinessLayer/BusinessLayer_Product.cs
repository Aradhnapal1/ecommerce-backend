using Microsoft.AspNetCore.Mvc;

namespace Ecommerce_Backend.Models.BusinessLayer
{
   public partial interface IBusinessLayer
    {
        Task<IActionResult> GetAllProducts();
        Task<IActionResult> AddProduct([FromForm] ProductModel product);
    }

    public partial class BusinessLayer : IBusinessLayer
    {
        
        public async Task<IActionResult> GetAllProducts()
        {
            var products = await _databaseLayer.GetAllProducts();
            return new OkObjectResult(products);
        }

        public async Task<IActionResult> AddProduct([FromForm] ProductModel product)
        {
            var result = await _databaseLayer.AddProduct(product);
            return new OkObjectResult(result);
        }
    }
}
