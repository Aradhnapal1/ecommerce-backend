using Ecommerce_Backend.Helpers;
using Ecommerce_Backend.Models;
using Ecommerce_Backend.Services;
using Microsoft.AspNetCore.Mvc;
using Npgsql;

namespace Ecommerce_Backend.Models.DatabaseLayer
{
    public partial interface IDatabaseLayer
    {
        Task<IActionResult> BuyNow(BuyNowModel model, int userId);
        Task<IActionResult> VerifyOnlinePayment(VerifyPaymentModel model, int userId);
    }

    public partial class DataBaseLayer : IDatabaseLayer
    {
        public async Task<IActionResult> BuyNow(BuyNowModel model, int userId)
        {
            try
            {
                if (model.Quantity <= 0)
                {
                    return new BadRequestObjectResult(new { success = false, message = "Quantity must be at least 1." });
                }

                using var con = new NpgsqlConnection(DbConnection);
                await con.OpenAsync();
                using var transaction = await con.BeginTransactionAsync();

                try
                {
                    var address = await FetchUserAddressAsync(con, transaction, model.AddressId, userId);
                    if (address == null)
                    {
                        await transaction.RollbackAsync();
                        return new BadRequestObjectResult(new { success = false, message = "Address not found" });
                    }

                    var variantId = await CartHelper.ResolveVariantIdAsync(
                        con, model.ProductId, model.VariantId, model.ColorId, model.SizeId);

                    if (!variantId.HasValue && (model.ColorId.HasValue || model.SizeId.HasValue))
                    {
                        await transaction.RollbackAsync();
                        return new BadRequestObjectResult(new
                        {
                            success = false,
                            message = "No variant found for the selected color and size."
                        });
                    }

                    var lineItem = await FetchProductLineItemAsync(con, transaction, model.ProductId, variantId, model.Quantity);
                    if (lineItem == null)
                    {
                        await transaction.RollbackAsync();
                        return new BadRequestObjectResult(new { success = false, message = "Product not found" });
                    }

                    if (model.Quantity > lineItem.AvailableStock)
                    {
                        await transaction.RollbackAsync();
                        return new BadRequestObjectResult(new
                        {
                            success = false,
                            message = $"Insufficient stock. Available: {lineItem.AvailableStock}"
                        });
                    }

                    decimal subtotal = lineItem.Quantity * lineItem.Price;
                    var (discountAmount, couponId, couponCode) =
                        await ApplyCouponAsync(con, transaction, model.CouponCode, subtotal, userId);

                    if (discountAmount < 0)
                    {
                        await transaction.RollbackAsync();
                        return new BadRequestObjectResult(new { success = false, message = couponCode });
                    }

                    decimal finalAmount = subtotal - discountAmount;
                    var paymentMethod = model.PaymentMethod.Trim().ToUpperInvariant();
                    bool isOnline = paymentMethod == "ONLINE";

                    var orderNumber = "ORD" + DateTime.Now.Ticks;
                    int orderId = await InsertOrderAsync(
                        con, transaction, orderNumber, userId, address, paymentMethod,
                        "PENDING", subtotal, discountAmount, couponId, couponCode, finalAmount);

                    await InsertOrderItemAsync(con, transaction, orderId, lineItem);
                    await DeductStockAsync(con, transaction, lineItem);
                    if (couponId.HasValue)
                        await InsertCouponUsageAsync(con, transaction, couponId.Value, userId, orderId);

                    await transaction.CommitAsync();

                    object? razorpay = null;
                    if (isOnline)
                        razorpay = await TryCreateRazorpayCheckoutAsync(con, orderId, orderNumber, finalAmount);

                    if (!isOnline)
                        await TrySendOrderInvoiceAsync(con, orderId, userId, "PENDING");

                    return new OkObjectResult(new
                    {
                        success = true,
                        message = isOnline
                            ? "Order created. Complete payment to confirm."
                            : "Order placed successfully.",
                        data = new
                        {
                            orderId,
                            orderNumber,
                            finalAmount,
                            paymentMethod,
                            paymentStatus = "PENDING",
                            razorpay
                        }
                    });
                }
                catch
                {
                    await transaction.RollbackAsync();
                    throw;
                }
            }
            catch (Exception ex)
            {
                return new ObjectResult(new { success = false, message = ex.Message }) { StatusCode = 500 };
            }
        }

