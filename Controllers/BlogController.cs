using Ecommerce_Backend.Helpers;
using Ecommerce_Backend.Models;
using Ecommerce_Backend.Models.BusinessLayer;
using Ecommerce_Backend.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Ecommerce_Backend.Controllers
{
    [ApiController]
    [Route("api/blog")]
    public class BlogController : ControllerBase
    {
       private readonly IBusinessLayer _businessLayer;
       private readonly CloudinaryService _cloudinary;
        public BlogController(IBusinessLayer businessLayer, CloudinaryService cloudinary)
        {
            _businessLayer = businessLayer;
            _cloudinary = cloudinary;

        }

        [HttpGet("getblog")]
        public async Task<IActionResult> GetBlog()
        {
            return await _businessLayer.GetBlog();
        }

        [HttpPost("addblog")]
        [Authorize(Roles = AuthRoles.Admin)]
        public async Task<IActionResult> AddBlog([FromForm] BlogModel blog)
        {
            return await _businessLayer.AddBlog(blog);
        }

        [HttpPut("updateblog/{id}")]
        [Authorize(Roles = AuthRoles.Admin)]
        public async Task<IActionResult> UpdateBlog(int id, [FromForm] BlogModel blog)
        {
            return await _businessLayer.UpdateBlog(id, blog);
        }

        [HttpDelete("deleteblog/{id}")]
        [Authorize(Roles = AuthRoles.Admin)]
        public async Task<IActionResult> DeleteBlog(int id)
        {
            return await _businessLayer.DeleteBlog(id);
        }
    }
}

