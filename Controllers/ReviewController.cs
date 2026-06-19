using Ecommerce_Backend.Models;
using Ecommerce_Backend.Models.BusinessLayer;
using Ecommerce_Backend.Helpers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using System.Threading.Tasks;

namespace Ecommerce_Backend.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ReviewController : ControllerBase
    {
        private readonly IBusinessLayer _businessLayer;

        public ReviewController(IBusinessLayer businessLayer)
        {
            _businessLayer = businessLayer;
        }

        [HttpGet("product/{productId}")]
        public async Task<IActionResult> GetProductReviews(int productId)
        {
            return await _businessLayer.GetProductReviews(productId);
        }

        [HttpPost("add")]
        [Authorize]
        public async Task<IActionResult> AddReview([FromBody] AddReviewRequest request)
        {
            var userId = UserContextHelper.GetUserId(User);
            if (!userId.HasValue)
            {
                return Unauthorized(new { success = false, message = "Invalid user token." });
            }

            return await _businessLayer.AddProductReview(userId.Value, request);
        }

        [HttpDelete("delete/{reviewId}")]
        [Authorize]
        public async Task<IActionResult> DeleteReview(int reviewId)
        {
            var userId = UserContextHelper.GetUserId(User);
            var isAdmin = UserContextHelper.IsAdmin(User);

            return await _businessLayer.DeleteProductReview(reviewId, userId, isAdmin);
        }
    }
}
