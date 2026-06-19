using Ecommerce_Backend.Helpers;
using Microsoft.AspNetCore.Mvc;
using Npgsql;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Ecommerce_Backend.Models.DatabaseLayer
{
    public partial interface IDatabaseLayer
    {
        Task<IActionResult> AddProductReview(int userId, AddReviewRequest request);
        Task<IActionResult> GetProductReviews(int productId);
        Task<IActionResult> DeleteProductReview(int reviewId, int? userId, bool isAdmin);
    }

    public partial class DataBaseLayer
    {
        public async Task<IActionResult> AddProductReview(int userId, AddReviewRequest request)
        {
            try
            {
                using var con = new NpgsqlConnection(DbConnection);
                await con.OpenAsync();

                // 1. Check if user already reviewed this product
                var checkQuery = "SELECT COUNT(*) FROM product_reviews WHERE product_id = @productId AND user_id = @userId";
                using var checkCmd = new NpgsqlCommand(checkQuery, con);
                checkCmd.Parameters.AddWithValue("productId", request.ProductId);
                checkCmd.Parameters.AddWithValue("userId", userId);
                
                if (Convert.ToInt32(await checkCmd.ExecuteScalarAsync()) > 0)
                {
                    return new BadRequestObjectResult(new { success = false, message = "You have already reviewed this product." });
                }

                // 2. Insert Review
                var insertQuery = @"
                    INSERT INTO product_reviews (product_id, user_id, rating, comment, created_at) 
                    VALUES (@productId, @userId, @rating, @comment, NOW())";
                using var insertCmd = new NpgsqlCommand(insertQuery, con);
                insertCmd.Parameters.AddWithValue("productId", request.ProductId);
                insertCmd.Parameters.AddWithValue("userId", userId);
                insertCmd.Parameters.AddWithValue("rating", request.Rating);
                insertCmd.Parameters.AddWithValue("comment", (object?)request.Comment ?? DBNull.Value);
                await insertCmd.ExecuteNonQueryAsync();

                // 3. Update average rating and total reviews in products table
                await UpdateProductRatingStats(con, request.ProductId);

                return new OkObjectResult(new { success = true, message = "Review added successfully." });
            }
            catch (Exception ex)
            {
                return ApiResponses.InternalError(ex);
            }
        }

        public async Task<IActionResult> GetProductReviews(int productId)
        {
            try
            {
                var reviews = new List<ProductReviewModel>();
                using var con = new NpgsqlConnection(DbConnection);
                await con.OpenAsync();

                // Join product_reviews with user_register to get user details
                var query = @"
                    SELECT 
                        pr.id, pr.product_id, pr.user_id, pr.rating, pr.comment, pr.created_at,
                        u.first_name, u.last_name, u.email, u.profile_image_url
                    FROM product_reviews pr
                    INNER JOIN user_register u ON pr.user_id = u.id
                    WHERE pr.product_id = @productId
                    ORDER BY pr.created_at DESC";

                using var cmd = new NpgsqlCommand(query, con);
                cmd.Parameters.AddWithValue("productId", productId);
                using var reader = await cmd.ExecuteReaderAsync();

                while (await reader.ReadAsync())
                {
                    reviews.Add(new ProductReviewModel
                    {
                        Id = reader.GetInt32(0),
                        ProductId = reader.GetInt32(1),
                        UserId = reader.GetInt32(2),
                        Rating = reader.GetInt32(3),
                        Comment = reader.IsDBNull(4) ? null : reader.GetString(4),
                        CreatedAt = reader.GetDateTime(5),
                        FirstName = reader.IsDBNull(6) ? null : reader.GetString(6),
                        LastName = reader.IsDBNull(7) ? null : reader.GetString(7),
                        Email = null,
                        ProfileImageUrl = reader.IsDBNull(9) ? null : reader.GetString(9)
                    });
                }

                return new OkObjectResult(new { success = true, data = reviews });
            }
            catch (Exception)
            {
                return ApiResponses.InternalError(new Exception("Review operation failed"));
            }
        }

        public async Task<IActionResult> DeleteProductReview(int reviewId, int? userId, bool isAdmin)
        {
            try
            {
                using var con = new NpgsqlConnection(DbConnection);
                await con.OpenAsync();

                int productId = 0;
                int reviewOwnerId = 0;
                var findQuery = "SELECT product_id, user_id FROM product_reviews WHERE id = @id";
                using var findCmd = new NpgsqlCommand(findQuery, con);
                findCmd.Parameters.AddWithValue("id", reviewId);
                using (var reader = await findCmd.ExecuteReaderAsync())
                {
                    if (!await reader.ReadAsync())
                    {
                        return new NotFoundObjectResult(new { success = false, message = "Review not found." });
                    }

                    productId = reader.GetInt32(0);
                    reviewOwnerId = reader.GetInt32(1);
                }

                if (!isAdmin && (!userId.HasValue || userId.Value != reviewOwnerId))
                {
                    return new ForbidResult();
                }

                var deleteQuery = "DELETE FROM product_reviews WHERE id = @id";
                using var deleteCmd = new NpgsqlCommand(deleteQuery, con);
                deleteCmd.Parameters.AddWithValue("id", reviewId);
                await deleteCmd.ExecuteNonQueryAsync();

                await UpdateProductRatingStats(con, productId);

                return new OkObjectResult(new { success = true, message = "Review deleted successfully." });
            }
            catch (Exception ex)
            {
                return ApiResponses.InternalError(ex);
            }
        }

        // Helper Method to dynamically update Product average rating
        private async Task UpdateProductRatingStats(NpgsqlConnection con, int productId)
        {
            var updateQuery = @"
                UPDATE products 
                SET 
                    average_rating = COALESCE((SELECT AVG(rating) FROM product_reviews WHERE product_id = @pid), 0),
                    total_reviews = (SELECT COUNT(*) FROM product_reviews WHERE product_id = @pid)
                WHERE id = @pid";
            using var cmd = new NpgsqlCommand(updateQuery, con);
            cmd.Parameters.AddWithValue("pid", productId);
            await cmd.ExecuteNonQueryAsync();
        }
    }
}