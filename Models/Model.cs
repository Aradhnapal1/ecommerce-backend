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


}
