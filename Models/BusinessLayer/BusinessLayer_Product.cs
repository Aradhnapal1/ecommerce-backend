using Microsoft.AspNetCore.Mvc;

namespace Ecommerce_Backend.Models.BusinessLayer
{
   public partial interface IBusinessLayer
    {
        Task<IActionResult> GetAllProducts();
        Task<IActionResult> GetFilteredProducts(ProductFilterRequest filter);
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

        public async Task<IActionResult> GetFilteredProducts(ProductFilterRequest filter)
        {
            if (filter.Page < 1) filter.Page = 1;
            if (filter.PageSize < 1) filter.PageSize = 20;
            if (filter.PageSize > 100) filter.PageSize = 100;

            var (products, total) = await _databaseLayer.GetFilteredProducts(filter);

            return new OkObjectResult(new
            {
                success = true,
                total,
                page = filter.Page,
                pageSize = filter.PageSize,
                totalPages = (int)Math.Ceiling(total / (double)filter.PageSize),
                filters = new
                {
                    filter.CategoryId,
                    filter.BrandId,
                    filter.ColorId,
                    filter.SizeId,
                    filter.MinPrice,
                    filter.MaxPrice,
                    filter.MinDiscount,
                    filter.HasDiscount,
                    filter.InStock,
                    filter.Search,
                    filter.SortBy
                },
                data = products
            });
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
