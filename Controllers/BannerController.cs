using Ecommerce_Backend.Helpers;
using Ecommerce_Backend.Models;
using Ecommerce_Backend.Models.BusinessLayer;
using Ecommerce_Backend.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Ecommerce_Backend.Controllers
{
    [ApiController]
    [Route("api/banner")]

    public class BannerController : ControllerBase
    {
        private readonly IBusinessLayer _businessLayer;
        private readonly CloudinaryService _cloudinary;
        public BannerController(IBusinessLayer businessLayer, CloudinaryService cloudinary)
        {
            _businessLayer = businessLayer;
            _cloudinary = cloudinary;
        }
        [HttpGet("get")]
        public async Task<IActionResult> GetBanner()
        {
            return await _businessLayer.GetBanner();
        }

        [HttpPost("addbanner")]
        [Authorize(Roles = AuthRoles.Admin)]
        public async Task<IActionResult> AddBanner([FromForm] BannerModel banner)
        {
            return await _businessLayer.AddBanner(banner);
        }

        [HttpPut("update/{id}")]
        [Authorize(Roles = AuthRoles.Admin)]
        public async Task<IActionResult> UpdateBanner(int id, [FromForm] BannerModel banner)
        {
            return await _businessLayer.UpdateBanner(id, banner);
        }

        [HttpDelete("delete/{id}")]
        [Authorize(Roles = AuthRoles.Admin)]
        public async Task<IActionResult> DeleteBanner(int id)
        {
            return await _businessLayer.DeleteBanner(id);
        }
    }
}
