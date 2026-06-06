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
        Task<ColorResponse> UpdateColor(int id, ColorResponse color);
        Task<ColorResponse> DeleteColor(int id);
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
            var trimmed = colorName.Trim();

            // ✅ Step 1: CSS canvas trick se RGB nikalo (server-side safe)
            var (r, g, b, cssValid) = ParseCssColor(trimmed);

            if (cssValid)
            {
                // ✅ Step 2: thecolorapi.com pe RGB bhejo — exact named hex milega
                try
                {
                    using var client = new HttpClient();
                    client.Timeout = TimeSpan.FromSeconds(8);
                    client.DefaultRequestHeaders.Add(
                        "User-Agent", "Mozilla/5.0 (compatible; EcommerceApp/1.0)");

                    // RGB se API call — name se nahi!
                    var url = $"https://www.thecolorapi.com/id?rgb=rgb({r},{g},{b})&format=json";
                    var response = await client.GetAsync(url);

                    if (response.IsSuccessStatusCode)
                    {
                        var json = await response.Content.ReadAsStringAsync();
                        using var doc = JsonDocument.Parse(json);
                        var hex = doc.RootElement
                                     .GetProperty("hex")
                                     .GetProperty("value")
                                     .GetString();

                        if (!string.IsNullOrEmpty(hex))
                            return hex.ToUpper(); // "#FF0000"
                    }
                }
                catch { }

                // ✅ Step 3: API fail hoi to CSS se mila RGB hi use karo
                return $"#{r:X2}{g:X2}{b:X2}".ToUpper();
            }

            // ✅ Step 4: CSS bhi nahi jaanta (custom name) → #808080
            return "#808080";
        }

        // Server-side CSS color parser — System.Drawing use karta hai
        private (int R, int G, int B, bool Valid) ParseCssColor(string colorName)
        {
            try
            {
                // System.Drawing.Color CSS named colors support karta hai
                var color = System.Drawing.ColorTranslator.FromHtml(colorName);
                return (color.R, color.G, color.B, true);
            }
            catch { }

            // Space-removed try — "Navy Blue" → "navyblue"
            try
            {
                var noSpace = colorName.Replace(" ", "");
                var color = System.Drawing.ColorTranslator.FromHtml(noSpace);
                return (color.R, color.G, color.B, true);
            }
            catch { }

            return (0, 0, 0, false);
        }

        // ✅ Local fallback — koi bhi API call nahi, deterministic color
        private string GenerateColorFromName(string colorName)
        {
            // Name se consistent hex generate karo
            int hash = colorName.ToLower().Aggregate(0, (h, c) => h * 31 + c);
            int r = (hash >> 16) & 0xFF;
            int g = (hash >> 8) & 0xFF;
            int b = hash & 0xFF;

            // Too dark/light avoid karo
            r = Math.Clamp(r, 80, 220);
            g = Math.Clamp(g, 80, 220);
            b = Math.Clamp(b, 80, 220);

            return $"#{r:X2}{g:X2}{b:X2}";
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

        public async Task<ColorResponse> UpdateColor(int id, ColorResponse color)
        {
            using var con = new NpgsqlConnection(DbConnection);
            await con.OpenAsync();
            // Check if color exists
            using (var checkCmd = new NpgsqlCommand(
                "SELECT COUNT(*) FROM colors WHERE id=@id", con))
            {
                checkCmd.Parameters.AddWithValue("@id", id);
                var count = Convert.ToInt32(await checkCmd.ExecuteScalarAsync());
                if (count == 0)
                    throw new Exception("Color not found.");
            }

            // Dynamic color code — API se aa raha hai
            string colorCode = await GetColorCode(color.ColorName);

            using var cmd = new NpgsqlCommand(@"
        UPDATE colors
        SET color_name=@color_name, color_code=@color_code, status=@status
        WHERE id=@id
        RETURNING id, created_at;
    ", con);

            cmd.Parameters.AddWithValue("@id", id);
            cmd.Parameters.AddWithValue("@color_name", color.ColorName);
            cmd.Parameters.AddWithValue("@color_code", colorCode);
            cmd.Parameters.AddWithValue("@status", color.Status);

            using var reader = await cmd.ExecuteReaderAsync();
            if (!await reader.ReadAsync())
                throw new Exception("Failed to update color.");

            return new ColorResponse
            {
                Id = reader.GetInt32(0),
                ColorName = color.ColorName,
                ColorCode = colorCode,
                Status = color.Status,
                CreatedAt = reader.GetDateTime(1)
            };
        }

        public async Task<ColorResponse> DeleteColor(int id)
        {
            using var con = new NpgsqlConnection(DbConnection);
            await con.OpenAsync();
            // Check if color exists
            using (var checkCmd = new NpgsqlCommand(
                "SELECT id, color_name, color_code, status, created_at FROM colors WHERE id=@id", con))
            {
                checkCmd.Parameters.AddWithValue("@id", id);
                using var reader = await checkCmd.ExecuteReaderAsync();
                if (!await reader.ReadAsync())
                    throw new Exception("Color not found.");
                var color = new ColorResponse
                {
                    Id = reader.GetInt32(0),
                    ColorName = reader.GetString(1),
                    ColorCode = reader.GetString(2),
                    Status = reader.GetBoolean(3),
                    CreatedAt = reader.GetDateTime(4)
                };
                reader.Close();
                // Delete the color
                using var deleteCmd = new NpgsqlCommand(
                    "DELETE FROM colors WHERE id=@id", con);
                deleteCmd.Parameters.AddWithValue("@id", id);
                var rowsAffected = await deleteCmd.ExecuteNonQueryAsync();
                if (rowsAffected == 0)
                    throw new Exception("Failed to delete color.");
                return color;
            }
        }
    }
}

