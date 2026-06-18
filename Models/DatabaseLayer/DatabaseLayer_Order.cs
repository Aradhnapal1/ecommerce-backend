using Microsoft.AspNetCore.Mvc;
using Npgsql;
using NpgsqlTypes;
using System;

namespace Ecommerce_Backend.Models.DatabaseLayer
{
    public partial interface IDatabaseLayer
    {
        Task<IActionResult> CreateOrder(CreateOrderModel model, int userId);
        Task<List<OrderDetailsModel>> GetAllOrders(int userId, bool isAdmin);
        Task<OrderDetailsModel?> GetOrderById(int orderId, int userId, bool isAdmin);
        Task<IActionResult> UpdateOrderStatus(int orderId, string status);
        Task<IActionResult> UpdatePaymentStatus(int orderId, string status);
        Task<IActionResult> CancelOrder(int orderId, int userId);
        Task<IActionResult> RequestReturn(int orderId, int userId, string reason);
        Task<IActionResult> ProcessRefund(int orderId, string action);
        Task<List<OrderItemModel>> GetOrderItems(int orderId);
    }

    public partial class DataBaseLayer : IDatabaseLayer
    {
        public async Task<IActionResult> CreateOrder(CreateOrderModel model, int userId)
        {
            try
            {
                using var con = new NpgsqlConnection(DbConnection);
                await con.OpenAsync();

                using var transaction = await con.BeginTransactionAsync();

                try
                {
                    // Step 1: Address Fetch
                    var addressQuery = "SELECT * FROM user_addresses WHERE id = @AddressId AND userid = @UserId LIMIT 1";
                    UserAddressModel? address = null;
                    
                    using (var addressCmd = new NpgsqlCommand(addressQuery, con))
                    {
                        addressCmd.Parameters.AddWithValue("@AddressId", model.AddressId);
                        addressCmd.Parameters.AddWithValue("@UserId", userId);
                        addressCmd.Transaction = transaction;

                        using var addressReader = await addressCmd.ExecuteReaderAsync();
                        if (await addressReader.ReadAsync())
                        {
                            address = new UserAddressModel
                            {
                                Id = (int)addressReader["id"],
                                FullName = (string)addressReader["full_name"],
                                Mobile = (string)addressReader["mobile"],
                                AddressLine1 = (string)addressReader["address_line1"],
                                AddressLine2 = addressReader["address_line2"] != DBNull.Value ? (string)addressReader["address_line2"] : null,
                                Landmark = addressReader["landmark"] != DBNull.Value ? (string)addressReader["landmark"] : null,
                                City = addressReader["city"] != DBNull.Value ? (string)addressReader["city"] : null,
                                State = addressReader["state"] != DBNull.Value ? (string)addressReader["state"] : null,
                                Country = addressReader["country"] != DBNull.Value ? (string)addressReader["country"] : null,
                                Pincode = addressReader["pincode"] != DBNull.Value ? (string)addressReader["pincode"] : null
                            };
                        }
                    }

                    if (address == null)
                    {
                        await transaction.RollbackAsync();
                        return new BadRequestObjectResult(new
                        {
                            success = false,
                            message = "Address not found"
                        });
                    }

                    // Step 2: Cart Fetch
                    var cartItems = new List<CartItemModel>();
                    decimal subtotal = 0;

                    var cartQuery = @"
                        SELECT 
                            ac.id, ac.userid, ac.productid, ac.variantid, ac.quantity, 
                            p.productname,
                            COALESCE(pv.saleprice, p.saleprice) AS saleprice,
                            COALESCE(pv.mrp, p.mrp) AS mrp,
                            COALESCE(pv.variantimageurl, p.productimageurl) AS imageurl,
                            COALESCE(pv.sku, p.sku) AS sku,
                            COALESCE(pv.stock, p.stock) AS available_stock,
                            c.color_name,
                            s.size_name
                        FROM addcart ac
                        JOIN products p ON p.id = ac.productid
                        LEFT JOIN product_variants pv ON pv.id = ac.variantid
                        LEFT JOIN colors c ON c.id = CAST(NULLIF(COALESCE(pv.color, p.color), '') AS INTEGER)
                        LEFT JOIN sizes s ON s.id = CAST((COALESCE(pv.sizes, p.sizes))[1] AS INTEGER)
                        WHERE ac.userid = @UserId";

                    using (var cartCmd = new NpgsqlCommand(cartQuery, con))
                    {
                        cartCmd.Parameters.AddWithValue("@UserId", userId);
                        cartCmd.Transaction = transaction;

                        using var cartReader = await cartCmd.ExecuteReaderAsync();
                        while (await cartReader.ReadAsync())
                        {
                            var quantity = (int)cartReader["quantity"];
                            var price = (decimal)cartReader["saleprice"];
                            var availableStock = Convert.ToInt32(cartReader["available_stock"]);
                            var itemTotal = quantity * price;

                            if (quantity > availableStock)
                            {
                                await transaction.RollbackAsync();
                                return new BadRequestObjectResult(new
                                {
                                    success = false,
                                    message = $"Insufficient stock for {cartReader["productname"]}. Available: {availableStock}"
                                });
                            }

                            cartItems.Add(new CartItemModel
                            {
                                Id = (int)cartReader["id"],
                                UserId = (int)cartReader["userid"],
                                ProductId = (int)cartReader["productid"],
                                VariantId = cartReader["variantid"] != DBNull.Value ? (int)cartReader["variantid"] : 0,
                                Quantity = quantity,
                                Price = price,
                                MRP = cartReader["mrp"] != DBNull.Value ? Convert.ToDecimal(cartReader["mrp"]) : 0,
                                ProductName = cartReader["productname"]?.ToString(),
                                ProductImageUrl = cartReader["imageurl"]?.ToString(),
                                SKU = cartReader["sku"]?.ToString(),
                                ColorName = cartReader["color_name"]?.ToString(),
                                SizeName = cartReader["size_name"]?.ToString()
                            });

                            subtotal += itemTotal;
                        }
                    }

                    if (cartItems.Count == 0)
                    {
                        await transaction.RollbackAsync();
                        return new BadRequestObjectResult(new
                        {
                            success = false,
                            message = "Cart is empty"
                        });
                    }

                    // Step 3: Coupon Validate
                    decimal discountAmount = 0;
                    int? couponId = null;
                    string? couponCode = model.CouponCode;

                    if (!string.IsNullOrEmpty(model.CouponCode))
                    {
                        string couponType = "";
                        decimal couponValue = 0;
                        decimal minOrderAmount = 0;
                        int usageLimit = 0;
                        bool couponFound = false;

                        var couponQuery = @"
                            SELECT * FROM coupons 
                            WHERE UPPER(coupon_code) = UPPER(@CouponCode) 
                            AND is_active = true 
                            AND DATE(end_date) >= CURRENT_DATE 
                            AND DATE(start_date) <= CURRENT_DATE
                            LIMIT 1";

                        using (var couponCmd = new NpgsqlCommand(couponQuery, con))
                        {
                            couponCmd.Parameters.AddWithValue("@CouponCode", model.CouponCode.Trim());
                            couponCmd.Transaction = transaction;

                            using var couponReader = await couponCmd.ExecuteReaderAsync();
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

                        if (couponFound)
                        {
                            // Min Amount check
                            if (subtotal < minOrderAmount)
                            {
                                await transaction.RollbackAsync();
                                return new BadRequestObjectResult(new { success = false, message = $"Minimum order amount should be ₹{minOrderAmount}" });
                            }

                            // Usage Limit Check
                            var usageQuery = "SELECT COUNT(*) FROM coupon_usage WHERE couponid = @CouponId AND userid = @UserId";
                            using (var usageCmd = new NpgsqlCommand(usageQuery, con))
                            {
                                usageCmd.Transaction = transaction;
                                usageCmd.Parameters.AddWithValue("@CouponId", couponId.Value);
                                usageCmd.Parameters.AddWithValue("@UserId", userId);
                                int usedCount = Convert.ToInt32(await usageCmd.ExecuteScalarAsync());

                                if (usedCount >= usageLimit)
                                {
                                    await transaction.RollbackAsync();
                                    return new BadRequestObjectResult(new { success = false, message = "Coupon already used" });
                                }
                            }

                            // Calculate discount
                            if (couponType == "FLAT")
                            {
                                discountAmount = couponValue;
                            }
                            else
                            {
                                discountAmount = (subtotal * couponValue) / 100;
                            }
                        }
                        else
                        {
                            await transaction.RollbackAsync();
                            return new BadRequestObjectResult(new
                            {
                                success = false,
                                message = "Invalid or expired coupon"
                            });
                        }
                    }

                    // Step 4: Calculate Final Amount
                    decimal finalAmount = subtotal - discountAmount;

                    string paymentStatus = model.PaymentMethod.Equals("COD", StringComparison.OrdinalIgnoreCase) ? "PENDING" : "SUCCESS";

                    // Step 5: Create Order
                    string orderNumber = "ORD" + DateTime.Now.Ticks;
                    int orderId = 0;

                    var createOrderQuery = @"
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
                        RETURNING id";

                    using (var createOrderCmd = new NpgsqlCommand(createOrderQuery, con))
                    {
                        createOrderCmd.Parameters.AddWithValue("@OrderNumber", orderNumber);
                        createOrderCmd.Parameters.AddWithValue("@UserId", userId);
                        createOrderCmd.Parameters.AddWithValue("@AddressId", model.AddressId);
                        createOrderCmd.Parameters.AddWithValue("@FullName", address.FullName);
                        createOrderCmd.Parameters.AddWithValue("@Mobile", address.Mobile);
                        createOrderCmd.Parameters.AddWithValue("@AddressLine1", address.AddressLine1);
                        createOrderCmd.Parameters.AddWithValue("@AddressLine2", address.AddressLine2 ?? (object)DBNull.Value);
                        createOrderCmd.Parameters.AddWithValue("@Landmark", address.Landmark ?? (object)DBNull.Value);
                        createOrderCmd.Parameters.AddWithValue("@City", address.City ?? (object)DBNull.Value);
                        createOrderCmd.Parameters.AddWithValue("@State", address.State ?? (object)DBNull.Value);
                        createOrderCmd.Parameters.AddWithValue("@Country", address.Country ?? (object)DBNull.Value);
                        createOrderCmd.Parameters.AddWithValue("@Pincode", address.Pincode ?? (object)DBNull.Value);
                        createOrderCmd.Parameters.AddWithValue("@PaymentMethod", model.PaymentMethod);
                        createOrderCmd.Parameters.AddWithValue("@PaymentStatus", paymentStatus);
                        createOrderCmd.Parameters.AddWithValue("@Subtotal", subtotal);
                        createOrderCmd.Parameters.AddWithValue("@DiscountAmount", discountAmount);
                        createOrderCmd.Parameters.AddWithValue("@CouponId", couponId ?? (object)DBNull.Value);
                        createOrderCmd.Parameters.AddWithValue("@CouponCode", couponCode ?? (object)DBNull.Value);
                        createOrderCmd.Parameters.AddWithValue("@FinalAmount", finalAmount);
                        createOrderCmd.Transaction = transaction;

                        orderId = (int)await createOrderCmd.ExecuteScalarAsync();
                    }

                    // Step 6: Create Order Items
                    foreach (var item in cartItems)
                    {
                        var lineTotal = item.Quantity * item.Price;

                        var createItemQuery = @"
                            INSERT INTO order_items 
                            (orderid, productid, variantid, productname, productimageurl, sku, color, size_name, quantity, mrp, saleprice, totalprice, createdat)
                            VALUES 
                            (@OrderId, @ProductId, @VariantId, @ProductName, @ImageUrl, @Sku, @Color, @Size, @Quantity, @Mrp, @SalePrice, @TotalPrice, NOW())";

                        using (var itemCmd = new NpgsqlCommand(createItemQuery, con))
                        {
                            itemCmd.Parameters.AddWithValue("@OrderId", orderId);
                            itemCmd.Parameters.AddWithValue("@ProductId", item.ProductId);
                            itemCmd.Parameters.AddWithValue("@VariantId", item.VariantId > 0 ? (object)item.VariantId : DBNull.Value);
                            itemCmd.Parameters.AddWithValue("@ProductName", item.ProductName ?? (object)DBNull.Value);
                            itemCmd.Parameters.AddWithValue("@ImageUrl", item.ProductImageUrl ?? (object)DBNull.Value);
                            itemCmd.Parameters.AddWithValue("@Sku", item.SKU ?? (object)DBNull.Value);
                            itemCmd.Parameters.AddWithValue("@Color", item.ColorName ?? (object)DBNull.Value);
                            itemCmd.Parameters.AddWithValue("@Size", item.SizeName ?? (object)DBNull.Value);
                            itemCmd.Parameters.AddWithValue("@Quantity", item.Quantity);
                            itemCmd.Parameters.AddWithValue("@Mrp", item.MRP);
                            itemCmd.Parameters.AddWithValue("@SalePrice", item.Price);
                            itemCmd.Parameters.AddWithValue("@TotalPrice", lineTotal);
                            itemCmd.Transaction = transaction;

                            await itemCmd.ExecuteNonQueryAsync();
                        }

                        // Step 8: Stock Deduct
                        string deductStockQuery;
                        if (item.VariantId > 0)
                        {
                            deductStockQuery = @"
                                UPDATE product_variants 
                                SET stock = stock - @Quantity 
                                WHERE id = @VariantId AND stock >= @Quantity";
                        }
                        else
                        {
                            deductStockQuery = @"
                                UPDATE products 
                                SET stock = stock - @Quantity 
                                WHERE id = @ProductId AND stock >= @Quantity";
                        }

                        using (var deductCmd = new NpgsqlCommand(deductStockQuery, con))
                        {
                            deductCmd.Parameters.AddWithValue("@Quantity", item.Quantity);
                            if (item.VariantId > 0)
                                deductCmd.Parameters.AddWithValue("@VariantId", item.VariantId);
                            else
                                deductCmd.Parameters.AddWithValue("@ProductId", item.ProductId);
                            deductCmd.Transaction = transaction;

                            var rowsAffected = await deductCmd.ExecuteNonQueryAsync();
                            if (rowsAffected == 0)
                            {
                                await transaction.RollbackAsync();
                                return new BadRequestObjectResult(new
                                {
                                    success = false,
                                    message = $"Insufficient stock for {item.ProductName}"
                                });
                            }
                        }
                    }

                    // Step 7: Coupon Usage (mark coupon as used)
                    if (couponId.HasValue)
                    {
                        var couponUsageQuery = @"
                            INSERT INTO coupon_usage 
                            (couponid, userid, orderid)
                            VALUES 
                            (@CouponId, @UserId, @OrderId)";

                        using (var couponUsageCmd = new NpgsqlCommand(couponUsageQuery, con))
                        {
                            couponUsageCmd.Parameters.AddWithValue("@CouponId", couponId.Value);
                            couponUsageCmd.Parameters.AddWithValue("@UserId", userId);
                            couponUsageCmd.Parameters.AddWithValue("@OrderId", orderId);
                            couponUsageCmd.Transaction = transaction;

                            await couponUsageCmd.ExecuteNonQueryAsync();
                        }
                    }

                    // Step 9: Clear Cart
                    var clearCartQuery = "DELETE FROM addcart WHERE userid = @UserId";
                    using (var clearCmd = new NpgsqlCommand(clearCartQuery, con))
                    {
                        clearCmd.Parameters.AddWithValue("@UserId", userId);
                        clearCmd.Transaction = transaction;

                        await clearCmd.ExecuteNonQueryAsync();
                    }

                    // Commit Transaction
                    await transaction.CommitAsync();

                    // Step 10: Send Order Confirmation Email
                    try
                    {
                        var emailQuery = "SELECT email FROM user_register WHERE id = @UserId LIMIT 1";
                        using var emailCmd = new NpgsqlCommand(emailQuery, con);
                        emailCmd.Parameters.AddWithValue("@UserId", userId);
                        var userEmail = (string?)await emailCmd.ExecuteScalarAsync();

                        if (!string.IsNullOrEmpty(userEmail))
                        {
                            await _emailService.SendOrderConfirmationEmail(userEmail, orderNumber, finalAmount, model.PaymentMethod);
                        }
                    }
                    catch
                    {
                        // Ignore email errors to prevent order cancellation
                    }

                    return new OkObjectResult(new
                    {
                        success = true,
                        message = "Order created successfully",
                        data = new
                        {
                            orderId = orderId,
                            orderNumber = orderNumber,
                            finalAmount = finalAmount,
                            paymentMethod = model.PaymentMethod
                        }
                    });
                }
                catch (Exception )
                {
                    await transaction.RollbackAsync();
                    throw;
                }       
            }
            catch (Exception ex)
            {
                return new ObjectResult(new
                {
                    success = false,
                    message = ex.Message
                })
                {
                    StatusCode = 500
                };
            }
        }

        public async Task<List<OrderDetailsModel>> GetAllOrders(int userId, bool isAdmin)
        {
            var orders = new List<OrderDetailsModel>();

            try
            {
                using var con = new NpgsqlConnection(DbConnection);
                await con.OpenAsync();

                var query = isAdmin
                    ? @"SELECT * FROM orders ORDER BY createdat DESC"
                    : @"SELECT * FROM orders WHERE userid = @UserId ORDER BY createdat DESC";

                using var cmd = new NpgsqlCommand(query, con);
                if (!isAdmin)
                    cmd.Parameters.AddWithValue("@UserId", userId);

                using var reader = await cmd.ExecuteReaderAsync();

                while (await reader.ReadAsync())
                {
                    orders.Add(new OrderDetailsModel
                    {
                        Id = (int)reader["id"],
                        OrderNumber = (string)reader["order_number"],
                        UserId = (int)reader["userid"],
                        AddressId = (int)reader["addressid"],
                        FullName = (string)reader["full_name"],
                        Mobile = (string)reader["mobile"],
                        PaymentMethod = (string)reader["payment_method"],
                        PaymentStatus = (string)reader["payment_status"],
                        OrderStatus = (string)reader["order_status"],
                        Subtotal = (decimal)reader["subtotal"],
                        DiscountAmount = (decimal)reader["discount_amount"],
                        FinalAmount = (decimal)reader["final_amount"],
                        CouponCode = reader["coupon_code"] != DBNull.Value ? (string)reader["coupon_code"] : null,
                        CreatedAt = (DateTime)reader["createdat"]
                    });
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Error fetching orders: {ex.Message}");
            }

            return orders;
        }

        public async Task<OrderDetailsModel?> GetOrderById(int orderId, int userId, bool isAdmin)
        {
            try
            {
                using var con = new NpgsqlConnection(DbConnection);
                await con.OpenAsync();

                var query = isAdmin
                    ? "SELECT * FROM orders WHERE id = @OrderId LIMIT 1"
                    : "SELECT * FROM orders WHERE id = @OrderId AND userid = @UserId LIMIT 1";

                using var cmd = new NpgsqlCommand(query, con);
                cmd.Parameters.AddWithValue("@OrderId", orderId);
                if (!isAdmin)
                    cmd.Parameters.AddWithValue("@UserId", userId);

                using var reader = await cmd.ExecuteReaderAsync();
                if (await reader.ReadAsync())
                {
                    return new OrderDetailsModel
                    {
                        Id = (int)reader["id"],
                        OrderNumber = (string)reader["order_number"],
                        UserId = (int)reader["userid"],
                        AddressId = (int)reader["addressid"],
                        FullName = (string)reader["full_name"],
                        Mobile = (string)reader["mobile"],
                        PaymentMethod = (string)reader["payment_method"],
                        PaymentStatus = (string)reader["payment_status"],
                        OrderStatus = (string)reader["order_status"],
                        Subtotal = (decimal)reader["subtotal"],
                        DiscountAmount = (decimal)reader["discount_amount"],
                        FinalAmount = (decimal)reader["final_amount"],
                        CouponCode = reader["coupon_code"] != DBNull.Value ? (string)reader["coupon_code"] : null,
                        CreatedAt = (DateTime)reader["createdat"]
                    };
                }

                return null;
            }
            catch (Exception ex)
            {
                throw new Exception($"Error fetching order: {ex.Message}");
            }
        }

        public async Task<IActionResult> UpdateOrderStatus(int orderId, string status)
        {
            try
            {
                using var con = new NpgsqlConnection(DbConnection);
                await con.OpenAsync();

                var query = @"
                    UPDATE orders 
                    SET order_status = @Status, updatedat = NOW()
                    WHERE id = @OrderId";

                using var cmd = new NpgsqlCommand(query, con);
                cmd.Parameters.AddWithValue("@Status", status);
                cmd.Parameters.AddWithValue("@OrderId", orderId);

                int rowsAffected = await cmd.ExecuteNonQueryAsync();

                if (rowsAffected > 0)
                {
                    return new OkObjectResult(new
                    {
                        success = true,
                        message = "Order status updated successfully"
                    });
                }

                return new BadRequestObjectResult(new
                {
                    success = false,
                    message = "Order not found"
                });
            }
            catch (Exception ex)
            {
                return new ObjectResult(new
                {
                    success = false,
                    message = ex.Message
                })
                {
                    StatusCode = 500
                };
            }
        }

        public async Task<IActionResult> UpdatePaymentStatus(int orderId, string status)
        {
            try
            {
                using var con = new NpgsqlConnection(DbConnection);
                await con.OpenAsync();

                var query = @"
                    UPDATE orders 
                    SET payment_status = @Status, updatedat = NOW()
                    WHERE id = @OrderId";

                using var cmd = new NpgsqlCommand(query, con);
                cmd.Parameters.AddWithValue("@Status", status);
                cmd.Parameters.AddWithValue("@OrderId", orderId);

                int rowsAffected = await cmd.ExecuteNonQueryAsync();

                if (rowsAffected > 0)
                {
                    return new OkObjectResult(new
                    {
                        success = true,
                        message = "Payment status updated successfully"
                    });
                }

                return new BadRequestObjectResult(new
                {
                    success = false,
                    message = "Order not found"
                });
            }
            catch (Exception ex)
            {
                return new ObjectResult(new
                {
                    success = false,
                    message = ex.Message
                })
                {
                    StatusCode = 500
                };
            }
        }

        public async Task<IActionResult> CancelOrder(int orderId, int userId)
        {
            try
            {
                using var con = new NpgsqlConnection(DbConnection);
                await con.OpenAsync();

                var checkQuery = @"
                    SELECT o.order_status, o.order_number, u.email 
                    FROM orders o
                    JOIN user_register u ON o.userid = u.id
                    WHERE o.id = @OrderId AND o.userid = @UserId LIMIT 1";

                using var checkCmd = new NpgsqlCommand(checkQuery, con);
                checkCmd.Parameters.AddWithValue("@OrderId", orderId);
                checkCmd.Parameters.AddWithValue("@UserId", userId);

                string? status = null;
                string? orderNumber = null;
                string? userEmail = null;
                using (var reader = await checkCmd.ExecuteReaderAsync())
                {
                    if (await reader.ReadAsync()) {
                        status = reader["order_status"]?.ToString();
                        orderNumber = reader["order_number"]?.ToString();
                        userEmail = reader["email"]?.ToString();
                    }
                }

                if (status == null)
                {
                    return new BadRequestObjectResult(new { success = false, message = "Order not found or access denied" });
                }

                if (status.Equals("SHIPPED", StringComparison.OrdinalIgnoreCase) || 
                    status.Equals("DELIVERED", StringComparison.OrdinalIgnoreCase) || 
                    status.Equals("CANCELLED", StringComparison.OrdinalIgnoreCase))
                {
                    return new BadRequestObjectResult(new { success = false, message = "Order cannot be cancelled at this stage" });
                }

                var updateQuery = "UPDATE orders SET order_status = 'CANCELLED', updatedat = NOW() WHERE id = @OrderId AND userid = @UserId";
                using var updateCmd = new NpgsqlCommand(updateQuery, con);
                updateCmd.Parameters.AddWithValue("@OrderId", orderId);
                updateCmd.Parameters.AddWithValue("@UserId", userId);

                await updateCmd.ExecuteNonQueryAsync();

                // Send cancellation email to user
                try
                {
                    if (!string.IsNullOrEmpty(userEmail) && !string.IsNullOrEmpty(orderNumber))
                    {
                        await _emailService.SendOrderCancellationEmail(userEmail, orderNumber);
                    }
                }
                catch
                {
                    // Ignore email sending error to avoid API failure if email service is down
                }

                return new OkObjectResult(new { success = true, message = "Order cancelled successfully" });
            }
            catch (Exception ex)
            {
                return new ObjectResult(new { success = false, message = ex.Message }) { StatusCode = 500 };
            }
        }

        public async Task<IActionResult> RequestReturn(int orderId, int userId, string reason)
        {
            try
            {
                using var con = new NpgsqlConnection(DbConnection);
                await con.OpenAsync();

                var checkQuery = "SELECT order_status FROM orders WHERE id = @OrderId AND userid = @UserId LIMIT 1";
                using var checkCmd = new NpgsqlCommand(checkQuery, con);
                checkCmd.Parameters.AddWithValue("@OrderId", orderId);
                checkCmd.Parameters.AddWithValue("@UserId", userId);

                var status = (string?)await checkCmd.ExecuteScalarAsync();

                if (status == null)
                {
                    return new BadRequestObjectResult(new { success = false, message = "Order not found or access denied" });
                }

                if (!status.Equals("DELIVERED", StringComparison.OrdinalIgnoreCase))
                {
                    return new BadRequestObjectResult(new { success = false, message = "Only delivered orders can be returned" });
                }

                var updateQuery = "UPDATE orders SET order_status = 'RETURN_REQUESTED', return_reason = @Reason, updatedat = NOW() WHERE id = @OrderId AND userid = @UserId";
                using var updateCmd = new NpgsqlCommand(updateQuery, con);
                updateCmd.Parameters.AddWithValue("@Reason", reason);
                updateCmd.Parameters.AddWithValue("@OrderId", orderId);
                updateCmd.Parameters.AddWithValue("@UserId", userId);

                await updateCmd.ExecuteNonQueryAsync();

                return new OkObjectResult(new { success = true, message = "Return request submitted successfully" });
            }
            catch (Exception ex)
            {
                return new ObjectResult(new { success = false, message = ex.Message }) { StatusCode = 500 };
            }
        }

        public async Task<IActionResult> ProcessRefund(int orderId, string action)
        {
            try
            {
                using var con = new NpgsqlConnection(DbConnection);
                await con.OpenAsync();

                var checkQuery = "SELECT order_status FROM orders WHERE id = @OrderId LIMIT 1";
                using var checkCmd = new NpgsqlCommand(checkQuery, con);
                checkCmd.Parameters.AddWithValue("@OrderId", orderId);

                var status = (string?)await checkCmd.ExecuteScalarAsync();

                if (status == null)
                {
                    return new BadRequestObjectResult(new { success = false, message = "Order not found" });
                }

                if (!status.Equals("RETURN_REQUESTED", StringComparison.OrdinalIgnoreCase))
                {
                    return new BadRequestObjectResult(new { success = false, message = "Order is not in RETURN_REQUESTED state" });
                }

                string newOrderStatus = action.Equals("APPROVE", StringComparison.OrdinalIgnoreCase) ? "RETURNED" : "RETURN_REJECTED";
                string newPaymentStatus = action.Equals("APPROVE", StringComparison.OrdinalIgnoreCase) ? "REFUNDED" : null;
                string message = action.Equals("APPROVE", StringComparison.OrdinalIgnoreCase) ? "Return approved and refund initiated." : "Return request rejected.";

                var updateQuery = "UPDATE orders SET order_status = @OrderStatus " +
                                  (newPaymentStatus != null ? ", payment_status = @PaymentStatus " : "") +
                                  ", updatedat = NOW() WHERE id = @OrderId";

                using var updateCmd = new NpgsqlCommand(updateQuery, con);
                updateCmd.Parameters.AddWithValue("@OrderStatus", newOrderStatus);
                if (newPaymentStatus != null)
                {
                    updateCmd.Parameters.AddWithValue("@PaymentStatus", newPaymentStatus);
                }
                updateCmd.Parameters.AddWithValue("@OrderId", orderId);

                await updateCmd.ExecuteNonQueryAsync();

                return new OkObjectResult(new { success = true, message = message });
            }
            catch (Exception ex)
            {
                return new ObjectResult(new { success = false, message = ex.Message }) { StatusCode = 500 };
            }
        }

        public async Task<List<OrderItemModel>> GetOrderItems(int orderId)
        {
            var items = new List<OrderItemModel>();

            try
            {
                using var con = new NpgsqlConnection(DbConnection);
                await con.OpenAsync();

                var query = @"
                    SELECT *
                    FROM order_items
                    WHERE orderid = @OrderId
                    ORDER BY id ASC";

                using var cmd = new NpgsqlCommand(query, con);
                cmd.Parameters.AddWithValue("@OrderId", orderId);

                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    items.Add(new OrderItemModel
                    {
                        Id = (int)reader["id"],
                        OrderId = (int)reader["orderid"],
                        ProductId = (int)reader["productid"],
                        VariantId = reader["variantid"] != DBNull.Value ? (int)reader["variantid"] : 0,
                        ProductName = reader["productname"] != DBNull.Value ? (string)reader["productname"] : null,
                        ProductImageUrl = reader["productimageurl"] != DBNull.Value ? (string)reader["productimageurl"] : null,
                        SKU = reader["sku"] != DBNull.Value ? (string)reader["sku"] : null,
                        ColorName = reader["color"] != DBNull.Value ? (string)reader["color"] : null,
                        SizeName = reader["size_name"] != DBNull.Value ? (string)reader["size_name"] : null,
                        Quantity = (int)reader["quantity"],
                        MRP = reader["mrp"] != DBNull.Value ? (decimal)reader["mrp"] : 0,
                        Price = reader["saleprice"] != DBNull.Value ? (decimal)reader["saleprice"] : 0,
                        Total = reader["totalprice"] != DBNull.Value ? (decimal)reader["totalprice"] : 0
                    });
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Error fetching order items: {ex.Message}");
            }

            return items;
        }
    }
}
