using Microsoft.AspNetCore.Mvc;
using Npgsql;

namespace Ecommerce_Backend.Models.DatabaseLayer
{
    public partial interface IDatabaseLayer
    {
        Task<IActionResult> CreateContact(ContactModel contact);

        Task<IActionResult> GetContacts();
        Task<IActionResult> DeleteContact(int id);
    }
    public partial class DataBaseLayer : IDatabaseLayer
    {

        public async Task<IActionResult> GetContacts()
        {
            try
            {
                var contacts = new List<ContactModel>();

                using var con = new NpgsqlConnection(DbConnection);

                await con.OpenAsync();

                using var cmd = new NpgsqlCommand(@"
            SELECT
                id,
                first_name,
                last_name,
                email,
                phone_number,
                message,
                created_at
            FROM contacts
            ORDER BY id DESC
        ", con);

                using var reader =
                    await cmd.ExecuteReaderAsync();

                while (await reader.ReadAsync())
                {
                    contacts.Add(new ContactModel
                    {
                        Id = reader.GetInt32(0),
                        FirstName = reader.GetString(1),
                        LastName = reader.GetString(2),
                        Email = reader.GetString(3),
                        PhoneNumber = reader.GetString(4),
                        Message = reader.GetString(5),
                        CreatedAt = reader.GetDateTime(6)
                    });
                }

                return new OkObjectResult(new
                {
                    status = true,
                    data = contacts
                });
            }
            catch (Exception ex)
            {
                return new BadRequestObjectResult(new
                {
                    status = false,
                    message = ex.Message
                });
            }
        }


        public async Task<IActionResult> CreateContact([FromForm] ContactModel contact)
        {
            using var con = new NpgsqlConnection(DbConnection);
            await con.OpenAsync();
            using var cmd = new NpgsqlCommand(
                "INSERT INTO contacts (first_name, last_name, email, phone_number, message) VALUES (@first_name, @last_name, @email, @phone_number, @message) RETURNING id",
                con
            );
            cmd.Parameters.AddWithValue("first_name", contact.FirstName ?? string.Empty);
            cmd.Parameters.AddWithValue("last_name", contact.LastName ?? string.Empty);
            cmd.Parameters.AddWithValue("email", contact.Email ?? string.Empty);
            cmd.Parameters.AddWithValue("phone_number", contact.PhoneNumber ?? string.Empty);
            cmd.Parameters.AddWithValue("message", contact.Message ?? string.Empty);
            var newId = (int)await cmd.ExecuteScalarAsync();
            contact.Id = newId;
            contact.CreatedAt = DateTime.UtcNow;
            return new JsonResult(contact);

        }


        public async Task<IActionResult> DeleteContact(int id)
        {
            using var con = new NpgsqlConnection(DbConnection);
            await con.OpenAsync();
            using var cmd = new NpgsqlCommand(
                "DELETE FROM contacts WHERE id = @id",
                con
            );
            cmd.Parameters.AddWithValue("id", id);
            var rowsAffected = await cmd.ExecuteNonQueryAsync();
            if (rowsAffected > 0)
            {
                return new JsonResult(new { status = true, message = "Contact deleted successfully" });
            }
            else
            {
                return new JsonResult(new { status = false, message = "Contact not found" });
            }
        }
    }
}
