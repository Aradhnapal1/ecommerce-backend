using Microsoft.AspNetCore.Mvc;

namespace Ecommerce_Backend.Models.BusinessLayer
{
    public partial interface IBusinessLayer
    {
        Task<IActionResult> GetBlog();
        Task<IActionResult> AddBlog([FromForm] BlogModel blog);
        Task<IActionResult> UpdateBlog(int id, [FromForm] BlogModel blog);
        Task<IActionResult> DeleteBlog(int id);
    }

    public partial class BusinessLayer : IBusinessLayer
    {
        public async Task<IActionResult> GetBlog()
        {
            var result = await _databaseLayer.GetBlog();
            return result;
        }

        public async Task<IActionResult> AddBlog([FromForm] BlogModel blog)
        {
            var result = await _databaseLayer.AddBlog(blog);
            return result;
        }

        public async Task<IActionResult> UpdateBlog(int id, [FromForm] BlogModel blog)
        {
            var result = await _databaseLayer.UpdateBlog(id, blog);
            return result;
        }

        public async Task<IActionResult> DeleteBlog(int id)
        {
            var result = await _databaseLayer.DeleteBlog(id);
            return result;
        }
    }
}
