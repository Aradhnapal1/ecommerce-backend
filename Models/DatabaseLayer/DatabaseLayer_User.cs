using Ecommerce_Backend.Models;
using Ecommerce_Backend.Services;
using Npgsql;

namespace Ecommerce_Backend.Models.DatabaseLayer
{
    public partial interface IDatabaseLayer
    {
        Task<bool> UserRegister(UserRegisterRequest model);
        Task<bool> UserVerifyOtp(UserVerifyOtpRequest model);
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

            using var checkCmd = new NpgsqlCommand(
                "SELECT COUNT(*) FROM user_register WHERE email = @email", con);
            checkCmd.Parameters.AddWithValue("@email", model.email ?? "");
            int count = Convert.ToInt32(await checkCmd.ExecuteScalarAsync());
            if (count > 0) return false;

            string otp = GenerateOtp();
            string hash = BCrypt.Net.BCrypt.HashPassword(model.password);

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
            cmd.Parameters.AddWithValue("@role", model.role.ToUpper());  // ✅ ADMIN or USER
            cmd.Parameters.AddWithValue("@otp", otp);
            cmd.Parameters.AddWithValue("@is_verified", false);
            await cmd.ExecuteNonQueryAsync();

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
    }
}