        public async Task<IActionResult> VerifyOnlinePayment(VerifyPaymentModel model, int userId)
        {
            try
            {
                using var con = new NpgsqlConnection(DbConnection);
                await con.OpenAsync();

                const string orderQuery = """
                    SELECT id, order_number, userid, payment_status, razorpay_order_id, final_amount
                    FROM orders
                    WHERE id = @orderid AND userid = @userid
                    LIMIT 1
                    """;

                await using var orderCmd = new NpgsqlCommand(orderQuery, con);
                orderCmd.Parameters.AddWithValue("@orderid", model.OrderId);
                orderCmd.Parameters.AddWithValue("@userid", userId);

                await using var reader = await orderCmd.ExecuteReaderAsync();
                if (!await reader.ReadAsync())
                {
                    return new NotFoundObjectResult(new { success = false, message = "Order not found." });
                }

                var paymentStatus = reader["payment_status"]?.ToString();
                var storedRazorpayOrderId = reader["razorpay_order_id"]?.ToString();
                var orderNumber = reader["order_number"]?.ToString() ?? string.Empty;
                await reader.CloseAsync();

                if (string.Equals(paymentStatus, "SUCCESS", StringComparison.OrdinalIgnoreCase))
                {
                    return new OkObjectResult(new
                    {
                        success = true,
                        message = "Payment already verified.",
                        data = new { orderId = model.OrderId, orderNumber, paymentStatus = "SUCCESS" }
                    });
                }

                if (!string.Equals(storedRazorpayOrderId, model.RazorpayOrderId, StringComparison.Ordinal))
                {
                    return new BadRequestObjectResult(new { success = false, message = "Invalid Razorpay order id." });
                }

                if (!_razorpayService.VerifyPaymentSignature(
                        model.RazorpayOrderId, model.RazorpayPaymentId, model.RazorpaySignature))
                {
                    return new BadRequestObjectResult(new { success = false, message = "Payment verification failed." });
                }

                const string updateQuery = """
                    UPDATE orders
                    SET payment_status = 'SUCCESS',
                        razorpay_payment_id = @paymentid,
                        razorpay_signature = @signature,
                        updatedat = NOW()
                    WHERE id = @orderid AND userid = @userid
                    """;

                await using var updateCmd = new NpgsqlCommand(updateQuery, con);
                updateCmd.Parameters.AddWithValue("@paymentid", model.RazorpayPaymentId);
                updateCmd.Parameters.AddWithValue("@signature", model.RazorpaySignature);
                updateCmd.Parameters.AddWithValue("@orderid", model.OrderId);
                updateCmd.Parameters.AddWithValue("@userid", userId);
                await updateCmd.ExecuteNonQueryAsync();

                await TrySendOrderInvoiceAsync(con, model.OrderId, userId, "SUCCESS");

                return new OkObjectResult(new
                {
                    success = true,
                    message = "Payment verified successfully.",
                    data = new
                    {
                        orderId = model.OrderId,
                        orderNumber,
                        paymentStatus = "SUCCESS"
                    }
                });
            }
            catch (Exception ex)
            {
                return new ObjectResult(new { success = false, message = ex.Message }) { StatusCode = 500 };
            }
        }

        private sealed class OrderLineItem
        {
            public int ProductId { get; set; }
            public int VariantId { get; set; }
            public int Quantity { get; set; }
            public decimal Price { get; set; }
            public decimal MRP { get; set; }
            public int AvailableStock { get; set; }
            public string? ProductName { get; set; }
            public string? ProductImageUrl { get; set; }
            public string? SKU { get; set; }
            public string? ColorName { get; set; }
            public string? SizeName { get; set; }
        }

        private async Task<UserAddressModel?> FetchUserAddressAsync(
            NpgsqlConnection con, NpgsqlTransaction transaction, int addressId, int userId)
        {
            const string query = "SELECT * FROM user_addresses WHERE id = @AddressId AND userid = @UserId LIMIT 1";
            await using var cmd = new NpgsqlCommand(query, con, transaction);
            cmd.Parameters.AddWithValue("@AddressId", addressId);
            cmd.Parameters.AddWithValue("@UserId", userId);

            await using var reader = await cmd.ExecuteReaderAsync();
            if (!await reader.ReadAsync()) return null;

            return new UserAddressModel
            {
                Id = (int)reader["id"],
                FullName = (string)reader["full_name"],
                Mobile = (string)reader["mobile"],
                AddressLine1 = (string)reader["address_line1"],
                AddressLine2 = reader["address_line2"] != DBNull.Value ? (string)reader["address_line2"] : null,
                Landmark = reader["landmark"] != DBNull.Value ? (string)reader["landmark"] : null,
                City = reader["city"] != DBNull.Value ? (string)reader["city"] : null,
                State = reader["state"] != DBNull.Value ? (string)reader["state"] : null,
                Country = reader["country"] != DBNull.Value ? (string)reader["country"] : null,
                Pincode = reader["pincode"] != DBNull.Value ? (string)reader["pincode"] : null
            };
        }

