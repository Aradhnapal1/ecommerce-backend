using Npgsql;
using System.Data.Common;
using System.Drawing;
using System.Text.Json;

namespace Ecommerce_Backend.Models.DatabaseLayer
{
    public partial interface IDatabaseLayer
    {
        Task<List<ColorResponse>> GetAllColors();
        Task<ColorResponse> CreateColor(ColorResponse color);
    }

    public partial class DataBaseLayer : IDatabaseLayer
    {
        public async Task<List<ColorResponse>> GetAllColors()
        {
            var colors = new List<ColorResponse>();

            using var con = new NpgsqlConnection(DbConnection);
            await con.OpenAsync();

            using var cmd = new NpgsqlCommand(
                // ✅ color_code bhi select karo
                "SELECT id, color_name, color_code, status, created_at FROM colors",
                con
            );

            using var reader = await cmd.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                colors.Add(new ColorResponse
                {
                    Id = reader.GetInt32(0),
                    ColorName = reader.GetString(1),
                    ColorCode = reader.GetString(2),  // ✅ index 2
                    Status = reader.GetBoolean(3),     // ✅ index 3
                    CreatedAt = reader.GetDateTime(4)  // ✅ index 4
                });
            }

            return colors;
        }

        public async Task<string> GetColorCode(string colorName)
        {
            using var client = new HttpClient();
            client.Timeout = TimeSpan.FromSeconds(10);

            // color.pizza API — FREE, no API key, name se hex deta hai
            var encodedName = Uri.EscapeDataString(colorName.Trim());
            var url = $"https://api.color.pizza/v1/names/?name={encodedName}";

            var response = await client.GetAsync(url);
            response.EnsureSuccessStatusCode();

            var content = await response.Content.ReadAsStringAsync();

            using var doc = JsonDocument.Parse(content);
            var root = doc.RootElement;

            // "colors" array ka pehla result lo (highest similarity)
            if (root.TryGetProperty("colors", out var colors) && colors.GetArrayLength() > 0)
            {
                var firstColor = colors[0];
                if (firstColor.TryGetProperty("hex", out var hexProp))
                {
                    var hex = hexProp.GetString();
                    if (!string.IsNullOrEmpty(hex))
                        return hex.ToUpper(); // e.g. "#4169E1"
                }
            }

            return "#808080";
        }

        public async Task<ColorResponse> CreateColor(ColorResponse color)
        {
            using var con = new NpgsqlConnection(DbConnection);
            await con.OpenAsync();

            // Duplicate check
            using (var checkCmd = new NpgsqlCommand(
                "SELECT COUNT(*) FROM colors WHERE LOWER(color_name)=LOWER(@color_name)", con))
            {
                checkCmd.Parameters.AddWithValue("@color_name", color.ColorName);
                var count = Convert.ToInt32(await checkCmd.ExecuteScalarAsync());
                if (count > 0)
                    throw new Exception("Color already exists.");
            }

            // Dynamic color code — API se aa raha hai
            string colorCode = await GetColorCode(color.ColorName);

            using var cmd = new NpgsqlCommand(@"
        INSERT INTO colors (color_name, color_code, status)
        VALUES (@color_name, @color_code, @status)
        RETURNING id, created_at;
    ", con);

            cmd.Parameters.AddWithValue("@color_name", color.ColorName);
            cmd.Parameters.AddWithValue("@color_code", colorCode);
            cmd.Parameters.AddWithValue("@status", color.Status);

            using var reader = await cmd.ExecuteReaderAsync();
            if (!await reader.ReadAsync())
                throw new Exception("Failed to create color.");

            return new ColorResponse
            {
                Id = reader.GetInt32(0),
                ColorName = color.ColorName,
                ColorCode = colorCode,
                Status = color.Status,
                CreatedAt = reader.GetDateTime(1)
            };
        }
    }
}

