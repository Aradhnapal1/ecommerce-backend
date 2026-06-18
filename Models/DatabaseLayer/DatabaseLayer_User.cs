using CloudinaryDotNet;
using CloudinaryDotNet.Actions;
using Ecommerce_Backend.Models;
using Ecommerce_Backend.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Razor.Language.Intermediate;
using Npgsql;

namespace Ecommerce_Backend.Models.DatabaseLayer
{
    public partial interface IDatabaseLayer
    {
        Task<bool> UserRegister(UserRegisterRequest model);
        Task<bool> UserVerifyOtp(UserVerifyOtpRequest model);

        Task<UserLoginResponse?> UserLogin(UserLoginRequest model);
        Task<UserLoginResponse?> GetUserById(int id);
        Task<List<UserLoginResponse>> GetAllUsers();
        Task<IActionResult> DeleteUser(int id);
        Task<IActionResult> ForgotPassword(string email);
        Task<IActionResult> ResetPassword(ResetPasswordRequest model);
        Task<IActionResult> ChangePassword(int userId, ChangePasswordRequest model);
        Task<IActionResult> UpdateProfile(int userId, UpdateProfileRequest model);
        Task<IActionResult> GetUserProfile(int userId);
    }

    public partial class DataBaseLayer : IDatabaseLayer
    {
        private readonly IEmailService _emailService;

        private static string GenerateOtp()
            => new Random().Next(100000, 999999).ToString();

