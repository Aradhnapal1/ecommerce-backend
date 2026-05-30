using Microsoft.AspNetCore.Http;
namespace Ecommerce_Backend.Models

{
    public class UserRegisterRequest
    {
        public string? first_name { get; set; }
        public string? last_name { get; set; }
        public string? email { get; set; }
        public string? phone_number { get; set; }
        public string? password { get; set; }
        public string? role { get; set; }
    }

    public class UserVerifyOtpRequest
    {
        public string? email { get; set; }
        public string? otp { get; set; }
    }

    public class UserLoginRequest
    {
        public string? email { get; set; }
        public string? password { get; set; }
    }

    public class UserLoginResponse
    {
        public int id { get; set; }
        public string? first_name { get; set; }
        public string? last_name { get; set; }
        public string? email { get; set; }
        public string? phone_number { get; set; }
        public string? role { get; set; }
        public string? token { get; set; }
    }



    public class SizeModel
    {
        public int Id { get; set; }
        public string? SizeName { get; set; }
        public DateTime CreatedAt { get; set; }
        public bool IsActive { get; set; } = true;
    }

    public class ColorResponse
    {
        public int Id { get; set; }

        public string ColorName { get; set; }

        public bool Status { get; set; }

        public DateTime CreatedAt { get; set; }

    }


    public class BrandModel
    {
        public int Id { get; set; }
        public string? BrandName { get; set; }
        public string? BrandImg { get; set; }
        public bool IsActive { get; set; }

        public IFormFile? BrandFile { get; set; }
    }

    public class CloudinarySettings
    {
        public string CloudName { get; set; } = string.Empty;
        public string ApiKey { get; set; } = string.Empty;
        public string ApiSecret { get; set; } = string.Empty;
    }
}
