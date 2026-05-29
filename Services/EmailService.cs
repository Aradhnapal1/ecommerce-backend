using System.Net;
using System.Net.Mail;

namespace Ecommerce_Backend.Services
{
    public interface IEmailService
    {
        Task SendOtp(string toEmail, string otp);
    }

    public class EmailService : IEmailService
    {
        private readonly IConfiguration _configuration;

        public EmailService(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public async Task SendOtp(string toEmail, string otp)
        {
            var smtp = _configuration.GetSection("Smtp");

            var client = new SmtpClient(smtp["Host"])
            {
                Port = int.Parse(smtp["Port"]!),
                Credentials = new NetworkCredential(
                                            smtp["Username"],
                                            smtp["Password"]),
                EnableSsl = true,
                DeliveryMethod = SmtpDeliveryMethod.Network,
                UseDefaultCredentials = false   // ✅ ye important hai
            };

            var mail = new MailMessage
            {
                From = new MailAddress(smtp["FromEmail"]!, smtp["FromName"]),
                Subject = "OTP Verification",
                Body = $@"
                    <h2>Email Verification</h2>
                    <p>Your OTP is:</p>
                    <h1 style='color:#4A90E2; letter-spacing:4px'>{otp}</h1>
                    <p>Valid for 10 minutes. Do not share this OTP.</p>
                ",
                IsBodyHtml = true
            };

            mail.To.Add(toEmail);
            await client.SendMailAsync(mail);
            client.Dispose();
        }
    }
}