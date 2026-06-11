using Microsoft.AspNetCore.Mvc;

namespace Ecommerce_Backend.Models.BusinessLayer
{
    public partial interface IBusinessLayer
    {
        Task<IActionResult> AddAddress(
          UserAddressModel model,
          int userId
      );
        Task<IActionResult> GetAddressList(
                int userId
               );

        Task<IActionResult> DeleteAddress(int id, int userId);
    }

    public partial class BusinessLayer : IBusinessLayer
    {
        public async Task<IActionResult> AddAddress(UserAddressModel model, int userId
      )
        {
            return await _databaseLayer.AddAddress(model, userId);


        }
        public async Task<IActionResult> GetAddressList(
    int userId)
        {
            return await _databaseLayer
                .GetAddressList(userId);
        }


        public async Task<IActionResult> DeleteAddress(int id, int userId)
        {
            return await _databaseLayer.DeleteAddress(id, userId);
        }
    }

}
