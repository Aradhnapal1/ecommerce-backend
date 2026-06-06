using Ecommerce_Backend.Models;
using Ecommerce_Backend.Models.BusinessLayer;
using Microsoft.AspNetCore.Mvc;
using Ecommerce_Backend.Services;

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

        // POST api/user/register
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

            // Role check
            var allowedRoles = new[] { "ADMIN", "USER" };

            if (!allowedRoles.Contains(model.role.ToUpper()))
                return BadRequest(new { message = "Role must be ADMIN or USER" });

            var result = await _businessLayer.UserRegister(model);
            if (!result)
                return BadRequest(new { message = "Email already exists or invalid role" });

            return Ok(new { message = "OTP sent to your email. Please verify to complete registration." });
        }

        // POST api/user/verify-otp
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






        // POST api/user/login
        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] UserLoginRequest model)
        {
            if (string.IsNullOrWhiteSpace(model.email) ||
                string.IsNullOrWhiteSpace(model.password))
                return BadRequest(new { message = "Email and password are required" });

            var user = await _businessLayer.UserLogin(model);
            if (user == null)
                return Unauthorized(new { message = "Invalid credentials or account not verified" });

            // ✅ JWT Token generate karo
            string token = _jwtService.GenerateToken(user);
            user.token = token;

            return Ok(new
            {
                message = "Login successful",
                token
            });
        }



        // GET api/user/get-all
        [HttpGet("get-all")]
        public async Task<IActionResult> GetAllUsers()
        {
            var users = await _businessLayer.GetAllUsers();
            if (users == null || users.Count == 0)
                return NotFound(new { message = "No users found" });

            return Ok(new
            {
                message = "Users fetched successfully",
                total = users.Count,
                users = users.Select(u => new {u.id, u.first_name, u.last_name, u.email, u.phone_number, u.role }).ToList()
            });
        }

        // GET api/user/get/{id}
        [HttpGet("get/{id}")]
        public async Task<IActionResult> GetUserById(int id)
        {
            var user = await _businessLayer.GetUserById(id);
            if (user == null)
                return NotFound(new { message = "User not found" });

            return Ok(new
            {
                message = "User fetched successfully",
                user
            });
        }
    }
}