        // ---------------- REGISTER ----------------
        public async Task<bool> UserRegister(UserRegisterRequest model)
        {
            // Role validate karo
            var allowedRoles = new[] { "ADMIN", "USER" };
            if (string.IsNullOrWhiteSpace(model.role) ||
                !allowedRoles.Contains(model.role.ToUpper()))
                return false;

            using var con = new NpgsqlConnection(DbConnection);
            await con.OpenAsync();

            // Check karo — email exists hai aur verified bhi hai?
            using var checkCmd = new NpgsqlCommand(@"
        SELECT is_verified FROM user_register 
        WHERE email = @email
    ", con);
            checkCmd.Parameters.AddWithValue("@email", model.email ?? "");
            var result = await checkCmd.ExecuteScalarAsync();

            if (result != null && result != DBNull.Value)
            {
                bool isVerified = Convert.ToBoolean(result);

                // ✅ Already verified — email already exists error
                if (isVerified) return false;

                // ✅ Not verified — purana record delete karo
                using var deleteCmd = new NpgsqlCommand(
                    "DELETE FROM user_register WHERE email = @email", con);
                deleteCmd.Parameters.AddWithValue("@email", model.email ?? "");
                await deleteCmd.ExecuteNonQueryAsync();
            }

            // Naya OTP generate karo + password hash karo
            string otp = GenerateOtp();
            string hash = BCrypt.Net.BCrypt.HashPassword(model.password);

            // Fresh insert karo
            using var cmd = new NpgsqlCommand(@"
        INSERT INTO user_register
            (first_name, last_name, email, phone_number, password, role, otp, is_verified)
        VALUES
            (@first_name, @last_name, @email, @phone_number, @password, @role, @otp, @is_verified)
    ", con);
            cmd.Parameters.AddWithValue("@first_name", model.first_name ?? "");
            cmd.Parameters.AddWithValue("@last_name", model.last_name ?? "");
            cmd.Parameters.AddWithValue("@email", model.email ?? "");
            cmd.Parameters.AddWithValue("@phone_number", model.phone_number ?? "");
            cmd.Parameters.AddWithValue("@password", hash);
            cmd.Parameters.AddWithValue("@role", model.role.ToUpper());
            cmd.Parameters.AddWithValue("@otp", otp);
            cmd.Parameters.AddWithValue("@is_verified", false);
            await cmd.ExecuteNonQueryAsync();

            // OTP email bhejo
            await _emailService.SendOtp(model.email!, otp);
            return true;
        }

        // ---------------- VERIFY OTP ----------------
        public async Task<bool> UserVerifyOtp(UserVerifyOtpRequest model)
        {
            using var con = new NpgsqlConnection(DbConnection);
            await con.OpenAsync();

            using var cmd = new NpgsqlCommand(@"
                SELECT COUNT(*) FROM user_register
                WHERE email = @email AND otp = @otp
            ", con);
            cmd.Parameters.AddWithValue("@email", model.email ?? "");
            cmd.Parameters.AddWithValue("@otp", model.otp ?? "");
            int count = Convert.ToInt32(await cmd.ExecuteScalarAsync());
            if (count == 0) return false;

            using var updateCmd = new NpgsqlCommand(@"
                UPDATE user_register
                SET is_verified = TRUE,
                    otp         = NULL
                WHERE email = @email
            ", con);
            updateCmd.Parameters.AddWithValue("@email", model.email ?? "");
            await updateCmd.ExecuteNonQueryAsync();
            return true;
        }


        public async Task<UserLoginResponse?> UserLogin(UserLoginRequest model)
        {
            using var con = new NpgsqlConnection(DbConnection);
            await con.OpenAsync();

            using var cmd = new NpgsqlCommand(@"
        SELECT id, first_name, last_name, email, phone_number, password, role, is_verified,
               profile_image_url, date_of_birth, gender
        FROM user_register 
        WHERE email = @email
    ", con);
            cmd.Parameters.AddWithValue("@email", model.email ?? "");

            using var reader = await cmd.ExecuteReaderAsync();
            if (!await reader.ReadAsync()) return null;

            bool isVerified = reader.GetBoolean(reader.GetOrdinal("is_verified"));
            if (!isVerified) return null;

            string storedHash = reader.GetString(reader.GetOrdinal("password"));
            if (!BCrypt.Net.BCrypt.Verify(model.password, storedHash)) return null;

            return new UserLoginResponse
            {
                id = reader.GetInt32(reader.GetOrdinal("id")),
                first_name = reader.GetString(reader.GetOrdinal("first_name")),
                last_name = reader.GetString(reader.GetOrdinal("last_name")),
                email = reader.GetString(reader.GetOrdinal("email")),
                phone_number = reader.GetString(reader.GetOrdinal("phone_number")),
                role = reader.GetString(reader.GetOrdinal("role")),
                ProfileImageUrl = reader.IsDBNull(reader.GetOrdinal("profile_image_url")) ? null : reader.GetString(reader.GetOrdinal("profile_image_url")),
                DateOfBirth = reader.IsDBNull(reader.GetOrdinal("date_of_birth")) ? (DateTime?)null : reader.GetDateTime(reader.GetOrdinal("date_of_birth")),
                Gender = reader.IsDBNull(reader.GetOrdinal("gender")) ? null : reader.GetString(reader.GetOrdinal("gender"))
            };
        }





        public async Task<UserLoginResponse?> GetUserById(int id)
        {
            using var con = new NpgsqlConnection(DbConnection);
            await con.OpenAsync();

            using var cmd = new NpgsqlCommand(@"
        SELECT id, first_name, last_name, email, phone_number, role, is_verified,
               profile_image_url, date_of_birth, gender
        FROM user_register 
        WHERE id = @id AND is_verified = TRUE
    ", con);
            cmd.Parameters.AddWithValue("@id", id);

            using var reader = await cmd.ExecuteReaderAsync();
            if (!await reader.ReadAsync()) return null;

            return new UserLoginResponse
            {
                id = reader.GetInt32(reader.GetOrdinal("id")),
                first_name = reader.GetString(reader.GetOrdinal("first_name")),
                last_name = reader.GetString(reader.GetOrdinal("last_name")),
                email = reader.GetString(reader.GetOrdinal("email")),
                phone_number = reader.GetString(reader.GetOrdinal("phone_number")),
                role = reader.GetString(reader.GetOrdinal("role")),
                ProfileImageUrl = reader.IsDBNull(reader.GetOrdinal("profile_image_url")) ? null : reader.GetString(reader.GetOrdinal("profile_image_url")),
                DateOfBirth = reader.IsDBNull(reader.GetOrdinal("date_of_birth")) ? (DateTime?)null : reader.GetDateTime(reader.GetOrdinal("date_of_birth")),
                Gender = reader.IsDBNull(reader.GetOrdinal("gender")) ? null : reader.GetString(reader.GetOrdinal("gender"))
            };
        }

        // Get All Users
        public async Task<List<UserLoginResponse>> GetAllUsers()
        {
            using var con = new NpgsqlConnection(DbConnection);
            await con.OpenAsync();

            using var cmd = new NpgsqlCommand(@"
        SELECT id, first_name, last_name, email, phone_number, role, is_verified,
               profile_image_url, date_of_birth, gender
        FROM user_register
        WHERE is_verified = TRUE
        ORDER BY id DESC
    ", con);

            using var reader = await cmd.ExecuteReaderAsync();
            var users = new List<UserLoginResponse>();

            while (await reader.ReadAsync())
            {
                users.Add(new UserLoginResponse
                {
                    id = reader.GetInt32(reader.GetOrdinal("id")),
                    first_name = reader.GetString(reader.GetOrdinal("first_name")),
                    last_name = reader.GetString(reader.GetOrdinal("last_name")),
                    email = reader.GetString(reader.GetOrdinal("email")),
                    phone_number = reader.GetString(reader.GetOrdinal("phone_number")),
                    role = reader.GetString(reader.GetOrdinal("role")),
                    ProfileImageUrl = reader.IsDBNull(reader.GetOrdinal("profile_image_url")) ? null : reader.GetString(reader.GetOrdinal("profile_image_url")),
                    DateOfBirth = reader.IsDBNull(reader.GetOrdinal("date_of_birth")) ? (DateTime?)null : reader.GetDateTime(reader.GetOrdinal("date_of_birth")),
                    Gender = reader.IsDBNull(reader.GetOrdinal("gender")) ? null : reader.GetString(reader.GetOrdinal("gender"))
                });
            }

            return users;
        }

        public async Task<IActionResult> DeleteUser(int id)
        {
            using var con = new NpgsqlConnection(DbConnection);
            await con.OpenAsync();
            using var cmd = new NpgsqlCommand(@"
       DELETE FROM user_register
       WHERE id = @id
    ", con);
            cmd.Parameters.AddWithValue("@id", id);

            var rows = await cmd.ExecuteNonQueryAsync();
            if (rows == 0)
            {
                return new NotFoundObjectResult(new
                {
                    success = false,
                    message = "User not found"
                });
            }

            return new OkObjectResult(new
            {
                success = true,
                message = "User deleted successfully"
            });
        }

        public async Task<IActionResult> ForgotPassword(string email)
        {
            try
            {
                using var con = new NpgsqlConnection(DbConnection);
                await con.OpenAsync();

                var checkQuery = "SELECT id FROM user_register WHERE email = @Email AND is_verified = TRUE";
                using var checkCmd = new NpgsqlCommand(checkQuery, con);
                checkCmd.Parameters.AddWithValue("@Email", email);
                var userId = await checkCmd.ExecuteScalarAsync();

                if (userId == null)
                {
                    // Don't reveal if the user exists or not for security
                    return new OkObjectResult(new { success = true, message = "If an account with this email exists, a password reset link has been sent." });
                }

                var token = GenerateOtp();

                var updateQuery = "UPDATE user_register SET otp = @Token WHERE id = @UserId";
                using var updateCmd = new NpgsqlCommand(updateQuery, con);
                updateCmd.Parameters.AddWithValue("@Token", token);
                updateCmd.Parameters.AddWithValue("@UserId", (int)userId);
                await updateCmd.ExecuteNonQueryAsync();

                // This should be a URL to your frontend reset page
                var resetLink = $"http://localhost:3000/reset-password?email={Uri.EscapeDataString(email)}&token={token}";
                await _emailService.SendPasswordResetEmail(email, resetLink);

                return new OkObjectResult(new { success = true, message = "If an account with this email exists, a password reset link has been sent." });
            }
            catch (Exception ex)
            {
                return new ObjectResult(new { success = false, message = ex.Message }) { StatusCode = 500 };
            }
        }

        public async Task<IActionResult> ResetPassword(ResetPasswordRequest model)
        {
            try
            {
                using var con = new NpgsqlConnection(DbConnection);
                await con.OpenAsync();

                var checkQuery = "SELECT id FROM user_register WHERE email = @Email AND otp = @Token AND is_verified = TRUE";
                using var checkCmd = new NpgsqlCommand(checkQuery, con);
                checkCmd.Parameters.AddWithValue("@Email", model.Email);
                var userId = await checkCmd.ExecuteScalarAsync();

                if (userId == null)
                {
                    return new BadRequestObjectResult(new { success = false, message = "Invalid token or email." });
                }

                var newPasswordHash = BCrypt.Net.BCrypt.HashPassword(model.NewPassword);

                var updateQuery = "UPDATE user_register SET password = @Password, otp = NULL WHERE id = @UserId";
                using var updateCmd = new NpgsqlCommand(updateQuery, con);
                updateCmd.Parameters.AddWithValue("@Password", newPasswordHash);
                updateCmd.Parameters.AddWithValue("@UserId", (int)userId);
                await updateCmd.ExecuteNonQueryAsync();

                return new OkObjectResult(new { success = true, message = "Password has been reset successfully." });
            }
            catch (Exception ex)
            {
                return new ObjectResult(new { success = false, message = ex.Message }) { StatusCode = 500 };
            }
        }

        public async Task<IActionResult> ChangePassword(int userId, ChangePasswordRequest model)
        {
            try
            {
                using var con = new NpgsqlConnection(DbConnection);
                await con.OpenAsync();

                var getHashQuery = "SELECT password FROM user_register WHERE id = @UserId AND is_verified = TRUE";
                using var getHashCmd = new NpgsqlCommand(getHashQuery, con);
                getHashCmd.Parameters.AddWithValue("@UserId", userId);
                var currentHash = (string?)await getHashCmd.ExecuteScalarAsync();

                if (currentHash == null)
                {
                    return new NotFoundObjectResult(new { success = false, message = "User not found or access denied." });
                }

                if (!BCrypt.Net.BCrypt.Verify(model.OldPassword, currentHash))
                {
                    return new BadRequestObjectResult(new { success = false, message = "Incorrect old password." });
                }

                var newHash = BCrypt.Net.BCrypt.HashPassword(model.NewPassword);
                var updateQuery = "UPDATE user_register SET password = @NewPassword WHERE id = @UserId";
                using var updateCmd = new NpgsqlCommand(updateQuery, con);
                updateCmd.Parameters.AddWithValue("@NewPassword", newHash);
                updateCmd.Parameters.AddWithValue("@UserId", userId);
                await updateCmd.ExecuteNonQueryAsync();

                return new OkObjectResult(new { success = true, message = "Password changed successfully." });
            }
            catch (Exception ex)
            {
                return new ObjectResult(new { success = false, message = ex.Message }) { StatusCode = 500 };
            }
        }

        public async Task<IActionResult> UpdateProfile(int userId, UpdateProfileRequest model)
        {
            try
            {
                using var con = new NpgsqlConnection(DbConnection);
                await con.OpenAsync();

                var setClauses = new List<string>();
                var parameters = new List<NpgsqlParameter> { new NpgsqlParameter("@UserId", userId) };

                if (!string.IsNullOrWhiteSpace(model.FirstName))
                {
                    setClauses.Add("first_name = @FirstName");
                    parameters.Add(new NpgsqlParameter("@FirstName", model.FirstName));
                }
                if (!string.IsNullOrWhiteSpace(model.LastName))
                {
                    setClauses.Add("last_name = @LastName");
                    parameters.Add(new NpgsqlParameter("@LastName", model.LastName));
                }
                if (!string.IsNullOrWhiteSpace(model.PhoneNumber))
                {
                    setClauses.Add("phone_number = @PhoneNumber");
                    parameters.Add(new NpgsqlParameter("@PhoneNumber", model.PhoneNumber));
                }
                if (model.DateOfBirth.HasValue)
                {
                    setClauses.Add("date_of_birth = @DateOfBirth");
                    parameters.Add(new NpgsqlParameter("@DateOfBirth", model.DateOfBirth.Value.Date));
                }
                if (!string.IsNullOrWhiteSpace(model.Gender))
                {
                    setClauses.Add("gender = @Gender");
                    parameters.Add(new NpgsqlParameter("@Gender", model.Gender));
                }

                if (model.ProfileImage != null)
                {
                    // Get old image url to delete it
                    var getOldImageCmd = new NpgsqlCommand("SELECT profile_image_url FROM user_register WHERE id = @UserId", con);
                    getOldImageCmd.Parameters.AddWithValue("@UserId", userId);
                    var oldImageUrl = (await getOldImageCmd.ExecuteScalarAsync()) as string;

                    // Cloudinary setup
                    var account = new Account(
                        _configuration["CloudinarySettings:CloudName"],
                        _configuration["CloudinarySettings:ApiKey"],
                        _configuration["CloudinarySettings:ApiSecret"]
                    );
                    var cloudinary = new Cloudinary(account);

                    // Delete old image if it exists
                    if (!string.IsNullOrEmpty(oldImageUrl))
                    {
                        try
                        {
                            var uri = new Uri(oldImageUrl);
                            var publicId = Path.GetFileNameWithoutExtension(uri.AbsolutePath);
                            await cloudinary.DestroyAsync(new DeletionParams($"profile_images/{publicId}"));
                        }
                        catch { /* Ignore delete errors */ }
                    }

                    // Upload new image
                    string newImageUrl;
                    using (var stream = model.ProfileImage.OpenReadStream())
                    {
                        var uploadParams = new ImageUploadParams()
                        {
                            File = new FileDescription(model.ProfileImage.FileName, stream),
                            Folder = "profile_images",
                            PublicId = $"user_{userId}_{Guid.NewGuid()}"
                        };
                        var uploadResult = await cloudinary.UploadAsync(uploadParams);
                        newImageUrl = uploadResult.SecureUrl.ToString();
                    }

                    setClauses.Add("profile_image_url = @ProfileImageUrl");
                    parameters.Add(new NpgsqlParameter("@ProfileImageUrl", newImageUrl));
                }

                if (setClauses.Count == 0)
                {
                    return new BadRequestObjectResult(new { success = false, message = "No fields to update." });
                }

                setClauses.Add("updatedat = NOW()");
                var query = $"UPDATE user_register SET {string.Join(", ", setClauses)} WHERE id = @UserId";

                using var cmd = new NpgsqlCommand(query, con);
                cmd.Parameters.AddRange(parameters.ToArray());

                var rowsAffected = await cmd.ExecuteNonQueryAsync();

                if (rowsAffected > 0)
                {
                    return new OkObjectResult(new { success = true, message = "Profile updated successfully." });
                }

                return new NotFoundObjectResult(new { success = false, message = "User not found." });
            }
            catch (Exception ex)
            {
                return new ObjectResult(new { success = false, message = ex.Message }) { StatusCode = 500 };
            }
        }

        public async Task<IActionResult> GetUserProfile(int userId)
        {
            try
            {
                using var con = new NpgsqlConnection(DbConnection);
                await con.OpenAsync();

                var query = @"
                    SELECT id, first_name, last_name, email, phone_number, role, is_verified,
                           profile_image_url, date_of_birth, gender
                    FROM user_register 
                    WHERE id = @UserId AND is_verified = TRUE LIMIT 1";

                using var cmd = new NpgsqlCommand(query, con);
                cmd.Parameters.AddWithValue("@UserId", userId);

                using var reader = await cmd.ExecuteReaderAsync();

                if (await reader.ReadAsync())
                {
                    return new OkObjectResult(new {
                        success = true,
                        message = "User profile fetched successfully.",
                        data = new {
                            id = reader.GetInt32(reader.GetOrdinal("id")),
                            firstName = reader.GetString(reader.GetOrdinal("first_name")),
                            lastName = reader.GetString(reader.GetOrdinal("last_name")),
                            email = reader.GetString(reader.GetOrdinal("email")),
                            phoneNumber = reader.GetString(reader.GetOrdinal("phone_number")),
                            role = reader.GetString(reader.GetOrdinal("role")),
                            profileImageUrl = reader.IsDBNull(reader.GetOrdinal("profile_image_url")) ? null : reader.GetString(reader.GetOrdinal("profile_image_url")),
                            dateOfBirth = reader.IsDBNull(reader.GetOrdinal("date_of_birth")) ? (DateTime?)null : reader.GetDateTime(reader.GetOrdinal("date_of_birth")),
                            gender = reader.IsDBNull(reader.GetOrdinal("gender")) ? null : reader.GetString(reader.GetOrdinal("gender"))
                        }
                    });
                }

                return new NotFoundObjectResult(new { success = false, message = "User not found." });
            }
            catch (Exception ex)
            {
                return new ObjectResult(new { success = false, message = ex.Message }) { StatusCode = 500 };
            }
        }
    }
}








   