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

        // Get All Reviews of a specific Product (Open for all)
        [HttpGet("product/{productId}")]
        public async Task<IActionResult> GetProductReviews(int productId)
        {
            return await _businessLayer.GetProductReviews(productId);
        }

        // Add Review (Only Logged-in Users)
        [HttpPost("add")]
        [Authorize]
        public async Task<IActionResult> AddReview([FromBody] AddReviewRequest request)
        {
            var userIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!int.TryParse(userIdStr, out int userId))
            {
                return Unauthorized(new { success = false, message = "Invalid User Token" });
            }

            return await _businessLayer.AddProductReview(userId, request);
        }

        // Delete Review (Only Admin)
        [HttpDelete("delete/{reviewId}")]
        [Authorize(Roles = AuthRoles.Admin)]
        public async Task<IActionResult> DeleteReview(int reviewId)
        {
            return await _businessLayer.DeleteProductReview(reviewId);
        }
    }
}