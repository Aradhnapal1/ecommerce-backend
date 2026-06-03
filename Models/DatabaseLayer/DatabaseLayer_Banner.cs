using Microsoft.AspNetCore.Mvc;
using Npgsql;

namespace Ecommerce_Backend.Models.DatabaseLayer
{
    public partial interface IDatabaseLayer
    {
        Task<IActionResult> GetBanner();
    }

    public partial class DataBaseLayer : IDatabaseLayer
    {
        public async Task<IActionResult> GetBanner()
        {
            try
            {
                var banners = new List<BannerModel>();

                await using var connection = new NpgsqlConnection(DbConnection);
                await connection.OpenAsync();

                await using var cmd = new NpgsqlCommand(
                    @"SELECT 
                        id,
                        banner_name,
                        banner_description,
                        banner_image,
                        banner_type,
                        banner_link,
                        active,
                        created_at
                    FROM banners
                    ORDER BY created_at DESC",
                    connection);

                await using var reader = await cmd.ExecuteReaderAsync();

                while (await reader.ReadAsync())
                {
                    var banner = new BannerModel
                    {
                        // If ID is SERIAL/INT
                        Id = reader.GetInt32(0),

                        // If columns can be NULL, handle safely
                        BannerName = reader.IsDBNull(1) ? string.Empty : reader.GetString(1),
                        BannerDescription = reader.IsDBNull(2) ? string.Empty : reader.GetString(2),
                        BannerImg = reader.IsDBNull(3) ? string.Empty : reader.GetString(3),
                        BannerType = reader.IsDBNull(4) ? string.Empty : reader.GetString(4),
                        BannerLink = reader.IsDBNull(5) ? string.Empty : reader.GetString(5),
                        Status = !reader.IsDBNull(6) && reader.GetBoolean(6),
                        CreatedAt = reader.IsDBNull(7)
                            ? DateTime.MinValue
                            : reader.GetDateTime(7)
                    };

                    banners.Add(banner);
                }

                return new OkObjectResult(banners);
            }
            catch (Exception ex)
            {
                return new BadRequestObjectResult(new
                {
                    Message = "Failed to fetch banners",
                    Error = ex.Message
                });
            }
        }
    }
}