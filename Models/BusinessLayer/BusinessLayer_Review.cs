using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;

namespace Ecommerce_Backend.Models.BusinessLayer
{
    public partial interface IBusinessLayer
    {
        Task<IActionResult> AddProductReview(int userId, AddReviewRequest request);
        Task<IActionResult> GetProductReviews(int productId);
        Task<IActionResult> DeleteProductReview(int reviewId, int? userId, bool isAdmin);
    }

    public partial class BusinessLayer
    {
        public async Task<IActionResult> AddProductReview(int userId, AddReviewRequest request)
        {
            return await _databaseLayer.AddProductReview(userId, request);
        }

        public async Task<IActionResult> GetProductReviews(int productId)
        {
            return await _databaseLayer.GetProductReviews(productId);
        }

        public async Task<IActionResult> DeleteProductReview(int reviewId, int? userId, bool isAdmin)
        {
            return await _databaseLayer.DeleteProductReview(reviewId, userId, isAdmin);
        }
    }
}
