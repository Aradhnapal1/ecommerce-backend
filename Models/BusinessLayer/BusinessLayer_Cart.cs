using Microsoft.AspNetCore.Mvc;

namespace Ecommerce_Backend.Models.BusinessLayer
{
    public partial interface IBusinessLayer
    {
        Task<List<CartResponseModel>> GetAllCartItems();
        Task<IActionResult> AddCartItem(CartModel cartItem);
    }

    public partial class BusinessLayer : IBusinessLayer
    {
        public async Task<List<CartResponseModel>> GetAllCartItems()
        {
            return await _databaseLayer.GetAllCartItems();
        }

        public async Task<IActionResult> AddCartItem(CartModel cartItem)
        {
            return await _databaseLayer.AddCartItem(cartItem);
        }


    }
}