using Npgsql;
using System.Data.Common;

namespace Ecommerce_Backend.Models.DatabaseLayer
{
    public partial interface IDatabaseLayer
    {
        Task<List<ColorResponse>> GetAllColors();
    }

    public partial class DataBaseLayer : IDatabaseLayer
    {
        public async Task<List<ColorResponse>> GetAllColors()
        {
            var colors = new List<ColorResponse>();

            using var con = new NpgsqlConnection(DbConnection);
            await con.OpenAsync();

            using var cmd = new NpgsqlCommand(
                "SELECT id, color_name, status, created_at FROM colors",
                con
            );

            using var reader = await cmd.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                colors.Add(new ColorResponse
                {
                    Id = reader.GetInt32(0),
                    ColorName = reader.GetString(1),
                    Status = reader.GetBoolean(2),
                    CreatedAt = reader.GetDateTime(3)
                });
            }

            return colors;
        }
    }
}