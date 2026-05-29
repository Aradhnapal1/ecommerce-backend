using Ecommerce_Backend.Models;
using Ecommerce_Backend.Models.DatabaseLayer;

namespace Ecommerce_Backend.Models.BusinessLayer
{
    public partial interface IBusinessLayer
    {
        Task<bool> UserRegister(UserRegisterRequest model);
        Task<bool> UserVerifyOtp(UserVerifyOtpRequest model);
    }

    public partial class BusinessLayer : IBusinessLayer
    {
        public async Task<bool> UserRegister(UserRegisterRequest model)
        {
            return await _databaseLayer.UserRegister(model);
        }

        public async Task<bool> UserVerifyOtp(UserVerifyOtpRequest model)
        {
            return await _databaseLayer.UserVerifyOtp(model);
        }
    }
}