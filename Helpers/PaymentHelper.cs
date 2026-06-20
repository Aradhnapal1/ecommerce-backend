namespace Ecommerce_Backend.Helpers
{
    public static class PaymentHelper
    {
        private static readonly HashSet<string> OnlineMethods = new(StringComparer.OrdinalIgnoreCase)
        {
            "ONLINE", "RAZORPAY", "UPI", "CARD", "CREDIT", "DEBIT",
            "NETBANKING", "WALLET", "PAYTM", "PHONEPE", "GPAY"
        };

        public static bool IsOnlinePayment(string? paymentMethod)
        {
            if (string.IsNullOrWhiteSpace(paymentMethod))
                return false;

            return OnlineMethods.Contains(paymentMethod.Trim());
        }

        public static string NormalizePaymentMethod(string? paymentMethod)
        {
            if (string.IsNullOrWhiteSpace(paymentMethod))
                return "COD";

            return IsOnlinePayment(paymentMethod) ? "ONLINE" : paymentMethod.Trim().ToUpperInvariant();
        }
    }
}
