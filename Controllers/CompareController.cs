using Ecommerce_Backend.Helpers;
using Ecommerce_Backend.Models;
using Ecommerce_Backend.Models.BusinessLayer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Ecommerce_Backend.Controllers
{
    [ApiController]
    [Route("api/compare")]
    [AllowAnonymous]
    public class CompareController : ControllerBase
    {
        private readonly IBusinessLayer _businessLayer;

        public CompareController(IBusinessLayer businessLayer)
        {
            _businessLayer = businessLayer;
        }

        [HttpPost("add")]
        [HttpPost("add-compare")]
        public async Task<IActionResult> AddCompare([FromForm] CompareModel compare)
        {
            var userId = UserContextHelper.GetUserId(User);
            var ipAddress = UserContextHelper.GetClientIp(HttpContext);

            if (!userId.HasValue && string.IsNullOrWhiteSpace(ipAddress))
            {
                return BadRequest(new
                {
                    success = false,
                    message = "Could not identify guest session. IP address is required."
                });
            }

            compare.UserId = userId;
            compare.IpAddress = ipAddress;

            return await _businessLayer.AddCompare(compare);
        }

        [HttpGet("get")]
        [HttpGet("get-compare")]
        public async Task<IActionResult> GetCompareList()
        {
            var userId = UserContextHelper.GetUserId(User);
            var ipAddress = userId.HasValue ? null : UserContextHelper.GetClientIp(HttpContext);

            if (!userId.HasValue && string.IsNullOrWhiteSpace(ipAddress))
            {
                return Ok(new { success = true, count = 0, maxAllowed = CompareHelper.MaxCompareItems, data = Array.Empty<object>() });
            }

            return await _businessLayer.GetCompareList(userId, ipAddress);
        }

        /// <summary>Compare products directly by ids (max 4) without saving to list.</summary>
        [HttpGet("products")]
        public async Task<IActionResult> CompareByProductIds([FromQuery] string productIds)
        {
            if (string.IsNullOrWhiteSpace(productIds))
            {
                return BadRequest(new { success = false, message = "productIds query is required (e.g. 1,2,3)" });
            }

            var ids = productIds
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(s => int.TryParse(s, out var id) ? id : 0)
                .Where(id => id > 0)
                .Distinct()
                .ToArray();

            return await _businessLayer.CompareProductsByIds(ids);
        }

        [HttpDelete("delete/{id}")]
        [HttpDelete("delete-compare/{id}")]
        public async Task<IActionResult> CompareDelete(int id)
        {
            var userId = UserContextHelper.GetUserId(User);
            var ipAddress = userId.HasValue ? null : UserContextHelper.GetClientIp(HttpContext);

            return await _businessLayer.CompareDelete(id, userId, ipAddress);
        }

        [HttpDelete("clear")]
        public async Task<IActionResult> ClearCompare()
        {
            var userId = UserContextHelper.GetUserId(User);
            var ipAddress = userId.HasValue ? null : UserContextHelper.GetClientIp(HttpContext);

            return await _businessLayer.ClearCompare(userId, ipAddress);
        }
    }
}
