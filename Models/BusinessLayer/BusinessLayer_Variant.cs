using Microsoft.AspNetCore.Mvc;

namespace Ecommerce_Backend.Models.BusinessLayer
{
   public partial interface IBusinessLayer
    {
        Task<List<ProductVariantModel>> GetAllVariants();
        Task<object> AddVariant([FromForm] ProductVariantModel variant);
        Task<object> UpdateVariant(int id, [FromForm] ProductVariantModel variant);
        Task<object> DeleteVariant(int id);
        Task<object> GetVariantById(int id);
        Task<ProductVariantModel?> GetVariantBySlug(string slug);
    }
    public partial class BusinessLayer : IBusinessLayer
    {
        public async Task<List<ProductVariantModel>> GetAllVariants()
        {
            return await _databaseLayer.GetAllVariants();
        }

        public async Task<object> AddVariant(ProductVariantModel variant)
        {
            return await _databaseLayer.AddVariant(variant);
        }

        public async Task<object> UpdateVariant(int id, ProductVariantModel variant)
        {
            return await _databaseLayer.UpdateVariant(id, variant);


        }
        public async Task<object> DeleteVariant(int id)
        {
            return await _databaseLayer.DeleteVariant(id);
        }

        public async Task<object> GetVariantById(int id)
        {
            return await _databaseLayer.GetVariantById(id);
        }

        public async Task<ProductVariantModel?> GetVariantBySlug(string slug)
        {
            return await _databaseLayer.GetVariantBySlug(slug);
        }
    }


}
