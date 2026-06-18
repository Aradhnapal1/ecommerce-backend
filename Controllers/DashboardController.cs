using Ecommerce_Backend.Models.BusinessLayer;
using Ecommerce_Backend.Helpers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;

namespace Ecommerce_Backend.Controllers
{
    [Route("api/admin/dashboard")]
    [ApiController]
    [Authorize(Roles = AuthRoles.Admin)] // Secure for Admins only
    public class DashboardController : ControllerBase
    {
        private readonly IBusinessLayer _businessLayer;

        public DashboardController(IBusinessLayer businessLayer)
        {
            _businessLayer = businessLayer;
        }

        // GET: api/admin/Dashboard/stats
        [HttpGet("stats")]
        public async Task<IActionResult> GetDashboardStats()
        {
            return await _businessLayer.GetAdminDashboardStats();
        }
    }
}
