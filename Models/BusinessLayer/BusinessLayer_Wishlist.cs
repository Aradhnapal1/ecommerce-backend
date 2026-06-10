using Microsoft.AspNetCore.Mvc;

namespace Ecommerce_Backend.Models.BusinessLayer
{
    public partial interface IBusinessLayer
    {
        Task<IActionResult> AddWishlist(
            [FromForm] WishlistModel wishlist
        );
         Task<IActionResult> GetWishlist();
         Task<IActionResult> WishlistDelete(int id);
    }

    public partial class BusinessLayer : IBusinessLayer
    {
        public async Task<IActionResult> AddWishlist(
            [FromForm] WishlistModel wishlist)
        {
            return await _databaseLayer
                .AddWishlist(wishlist);
        }
        public async  Task<IActionResult> GetWishlist()
        {
            var result = await _databaseLayer.GetWishlist();
            return result;
        }
        public async Task<IActionResult> WishlistDelete(int id)
        {
            var result = await _databaseLayer.WishlistDelete(id);
            return result;
        }
    }

    }
