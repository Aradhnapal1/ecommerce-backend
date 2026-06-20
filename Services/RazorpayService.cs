using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Ecommerce_Backend.Services
{
    public class RazorpayOrderResult
    {
        public string Id { get; set; } = string.Empty;
        public int Amount { get; set; }
        public string Currency { get; set; } = "INR";
    }

    public interface IRazorpayService
    {
        string KeyId { get; }
        bool IsConfigured { get; }
        Task<RazorpayOrderResult> CreateOrderAsync(decimal amountInr, string receipt);
        bool VerifyPaymentSignature(string razorpayOrderId, string razorpayPaymentId, string razorpaySignature);
    }

    public class RazorpayService : IRazorpayService
    {
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _configuration;
        private readonly ILogger<RazorpayService> _logger;

        public RazorpayService(HttpClient httpClient, IConfiguration configuration, ILogger<RazorpayService> logger)
        {
            _httpClient = httpClient;
            _configuration = configuration;
            _logger = logger;
        }

        public string KeyId => _configuration["Razorpay:KeyId"] ?? string.Empty;

        public bool IsConfigured =>
            !string.IsNullOrWhiteSpace(KeyId) &&
            !string.IsNullOrWhiteSpace(_configuration["Razorpay:KeySecret"]);

        private string KeySecret => _configuration["Razorpay:KeySecret"]
            ?? throw new InvalidOperationException("Razorpay:KeySecret is not configured.");

        private string Currency => _configuration["Razorpay:Currency"] ?? "INR";

        public async Task<RazorpayOrderResult> CreateOrderAsync(decimal amountInr, string receipt)
        {
            if (!IsConfigured)
                throw new InvalidOperationException("Razorpay is not configured. Set Razorpay:KeyId and Razorpay:KeySecret.");

            var amountPaise = (int)Math.Round(amountInr * 100, MidpointRounding.AwayFromZero);
            if (amountPaise < 100)
                throw new InvalidOperationException("Order amount must be at least ₹1.");

            var safeReceipt = receipt.Length > 40 ? receipt[..40] : receipt;

            var payload = new Dictionary<string, object>
            {
                ["amount"] = amountPaise,
                ["currency"] = Currency,
                ["receipt"] = safeReceipt,
                ["payment_capture"] = true
            };

            using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.razorpay.com/v1/orders");
            var authToken = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{KeyId}:{KeySecret}"));
            request.Headers.Authorization = new AuthenticationHeaderValue("Basic", authToken);
            request.Content = new StringContent(
                JsonSerializer.Serialize(payload),
                Encoding.UTF8,
                "application/json");

            var response = await _httpClient.SendAsync(request);
            var body = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("Razorpay create order failed ({Status}): {Body}", response.StatusCode, body);
                throw new InvalidOperationException($"Razorpay error: {body}");
            }

            using var doc = JsonDocument.Parse(body);
            return new RazorpayOrderResult
            {
                Id = doc.RootElement.GetProperty("id").GetString() ?? string.Empty,
                Amount = doc.RootElement.GetProperty("amount").GetInt32(),
                Currency = doc.RootElement.GetProperty("currency").GetString() ?? Currency
            };
        }

        public bool VerifyPaymentSignature(string razorpayOrderId, string razorpayPaymentId, string razorpaySignature)
        {
            if (string.IsNullOrWhiteSpace(razorpayOrderId) ||
                string.IsNullOrWhiteSpace(razorpayPaymentId) ||
                string.IsNullOrWhiteSpace(razorpaySignature))
            {
                return false;
            }

            try
            {
                var payload = $"{razorpayOrderId}|{razorpayPaymentId}";
                using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(KeySecret));
                var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(payload));
                var expected = Convert.ToHexString(hash).ToLowerInvariant();
                var received = razorpaySignature.Trim().ToLowerInvariant();

                return expected.Length == received.Length &&
                       CryptographicOperations.FixedTimeEquals(
                           Encoding.UTF8.GetBytes(expected),
                           Encoding.UTF8.GetBytes(received));
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Razorpay signature verification error");
                return false;
            }
        }
    }
}