        private static async Task<OrderLineItem?> FetchProductLineItemAsync(
            NpgsqlConnection con, NpgsqlTransaction transaction, int productId, int? variantId, int quantity)
        {
            const string query = """
                SELECT
                    p.id AS productid,
                    COALESCE(pv.id, 0) AS variantid,
                    p.productname,
                    COALESCE(pv.saleprice, p.saleprice) AS saleprice,
                    COALESCE(pv.mrp, p.mrp) AS mrp,
                    COALESCE(pv.variantimageurl, p.productimageurl) AS imageurl,
                    COALESCE(pv.sku, p.sku) AS sku,
                    COALESCE(pv.stock, p.stock) AS available_stock,
                    c.color_name,
                    s.size_name
                FROM products p
                LEFT JOIN product_variants pv ON pv.id = @variantid AND pv.productid = p.id
                LEFT JOIN colors c ON c.id = CAST(NULLIF(COALESCE(pv.color, p.color), '') AS INTEGER)
                LEFT JOIN LATERAL (
                    SELECT sz.size_name
                    FROM sizes sz
                    WHERE sz.id = ANY(COALESCE(pv.sizes, p.sizes, ARRAY[]::int[]))
                    ORDER BY sz.id
                    LIMIT 1
                ) s ON TRUE
                WHERE p.id = @productid
                LIMIT 1
                """;

            await using var cmd = new NpgsqlCommand(query, con, transaction);
            cmd.Parameters.AddWithValue("@productid", productId);
            cmd.Parameters.AddWithValue("@variantid", variantId ?? (object)DBNull.Value);

            await using var reader = await cmd.ExecuteReaderAsync();
            if (!await reader.ReadAsync()) return null;

            return new OrderLineItem
            {
                ProductId = (int)reader["productid"],
                VariantId = Convert.ToInt32(reader["variantid"]),
                Quantity = quantity,
                Price = Convert.ToDecimal(reader["saleprice"]),
                MRP = reader["mrp"] != DBNull.Value ? Convert.ToDecimal(reader["mrp"]) : 0,
                ProductName = reader["productname"]?.ToString(),
                ProductImageUrl = reader["imageurl"]?.ToString(),
                SKU = reader["sku"]?.ToString(),
                ColorName = reader["color_name"]?.ToString(),
                SizeName = reader["size_name"]?.ToString(),
                AvailableStock = Convert.ToInt32(reader["available_stock"])
            };
        }

        private static async Task<(decimal Discount, int? CouponId, string? CouponCodeOrError)> ApplyCouponAsync(
            NpgsqlConnection con, NpgsqlTransaction transaction, string? couponCode, decimal subtotal, int userId)
        {
            if (string.IsNullOrWhiteSpace(couponCode))
                return (0, null, null);

            const string couponQuery = """
                SELECT * FROM coupons
                WHERE UPPER(coupon_code) = UPPER(@CouponCode)
                  AND is_active = true
                  AND DATE(end_date) >= CURRENT_DATE
                  AND DATE(start_date) <= CURRENT_DATE
                LIMIT 1
                """;

            int? couponId = null;
            string couponType = "";
            decimal couponValue = 0;
            decimal minOrderAmount = 0;
            int usageLimit = 0;
            bool couponFound = false;

            await using (var couponCmd = new NpgsqlCommand(couponQuery, con, transaction))
            {
                couponCmd.Parameters.AddWithValue("@CouponCode", couponCode.Trim());
                await using var couponReader = await couponCmd.ExecuteReaderAsync();
                if (await couponReader.ReadAsync())
                {
                    couponId = (int)couponReader["id"];
                    couponType = couponReader["coupon_type"].ToString()!;
                    couponValue = Convert.ToDecimal(couponReader["coupon_value"]);
                    minOrderAmount = Convert.ToDecimal(couponReader["min_order_amount"]);
                    usageLimit = Convert.ToInt32(couponReader["usage_limit"]);
                    couponFound = true;
                }
            }

            if (!couponFound)
                return (-1, null, "Invalid or expired coupon");

            if (subtotal < minOrderAmount)
                return (-1, null, $"Minimum order amount should be ₹{minOrderAmount}");

            const string usageQuery = "SELECT COUNT(*) FROM coupon_usage WHERE couponid = @CouponId AND userid = @UserId";
            await using var usageCmd = new NpgsqlCommand(usageQuery, con, transaction);
            usageCmd.Parameters.AddWithValue("@CouponId", couponId!.Value);
            usageCmd.Parameters.AddWithValue("@UserId", userId);
            int usedCount = Convert.ToInt32(await usageCmd.ExecuteScalarAsync());
            if (usedCount >= usageLimit)
                return (-1, null, "Coupon already used");

            decimal discount = couponType == "FLAT"
                ? couponValue
                : (subtotal * couponValue) / 100;

            return (discount, couponId, couponCode.Trim());
        }

