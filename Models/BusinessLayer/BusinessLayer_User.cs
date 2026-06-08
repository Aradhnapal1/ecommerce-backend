using Ecommerce_Backend.Models;
using Ecommerce_Backend.Models.DatabaseLayer;
using Microsoft.AspNetCore.Mvc;

namespace Ecommerce_Backend.Models.BusinessLayer
{
    public partial interface IBusinessLayer
    {
        Task<bool> UserRegister(UserRegisterRequest model);
        Task<bool> UserVerifyOtp(UserVerifyOtpRequest model);
        Task<UserLoginResponse?> UserLogin(UserLoginRequest model);
        Task<UserLoginResponse?> GetUserById(int id);
        Task<List<UserLoginResponse>> GetAllUsers();
        Task<IActionResult> DeleteUser(int id);
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

        public async Task<UserLoginResponse?> UserLogin(UserLoginRequest model)
        {
            return await _databaseLayer.UserLogin(model);
        }


        public async Task<UserLoginResponse?> GetUserById(int id)
        { 
            return await _databaseLayer.GetUserById(id);
        }

        public async Task<List<UserLoginResponse>> GetAllUsers()
        {
            return await _databaseLayer.GetAllUsers();
        }

        public async Task<IActionResult> DeleteUser(int id)
        {
            return await _databaseLayer.DeleteUser(id);
        }
    }
}