using Microsoft.AspNetCore.Mvc;

namespace Ecommerce_Backend.Models.BusinessLayer
{
    //ytrfed
   public partial interface IBusinessLayer
    {
        Task<IActionResult> GetAllProducts();
        Task<IActionResult> AddProduct([FromForm] ProductModel product);
        Task<IActionResult> UpdateProduct(int id, [FromForm] ProductModel product);
        Task<IActionResult> DeleteProduct(int id);
        Task<ProductModel?> GetProductById(int id);
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

        public async Task<IActionResult> UpdateProduct(int id, [FromForm] ProductModel product)
        {
            var result = await _databaseLayer.UpdateProduct(id, product);
            return new OkObjectResult(result);
        }

        public async Task<IActionResult> DeleteProduct(int id)
        {
            var result = await _databaseLayer.DeleteProduct(id);
            return new OkObjectResult(result);
        }

        public async Task<ProductModel?> GetProductById(int id)
        {
            return await _databaseLayer.GetProductById(id);
        }
    }
}
