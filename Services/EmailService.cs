using Ecommerce_Backend.Models;
using System.Net;
using System.Net.Mail;

namespace Ecommerce_Backend.Services
{
    public interface IEmailService
    {
        Task SendOtp(string toEmail, string otp);

        Task SendContactNotification(ContactModel contact);

        Task SendOrderConfirmationEmail(string toEmail, string orderNumber, decimal finalAmount, string paymentMethod);
        Task SendOrderInvoiceEmail(string toEmail, OrderInvoiceEmailModel invoice);
        Task SendOrderCancellationEmail(string toEmail, string orderNumber);
        Task SendPasswordResetEmail(string toEmail, string resetLink);
    }

    public class EmailService : IEmailService
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<EmailService> _logger;

        static EmailService()
        {
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
        }

        public EmailService(IConfiguration configuration, ILogger<EmailService> logger)
        {
            _configuration = configuration;
            _logger = logger;
        }

        public Task SendOtp(string toEmail, string otp) =>
            SendAsync(toEmail, "OTP Verification", $@"
                    <h2>Email Verification</h2>
                    <p>Your OTP is:</p>
                    <h1 style='color:#4A90E2; letter-spacing:4px'>{otp}</h1>
                    <p>Valid for 10 minutes. Do not share this OTP.</p>
                ");

        public async Task SendContactNotification(ContactModel contact)
        {
            var smtp = _configuration.GetSection("Smtp");
            var adminEmail = smtp["FromEmail"]
                ?? throw new InvalidOperationException("Smtp:FromEmail is not configured.");

            await SendAsync(adminEmail, "New Contact Form Submission", $@"
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
        ");
        }

        public Task SendOrderConfirmationEmail(string toEmail, string orderNumber, decimal finalAmount, string paymentMethod) =>
            SendOrderInvoiceEmail(toEmail, new OrderInvoiceEmailModel
            {
                OrderNumber = orderNumber,
                FinalAmount = finalAmount,
                PaymentMethod = paymentMethod,
                PaymentStatus = "CONFIRMED",
                Subtotal = finalAmount,
                Items = new List<OrderItemModel>()
            });

        public Task SendOrderInvoiceEmail(string toEmail, OrderInvoiceEmailModel invoice)
        {
            var rows = string.Join("", invoice.Items.Select(item => $@"
                <tr>
                    <td style='padding:8px;border-bottom:1px solid #eee'>{item.ProductName}</td>
                    <td style='padding:8px;border-bottom:1px solid #eee'>{item.ColorName ?? "-"}</td>
                    <td style='padding:8px;border-bottom:1px solid #eee'>{item.SizeName ?? "-"}</td>
                    <td style='padding:8px;border-bottom:1px solid #eee;text-align:center'>{item.Quantity}</td>
                    <td style='padding:8px;border-bottom:1px solid #eee;text-align:right'>₹{item.Price}</td>
                    <td style='padding:8px;border-bottom:1px solid #eee;text-align:right'>₹{item.Total}</td>
                </tr>"));

            var body = $@"
                <div style='font-family:Arial,sans-serif;max-width:700px;margin:0 auto;border:1px solid #ddd;padding:20px;border-radius:8px'>
                    <h2 style='color:#4A90E2'>Order Invoice</h2>
                    <p>Hi {invoice.CustomerName},</p>
                    <p>Thank you for your order. Your invoice details are below.</p>
                    <p><strong>Order Number:</strong> {invoice.OrderNumber}</p>
                    <p><strong>Payment Method:</strong> {invoice.PaymentMethod}</p>
                    <p><strong>Payment Status:</strong> {invoice.PaymentStatus}</p>
                    <table style='width:100%;border-collapse:collapse;margin-top:16px'>
                        <thead>
                            <tr style='background:#f5f5f5'>
                                <th style='padding:8px;text-align:left'>Product</th>
                                <th style='padding:8px;text-align:left'>Color</th>
                                <th style='padding:8px;text-align:left'>Size</th>
                                <th style='padding:8px;text-align:center'>Qty</th>
                                <th style='padding:8px;text-align:right'>Price</th>
                                <th style='padding:8px;text-align:right'>Total</th>
                            </tr>
                        </thead>
                        <tbody>{rows}</tbody>
                    </table>
                    <p style='margin-top:16px'><strong>Subtotal:</strong> ₹{invoice.Subtotal}</p>
                    <p><strong>Discount:</strong> ₹{invoice.DiscountAmount}</p>
                    <p style='font-size:18px'><strong>Grand Total:</strong> ₹{invoice.FinalAmount}</p>
                </div>";

            return SendAsync(toEmail, $"Order Invoice - {invoice.OrderNumber}", body);
        }

        public Task SendOrderCancellationEmail(string toEmail, string orderNumber) =>
            SendAsync(toEmail, $"Order Cancelled - {orderNumber}", $@"
                    <div style='font-family: Arial, sans-serif; max-width: 600px; margin: 0 auto; border: 1px solid #ddd; padding: 20px; border-radius: 8px;'>
                        <h2 style='color: #D0021B;'>Order Cancelled</h2>
                        <p>Hi,</p>
                        <p>Your order <strong>{orderNumber}</strong> has been successfully cancelled as per your request.</p>
                        <p style='color: #555;'>If you have already paid for this order, the refund process will be initiated shortly.</p>
                    </div>
                ");

        public Task SendPasswordResetEmail(string toEmail, string resetLink) =>
            SendAsync(toEmail, "Reset Your Password", $@"
                    <div style='font-family: Arial, sans-serif; max-width: 600px; margin: 0 auto; border: 1px solid #ddd; padding: 20px; border-radius: 8px;'>
                        <h2 style='color: #4A90E2;'>Password Reset Request</h2>
                        <p>Hi,</p>
                        <p>We received a request to reset your password. You can reset your password by clicking the link below:</p>
                        <p style='text-align: center;'>
                            <a href='{resetLink}' style='background-color: #4A90E2; color: white; padding: 10px 20px; text-decoration: none; border-radius: 5px;'>Reset Password</a>
                        </p>
                        <p>If you did not request a password reset, please ignore this email. This link is valid for 1 hour.</p>
                    </div>");

        private async Task SendAsync(string toEmail, string subject, string htmlBody)
        {
            var smtp = _configuration.GetSection("Smtp");
            var host = smtp["Host"];
            var username = smtp["Username"];
            var password = smtp["Password"]?.Replace(" ", "");
            var fromEmail = smtp["FromEmail"];
            var fromName = smtp["FromName"] ?? "Ecommerce App";

            if (string.IsNullOrWhiteSpace(host) || string.IsNullOrWhiteSpace(username) ||
                string.IsNullOrWhiteSpace(password) || string.IsNullOrWhiteSpace(fromEmail))
            {
                throw new InvalidOperationException(
                    "SMTP is not configured. Set Smtp:Host, Username, Password, and FromEmail in appsettings.Development.json.");
            }

            var port = int.Parse(smtp["Port"] ?? "587");
            var enableSsl = bool.Parse(smtp["EnableSsl"] ?? "true");

            using var client = new SmtpClient(host, port)
            {
                Credentials = new NetworkCredential(username, password),
                EnableSsl = enableSsl,
                DeliveryMethod = SmtpDeliveryMethod.Network,
                UseDefaultCredentials = false,
                Timeout = 30000
            };

            using var mail = new MailMessage
            {
                From = new MailAddress(fromEmail, fromName),
                Subject = subject,
                Body = htmlBody,
                IsBodyHtml = true
            };
            mail.To.Add(toEmail);

            try
            {
                await client.SendMailAsync(mail);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send email to {Email} via {Host}:{Port}", toEmail, host, port);
                throw;
            }
        }
    }
}
