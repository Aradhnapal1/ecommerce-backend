using Microsoft.AspNetCore.Mvc;

namespace Ecommerce_Backend.Models.BusinessLayer
{
    public partial interface IBusinessLayer
    {
        Task<IActionResult> AddCompare([FromForm] CompareModel compare);
        Task<IActionResult> GetCompareList(int? userId, string? ipAddress);
        Task<IActionResult> CompareDelete(int id, int? userId, string? ipAddress);
        Task<IActionResult> ClearCompare(int? userId, string? ipAddress);
        Task<IActionResult> CompareProductsByIds(int[] productIds);
        Task MergeGuestCompareToUser(int userId, string ipAddress);
    }

    public partial class BusinessLayer : IBusinessLayer
    {
        public Task<IActionResult> AddCompare([FromForm] CompareModel compare) =>
            _databaseLayer.AddCompare(compare);

        public Task<IActionResult> GetCompareList(int? userId, string? ipAddress) =>
            _databaseLayer.GetCompareList(userId, ipAddress);

        public Task<IActionResult> CompareDelete(int id, int? userId, string? ipAddress) =>
            _databaseLayer.CompareDelete(id, userId, ipAddress);

        public Task<IActionResult> ClearCompare(int? userId, string? ipAddress) =>
            _databaseLayer.ClearCompare(userId, ipAddress);

        public Task<IActionResult> CompareProductsByIds(int[] productIds) =>
            _databaseLayer.CompareProductsByIds(productIds);

        public Task MergeGuestCompareToUser(int userId, string ipAddress) =>
            _databaseLayer.MergeGuestCompareToUser(userId, ipAddress);
    }
}
