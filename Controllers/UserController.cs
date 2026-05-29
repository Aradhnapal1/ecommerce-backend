using Ecommerce_Backend.Models;
using Ecommerce_Backend.Models.BusinessLayer;
using Microsoft.AspNetCore.Mvc;

namespace Ecommerce_Backend.Controllers
{
    [ApiController]
    [Route("api/user")]
    public class UserController : ControllerBase
    {
        private readonly IBusinessLayer _businessLayer;

        public UserController(IBusinessLayer businessLayer)
        {
            _businessLayer = businessLayer;
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
    }
}