        private static async Task<int> InsertOrderAsync(
            NpgsqlConnection con,
            NpgsqlTransaction transaction,
            string orderNumber,
            int userId,
            UserAddressModel address,
            string paymentMethod,
            string paymentStatus,
            decimal subtotal,
            decimal discountAmount,
            int? couponId,
            string? couponCode,
            decimal finalAmount)
        {
            const string createOrderQuery = """
                INSERT INTO orders
                (order_number, userid, addressid, full_name, mobile, address_line1,
                 address_line2, landmark, city, state, country, pincode,
                 payment_method, payment_status, order_status, subtotal,
                 discount_amount, couponid, coupon_code, final_amount,
                 createdat, updatedat)
                VALUES
                (@OrderNumber, @UserId, @AddressId, @FullName, @Mobile, @AddressLine1,
                 @AddressLine2, @Landmark, @City, @State, @Country, @Pincode,
                 @PaymentMethod, @PaymentStatus, 'PLACED', @Subtotal,
                 @DiscountAmount, @CouponId, @CouponCode, @FinalAmount,
                 NOW(), NOW())
                RETURNING id
                """;

            await using var cmd = new NpgsqlCommand(createOrderQuery, con, transaction);
            cmd.Parameters.AddWithValue("@OrderNumber", orderNumber);
            cmd.Parameters.AddWithValue("@UserId", userId);
            cmd.Parameters.AddWithValue("@AddressId", address.Id);
            cmd.Parameters.AddWithValue("@FullName", address.FullName);
            cmd.Parameters.AddWithValue("@Mobile", address.Mobile);
            cmd.Parameters.AddWithValue("@AddressLine1", address.AddressLine1);
            cmd.Parameters.AddWithValue("@AddressLine2", address.AddressLine2 ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@Landmark", address.Landmark ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@City", address.City ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@State", address.State ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@Country", address.Country ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@Pincode", address.Pincode ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@PaymentMethod", paymentMethod);
            cmd.Parameters.AddWithValue("@PaymentStatus", paymentStatus);
            cmd.Parameters.AddWithValue("@Subtotal", subtotal);
            cmd.Parameters.AddWithValue("@DiscountAmount", discountAmount);
            cmd.Parameters.AddWithValue("@CouponId", couponId ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@CouponCode", couponCode ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@FinalAmount", finalAmount);

            return (int)(await cmd.ExecuteScalarAsync())!;
        }

