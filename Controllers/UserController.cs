using Ecommerce_Backend.Helpers;
using Ecommerce_Backend.Models;
using Ecommerce_Backend.Models.BusinessLayer;
using Ecommerce_Backend.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Ecommerce_Backend.Controllers
{
    [ApiController]
    [Route("api/user")]
    public class UserController : ControllerBase
    {
        private readonly IBusinessLayer _businessLayer;
        private readonly IJwtService _jwtService;

        public UserController(IBusinessLayer businessLayer, IJwtService jwtService)
        {
            _businessLayer = businessLayer;
            _jwtService = jwtService;
        }

        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] UserRegisterRequest model)
        {
            if (string.IsNullOrWhiteSpace(model.first_name) ||
                string.IsNullOrWhiteSpace(model.last_name) ||
                string.IsNullOrWhiteSpace(model.email) ||
                string.IsNullOrWhiteSpace(model.phone_number) ||
                string.IsNullOrWhiteSpace(model.password) ||
                string.IsNullOrWhiteSpace(model.role))
                return BadRequest(new { message = "All fields are required" });

            if (!string.Equals(model.role, AuthRoles.User, StringComparison.OrdinalIgnoreCase))
                return BadRequest(new { message = "Only USER role is allowed for self-registration" });

            var result = await _businessLayer.UserRegister(model);
            if (!result)
                return BadRequest(new { message = "Email already exists or invalid role" });

            return Ok(new { message = "OTP sent to your email. Please verify to complete registration." });
        }

        [HttpPost("verify-otp")]
        public async Task<IActionResult> VerifyOtp([FromBody] UserVerifyOtpRequest model)
        {
            if (string.IsNullOrWhiteSpace(model.email) ||
                string.IsNullOrWhiteSpace(model.otp))
                return BadRequest(new { message = "Email and OTP are required" });

            var result = await _businessLayer.UserVerifyOtp(model);
            if (!result)
                return BadRequest(new { message = "Invalid or expired OTP" });

            return Ok(new { message = "OTP verified. Registration complete!" });
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] UserLoginRequest model)
        {
            if (string.IsNullOrWhiteSpace(model.email) ||
                string.IsNullOrWhiteSpace(model.password))
                return BadRequest(new { message = "Email and password are required" });

            var user = await _businessLayer.UserLogin(model);
            if (user == null)
                return Unauthorized(new { message = "Invalid credentials or account not verified" });

            string token = _jwtService.GenerateToken(user);

            return Ok(new
            {
                message = "Login successful",
                token
            });
        }

        [HttpGet("get-all")]
        [Authorize(Roles = AuthRoles.Admin)]
        public async Task<IActionResult> GetAllUsers()
        {
            var users = await _businessLayer.GetAllUsers();
            if (users == null || users.Count == 0)
                return NotFound(new { message = "No users found" });

            return Ok(new
            {
                message = "Users fetched successfully",
                total = users.Count,
                users = users.Select(u => new { u.id, u.first_name, u.last_name, u.email, u.phone_number, u.role }).ToList()
            });
        }

        [HttpGet("get/{id}")]
        [Authorize]
        public async Task<IActionResult> GetUserById(int id)
        {
            var currentUserId = UserContextHelper.GetUserId(User);
            if (!UserContextHelper.IsAdmin(User) && currentUserId != id)
                return Forbid();

            var user = await _businessLayer.GetUserById(id);
            if (user == null)
                return NotFound(new { message = "User not found" });

            return Ok(new
            {
                message = "User fetched successfully",
                user
            });
        }

        [HttpDelete("delete/{id}")]
        [Authorize(Roles = AuthRoles.Admin)]
        public async Task<IActionResult> DeleteUser(int id)
        {
            return await _businessLayer.DeleteUser(id);
        }

        [HttpPost("forgot-password")]
        [AllowAnonymous]
        public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordRequest model)
        {
            return await _businessLayer.ForgotPassword(model);
        }

        [HttpPost("reset-password")]
        [AllowAnonymous]
        public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordRequest model)
        {
            return await _businessLayer.ResetPassword(model);
        }

        [HttpPost("change-password")]
        [Authorize]
        public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest model)
        {
            var userId = UserContextHelper.GetUserId(User)!.Value;
            return await _businessLayer.ChangePassword(userId, model);
        }

        [HttpGet("profile")]
        [Authorize]
        public async Task<IActionResult> GetProfile()
        {
            var userId = UserContextHelper.GetUserId(User)!.Value;
            return await GetUserById(userId);
        }

        [HttpPut("update-profile")]
        [Authorize]
        public async Task<IActionResult> UpdateProfile([FromForm] UpdateProfileRequest model)
        {
            var userId = UserContextHelper.GetUserId(User)!.Value;
            return await _businessLayer.UpdateProfile(userId, model);
        }
    }
}
