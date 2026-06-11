using Microsoft.AspNetCore.Mvc;

namespace Ecommerce_Backend.Models.BusinessLayer
{
    public partial interface IBusinessLayer
    {
        Task<IActionResult> AddCart(
            [FromForm] AddCartModel cart
        );
        Task<IActionResult> GetCart(int? userId, string? ipAddress);
        Task<IActionResult> UpdateCartQuantity(
    UpdateCartQuantityModel model
);



    }
    public partial class BusinessLayer : IBusinessLayer
    {
        public async Task<IActionResult> AddCart(
            [FromForm] AddCartModel cart)
        {
            return await _databaseLayer
                .AddCart(cart);
        }

        public async Task<IActionResult> GetCart(
    int? userId,
    string? ipAddress)
        {
            return await _databaseLayer
                .GetCart(
                    userId,
                    ipAddress
                );
        }

        // Implementation
        public async Task<IActionResult> UpdateCartQuantity(
            UpdateCartQuantityModel model)
        {
            return await _databaseLayer
                .UpdateCartQuantity(model);
        }


    }
}