        private static async Task InsertOrderItemAsync(
            NpgsqlConnection con, NpgsqlTransaction transaction, int orderId, OrderLineItem item)
        {
            const string query = """
                INSERT INTO order_items
                (orderid, productid, variantid, productname, productimageurl, sku, color, size_name, quantity, mrp, saleprice, totalprice, createdat)
                VALUES
                (@OrderId, @ProductId, @VariantId, @ProductName, @ImageUrl, @Sku, @Color, @Size, @Quantity, @Mrp, @SalePrice, @TotalPrice, NOW())
                """;

            await using var cmd = new NpgsqlCommand(query, con, transaction);
            cmd.Parameters.AddWithValue("@OrderId", orderId);
            cmd.Parameters.AddWithValue("@ProductId", item.ProductId);
            cmd.Parameters.AddWithValue("@VariantId", item.VariantId > 0 ? item.VariantId : DBNull.Value);
            cmd.Parameters.AddWithValue("@ProductName", item.ProductName ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@ImageUrl", item.ProductImageUrl ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@Sku", item.SKU ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@Color", item.ColorName ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@Size", item.SizeName ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@Quantity", item.Quantity);
            cmd.Parameters.AddWithValue("@Mrp", item.MRP);
            cmd.Parameters.AddWithValue("@SalePrice", item.Price);
            cmd.Parameters.AddWithValue("@TotalPrice", item.Quantity * item.Price);
            await cmd.ExecuteNonQueryAsync();
        }

        private static async Task DeductStockAsync(
            NpgsqlConnection con, NpgsqlTransaction transaction, OrderLineItem item)
        {
            string query = item.VariantId > 0
                ? "UPDATE product_variants SET stock = stock - @Quantity WHERE id = @VariantId AND stock >= @Quantity"
                : "UPDATE products SET stock = stock - @Quantity WHERE id = @ProductId AND stock >= @Quantity";

            await using var cmd = new NpgsqlCommand(query, con, transaction);
            cmd.Parameters.AddWithValue("@Quantity", item.Quantity);
            if (item.VariantId > 0)
                cmd.Parameters.AddWithValue("@VariantId", item.VariantId);
            else
                cmd.Parameters.AddWithValue("@ProductId", item.ProductId);

            if (await cmd.ExecuteNonQueryAsync() == 0)
                throw new InvalidOperationException($"Insufficient stock for {item.ProductName}");
        }

        private static async Task InsertCouponUsageAsync(
            NpgsqlConnection con, NpgsqlTransaction transaction, int couponId, int userId, int orderId)
        {
            const string query = "INSERT INTO coupon_usage (couponid, userid, orderid) VALUES (@CouponId, @UserId, @OrderId)";
            await using var cmd = new NpgsqlCommand(query, con, transaction);
            cmd.Parameters.AddWithValue("@CouponId", couponId);
            cmd.Parameters.AddWithValue("@UserId", userId);
            cmd.Parameters.AddWithValue("@OrderId", orderId);
            await cmd.ExecuteNonQueryAsync();
        }

        private async Task<object?> TryCreateRazorpayCheckoutAsync(
            NpgsqlConnection con, int orderId, string orderNumber, decimal finalAmount)
        {
            try
            {
                var razorpayOrder = await _razorpayService.CreateOrderAsync(finalAmount, orderNumber);
                const string updateQuery = """
                    UPDATE orders SET razorpay_order_id = @RazorpayOrderId, updatedat = NOW() WHERE id = @OrderId
                    """;
                await using var cmd = new NpgsqlCommand(updateQuery, con);
                cmd.Parameters.AddWithValue("@RazorpayOrderId", razorpayOrder.Id);
                cmd.Parameters.AddWithValue("@OrderId", orderId);
                await cmd.ExecuteNonQueryAsync();

                return new
                {
                    keyId = _razorpayService.KeyId,
                    orderId = razorpayOrder.Id,
                    amount = razorpayOrder.Amount,
                    currency = razorpayOrder.Currency
                };
            }
            catch
            {
                return null;
            }
        }

        private async Task TrySendOrderInvoiceAsync(NpgsqlConnection con, int orderId, int userId, string paymentStatus)
        {
            try
            {
                const string userQuery = """
                    SELECT u.email, u.first_name, o.order_number, o.subtotal, o.discount_amount,
                           o.final_amount, o.payment_method
                    FROM orders o
                    JOIN user_register u ON u.id = o.userid
                    WHERE o.id = @OrderId AND o.userid = @UserId
                    LIMIT 1
                    """;

                await using var userCmd = new NpgsqlCommand(userQuery, con);
                userCmd.Parameters.AddWithValue("@OrderId", orderId);
                userCmd.Parameters.AddWithValue("@UserId", userId);

                string? email = null;
                string customerName = "Customer";
                string orderNumber = "";
                decimal subtotal = 0;
                decimal discount = 0;
                decimal finalAmount = 0;
                string paymentMethod = "";

                await using (var reader = await userCmd.ExecuteReaderAsync())
                {
                    if (!await reader.ReadAsync()) return;
                    email = reader["email"]?.ToString();
                    customerName = reader["first_name"]?.ToString() ?? "Customer";
                    orderNumber = reader["order_number"]?.ToString() ?? "";
                    subtotal = Convert.ToDecimal(reader["subtotal"]);
                    discount = Convert.ToDecimal(reader["discount_amount"]);
                    finalAmount = Convert.ToDecimal(reader["final_amount"]);
                    paymentMethod = reader["payment_method"]?.ToString() ?? "";
                }

                if (string.IsNullOrWhiteSpace(email)) return;

                var items = await GetOrderItems(orderId);
                await _emailService.SendOrderInvoiceEmail(email, new OrderInvoiceEmailModel
                {
                    CustomerName = customerName,
                    OrderNumber = orderNumber,
                    Subtotal = subtotal,
                    DiscountAmount = discount,
                    FinalAmount = finalAmount,
                    PaymentMethod = paymentMethod,
                    PaymentStatus = paymentStatus,
                    Items = items
                });
            }
            catch
            {
                // Do not fail API if email fails
            }
        }
    }
}
