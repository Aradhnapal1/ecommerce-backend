using Microsoft.AspNetCore.Mvc;

namespace Ecommerce_Backend.Models.BusinessLayer
{
    //ytrfedgbhnjk
   public partial interface IBusinessLayer
    {
        Task<IActionResult> GetAllProducts();
        Task<IActionResult> GetFilteredProducts(ProductFilterRequest filter);
        Task<IActionResult> SearchProducts(string query, ProductFilterRequest? filter);
        Task<IActionResult> AddProduct([FromForm] ProductModel product);
        Task<IActionResult> UpdateProduct(int id, [FromForm] ProductModel product);
        Task<IActionResult> DeleteProduct(int id);
        Task<ProductModel?> GetProductById(int id);
        Task<ProductModel?> GetProductBySlug(string slug);
        Task<ProductDetailModel?> GetProductDetail(int? id, string? slug);
        Task<IActionResult> GetRelatedProducts(int productId, int limit);
        Task<IActionResult> GetProductsByCategorySlug(string categorySlug, ProductFilterRequest? filter);
        Task<IActionResult> GetProductsByBrandSlug(string brandSlug, ProductFilterRequest? filter);
        Task<IActionResult> GetProductVariants(int productId);
        Task<IActionResult> GetTopDiscountedProducts();

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
                    categoryIds = filter.ResolvedCategoryIds,
                    categorySlugs = filter.ResolvedCategorySlugs,
                    brandIds = filter.ResolvedBrandIds,
                    brandSlugs = filter.ResolvedBrandSlugs,
                    colorIds = filter.ResolvedColorIds,
                    colorSlugs = filter.ResolvedColorSlugs,
                    sizeIds = filter.ResolvedSizeIds,
                    sizeSlugs = filter.ResolvedSizeSlugs,
                    discountPercents = filter.ResolvedDiscountPercents,
                    filter.MinPrice,
                    filter.MaxPrice,
                    filter.HasDiscount,
                    filter.InStock,
                    search = filter.ResolvedSearch,
                    filter.SortBy,
                    filter.UseGlobalSearch
                },
                data = products
            });
        }

        public async Task<IActionResult> SearchProducts(
            string query,
            ProductFilterRequest? filter)
        {
            if (string.IsNullOrWhiteSpace(query))
            {
                return new BadRequestObjectResult(new
                {
                    success = false,
                    message = "Search query 'q' is required"
                });
            }

            filter ??= new ProductFilterRequest();
            filter.Q = query.Trim();
            filter.UseGlobalSearch = true;

            return await GetFilteredProducts(filter);
        }

        public async Task<IActionResult> GetTopDiscountedProducts()
        {
            var products = await _databaseLayer.GetTopDiscountedProducts(9);
            return new OkObjectResult(new
            {
                success = true,
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

        public async Task<ProductModel?> GetProductBySlug(string slug)
        {
            return await _databaseLayer.GetProductBySlug(slug);
        }

        public async Task<ProductDetailModel?> GetProductDetail(int? id, string? slug)
        {
            return await _databaseLayer.GetProductDetail(id, slug);
        }

        public async Task<IActionResult> GetRelatedProducts(int productId, int limit)
        {
            if (productId <= 0)
            {
                return new BadRequestObjectResult(new
                {
                    success = false,
                    message = "Invalid product ID"
                });
            }

            if (limit < 1) limit = 8;
            if (limit > 24) limit = 24;

            var products = await _databaseLayer.GetRelatedProducts(productId, limit);
            return new OkObjectResult(new
            {
                success = true,
                count = products.Count,
                data = products
            });
        }

        public async Task<IActionResult> GetProductsByCategorySlug(string categorySlug, ProductFilterRequest? filter)
        {
            if (string.IsNullOrWhiteSpace(categorySlug))
            {
                return new BadRequestObjectResult(new
                {
                    success = false,
                    message = "Category slug is required"
                });
            }

            filter ??= new ProductFilterRequest();
            filter.CategorySlug = categorySlug.Trim();
            return await GetFilteredProducts(filter);
        }

        public async Task<IActionResult> GetProductsByBrandSlug(string brandSlug, ProductFilterRequest? filter)
        {
            if (string.IsNullOrWhiteSpace(brandSlug))
            {
                return new BadRequestObjectResult(new
                {
                    success = false,
                    message = "Brand slug is required"
                });
            }

            filter ??= new ProductFilterRequest();
            filter.BrandSlug = brandSlug.Trim();
            return await GetFilteredProducts(filter);
        }

        public async Task<IActionResult> GetProductVariants(int productId)
        {
            if (productId <= 0)
            {
                return new BadRequestObjectResult(new
                {
                    success = false,
                    message = "Invalid product ID"
                });
            }

            var variants = await _databaseLayer.GetVariantsByProductId(productId);
            return new OkObjectResult(new
            {
                success = true,
                count = variants.Count,
                data = variants
            });
        }
    }
}
