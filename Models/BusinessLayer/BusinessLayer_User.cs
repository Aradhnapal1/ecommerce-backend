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
        Task<IActionResult> ForgotPassword(ForgotPasswordRequest model);
        Task<IActionResult> ResetPassword(ResetPasswordRequest model);
        Task<IActionResult> ChangePassword(int userId, ChangePasswordRequest model);
        Task<IActionResult> UpdateProfile(int userId, UpdateProfileRequest model);
        Task<IActionResult> GetUserProfile(int userId);
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

        public async Task<IActionResult> ForgotPassword(ForgotPasswordRequest model)
        {
            if (string.IsNullOrWhiteSpace(model.Email))
            {
                return new BadRequestObjectResult(new { success = false, message = "Email is required." });
            }
            return await _databaseLayer.ForgotPassword(model.Email);
        }

        public async Task<IActionResult> ResetPassword(ResetPasswordRequest model)
        {
            if (string.IsNullOrWhiteSpace(model.Email) || string.IsNullOrWhiteSpace(model.NewPassword))
            {
                return new BadRequestObjectResult(new { success = false, message = "Email, Token and new password are required." });
            }
            if (model.NewPassword.Length < 6)
            {
                return new BadRequestObjectResult(new { success = false, message = "Password must be at least 6 characters long." });
            }
            return await _databaseLayer.ResetPassword(model);
        }

        public async Task<IActionResult> ChangePassword(int userId, ChangePasswordRequest model)
        {
            if (string.IsNullOrWhiteSpace(model.OldPassword) || string.IsNullOrWhiteSpace(model.NewPassword))
            {
                return new BadRequestObjectResult(new { success = false, message = "Old password and new password are required." });
            }
            if (model.NewPassword.Length < 6)
            {
                return new BadRequestObjectResult(new { success = false, message = "New password must be at least 6 characters long." });
            }

            return await _databaseLayer.ChangePassword(userId, model);
        }

        public async Task<IActionResult> UpdateProfile(int userId, UpdateProfileRequest model)
        {
            if (userId <= 0)
            {
                return new BadRequestObjectResult(new { success = false, message = "Invalid user ID." });
            }

            return await _databaseLayer.UpdateProfile(userId, model);
        }

        public async Task<IActionResult> GetUserProfile(int userId)
        {
            if (userId <= 0)
            {
                return new BadRequestObjectResult(new { success = false, message = "Invalid user ID." });
            }
            return await _databaseLayer.GetUserProfile(userId);
        }
    }
}