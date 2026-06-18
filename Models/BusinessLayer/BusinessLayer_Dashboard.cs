using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;

namespace Ecommerce_Backend.Models.BusinessLayer
{
    public partial interface IBusinessLayer
    {
        Task<IActionResult> GetAdminDashboardStats();
    }

    public partial class BusinessLayer : IBusinessLayer
    {
        public async Task<IActionResult> GetAdminDashboardStats()
        {
            return await _databaseLayer.GetAdminDashboardStats();
        }
    }
}