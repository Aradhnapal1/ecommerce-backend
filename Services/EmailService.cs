using Ecommerce_Backend.Models;
using System.Net;
using System.Net.Mail;

namespace Ecommerce_Backend.Services
{
    public interface IEmailService
    {
        Task SendOtp(string toEmail, string otp);

        Task SendContactNotification(ContactModel contact);

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



        public async Task SendContactNotification(ContactModel contact)
        {
            var smtp = _configuration.GetSection("Smtp");
            var client = new SmtpClient(smtp["Host"])
            {
                Port = int.Parse(smtp["Port"]!),
                Credentials = new NetworkCredential(smtp["Username"], smtp["Password"]),
                EnableSsl = true,
                DeliveryMethod = SmtpDeliveryMethod.Network,
                UseDefaultCredentials = false
            };

            var mail = new MailMessage
            {
                From = new MailAddress(smtp["FromEmail"]!, smtp["FromName"]),
                Subject = "New Contact Form Submission",
                Body = $@"
            <h2>New Contact Received</h2>
            <table style='border-collapse:collapse;width:100%'>
                <tr><td style='padding:8px;font-weight:bold'>Name</td>
                    <td style='padding:8px'>{contact.FirstName} {contact.LastName}</td></tr>
                <tr style='background:#f5f5f5'><td style='padding:8px;font-weight:bold'>Email</td>
                    <td style='padding:8px'>{contact.Email}</td></tr>
                <tr><td style='padding:8px;font-weight:bold'>Phone</td>
                    <td style='padding:8px'>{contact.PhoneNumber}</td></tr>
                <tr style='background:#f5f5f5'><td style='padding:8px;font-weight:bold'>Message</td>
                    <td style='padding:8px'>{contact.Message}</td></tr>
                <tr><td style='padding:8px;font-weight:bold'>Submitted At</td>
                    <td style='padding:8px'>{contact.CreatedAt:dd MMM yyyy, hh:mm tt}</td></tr>
            </table>
        ",
                IsBodyHtml = true
            };

            // Notification jaayegi admin ke email pe (appsettings se)
            mail.To.Add(smtp["FromEmail"]!);

            await client.SendMailAsync(mail);
            client.Dispose();
        }
    }
}