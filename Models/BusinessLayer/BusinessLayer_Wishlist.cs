using Microsoft.AspNetCore.Mvc;

namespace Ecommerce_Backend.Models.BusinessLayer
{
    public partial interface IBusinessLayer
    {
        Task<IActionResult> AddWishlist(
            [FromForm] WishlistModel wishlist
        );
         Task<IActionResult> GetWishlist(int? userId, string? ipAddress);
         Task<IActionResult> WishlistDelete(int id, int? userId, string? ipAddress);
         Task MergeGuestWishlistToUser(int userId, string ipAddress);
    }

    public partial class BusinessLayer : IBusinessLayer
    {
        public async Task<IActionResult> AddWishlist(
            [FromForm] WishlistModel wishlist)
        {
            return await _databaseLayer
                .AddWishlist(wishlist);
        }

        public async Task<IActionResult> GetWishlist(int? userId, string? ipAddress)
        {
            return await _databaseLayer.GetWishlist(userId, ipAddress);
        }

        public async Task<IActionResult> WishlistDelete(int id, int? userId, string? ipAddress)
        {
            return await _databaseLayer.WishlistDelete(id, userId, ipAddress);
        }

        public Task MergeGuestWishlistToUser(int userId, string ipAddress) =>
            _databaseLayer.MergeGuestWishlistToUser(userId, ipAddress);
    }
}
