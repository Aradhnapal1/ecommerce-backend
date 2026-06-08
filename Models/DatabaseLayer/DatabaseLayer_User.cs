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
        SELECT id, first_name, last_name, email, phone_number, password, role, is_verified
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
                role = reader.GetString(reader.GetOrdinal("role"))
            };
        }





        public async Task<UserLoginResponse?> GetUserById(int id)
        {
            using var con = new NpgsqlConnection(DbConnection);
            await con.OpenAsync();

            using var cmd = new NpgsqlCommand(@"
        SELECT id, first_name, last_name, email, phone_number, role, is_verified
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
                role = reader.GetString(reader.GetOrdinal("role"))
            };
        }

        // Get All Users
        public async Task<List<UserLoginResponse>> GetAllUsers()
        {
            using var con = new NpgsqlConnection(DbConnection);
            await con.OpenAsync();

            using var cmd = new NpgsqlCommand(@"
        SELECT id, first_name, last_name, email, phone_number, role, is_verified
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
                    role = reader.GetString(reader.GetOrdinal("role"))
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

            await cmd.ExecuteNonQueryAsync();
            return new OkResult();
        }
    }
}








   