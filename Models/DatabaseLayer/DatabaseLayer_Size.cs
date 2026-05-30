using Microsoft.AspNetCore.Mvc;
using Npgsql;
using System.Data.Common;

namespace Ecommerce_Backend.Models.DatabaseLayer
{
    public partial interface IDatabaseLayer
    {
        Task<List<SizeModel>> GetAllSizes();
        Task<IActionResult> AddSize([FromForm] SizeModel size);
        Task<IActionResult> UpdateSize(int id, [FromForm] SizeModel size);
        Task<IActionResult> DeleteSize(int id);
    }

    public partial class DataBaseLayer : IDatabaseLayer
    {
        public async Task<List<SizeModel>> GetAllSizes()
        {
            var sizes = new List<SizeModel>();

            using var connection = new NpgsqlConnection(DbConnection);
            await connection.OpenAsync();

            using var command = new NpgsqlCommand(@"
                SELECT
                    id,
                    size_name,
                    created_at,
                    is_active
                FROM sizes
                ORDER BY id;
            ", connection);

            using var reader = await command.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                sizes.Add(new SizeModel
                {
                    Id = Convert.ToInt32(reader["id"]),
                    SizeName = reader["size_name"]?.ToString(),
                    CreatedAt = Convert.ToDateTime(reader["created_at"]),
                    IsActive = Convert.ToBoolean(reader["is_active"])
                });
            }

            return sizes;
        }



        public async Task<IActionResult> AddSize(SizeModel size)
        {
            using var connection = new NpgsqlConnection(DbConnection);
            await connection.OpenAsync();

            using var checkCommand = new NpgsqlCommand(@"
        SELECT COUNT(*)
        FROM sizes
        WHERE LOWER(size_name) = LOWER(@size_name);
    ", connection);

            checkCommand.Parameters.AddWithValue("@size_name", size.SizeName ?? "");

            var count = Convert.ToInt32(await checkCommand.ExecuteScalarAsync());

            if (count > 0)
            {
                return new BadRequestObjectResult(new
                {
                    status = false,
                    message = "Size already exists"
                });
            }

            using var command = new NpgsqlCommand(@"
        INSERT INTO sizes (size_name, created_at, is_active)
        VALUES (@size_name, @created_at, @is_active)
        RETURNING id;
    ", connection);

            command.Parameters.AddWithValue("@size_name", size.SizeName ?? "");
            command.Parameters.AddWithValue("@created_at", DateTime.UtcNow);
            command.Parameters.AddWithValue("@is_active", true);

            await command.ExecuteScalarAsync();

            return new OkObjectResult(new
            {
                status = true,
                message = "Size added successfully"
            });
        }


        public async Task<IActionResult> UpdateSize(int id, [FromForm] SizeModel size)
        {
            using var connection = new NpgsqlConnection(DbConnection);
            await connection.OpenAsync();
            using var command = new NpgsqlCommand(@"
                UPDATE sizes
                SET size_name = @size_name,
                    is_active = @is_active
                WHERE id = @id;
            ", connection);
            command.Parameters.AddWithValue("@size_name", size.SizeName ?? string.Empty);
            command.Parameters.AddWithValue("@is_active", size.IsActive);
            command.Parameters.AddWithValue("@id", id);
            var rowsAffected = await command.ExecuteNonQueryAsync();
            if (rowsAffected == 0)
            {
                return new NotFoundObjectResult($"Size with ID {id} not found.");
            }
            return new OkObjectResult(size);
        }


        public async Task<IActionResult> DeleteSize(int id)
        {
            using var connection = new NpgsqlConnection(DbConnection);
            await connection.OpenAsync();
            using var command = new NpgsqlCommand(@"
                DELETE FROM sizes
                WHERE id = @id;
            ", connection);
            command.Parameters.AddWithValue("@id", id);
            var rowsAffected = await command.ExecuteNonQueryAsync();
            if (rowsAffected == 0)
            {
                return new NotFoundObjectResult($"Size with ID {id} not found.");
            }
            return new OkObjectResult($"Size with ID {id} deleted successfully.");
        }
    }
}