using Microsoft.AspNetCore.Mvc;

namespace Ecommerce_Backend.Models.BusinessLayer
{
   public partial interface IBusinessLayer
    {
        Task<IActionResult> CreateOrder(CreateOrderModel model);
    }

    public partial class BusinessLayer : IBusinessLayer
    {
        public async Task<IActionResult> CreateOrder(CreateOrderModel model)
        {
            return await _databaseLayer
                .CreateOrder(model);
        }
    }
}
