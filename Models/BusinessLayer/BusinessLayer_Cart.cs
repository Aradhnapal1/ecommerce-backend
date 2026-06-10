using Microsoft.AspNetCore.Mvc;

namespace Ecommerce_Backend.Models.BusinessLayer
{
    public partial interface IBusinessLayer
    {
        Task<List<CartModel>> GetAllCartItems();
        Task<IActionResult> AddCartItem([FromForm] CartModel cartItem);
    }

    public partial class BusinessLayer : IBusinessLayer
    {
        public async Task<List<CartModel>> GetAllCartItems()
        {
            return await _databaseLayer.GetAllCartItems();
        }

        public async Task<IActionResult> AddCartItem(CartModel cartItem)
        {
            if (cartItem.UserId.HasValue && !string.IsNullOrEmpty(cartItem.IpAddress))
                cartItem.IpAddress = null;

            return await _databaseLayer.AddCartItem(cartItem);
        }


    }
}