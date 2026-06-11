using Microsoft.AspNetCore.Mvc;
using Npgsql;

namespace Ecommerce_Backend.Models.DatabaseLayer
{
    public partial interface IDatabaseLayer
    {
        Task<IActionResult> AddAddress(
              UserAddressModel model,
              int userId
          );
        Task<IActionResult> GetAddressList(
    int userId
);

        Task<IActionResult> DeleteAddress(int id, int userId);
    }

    public partial class DataBaseLayer : IDatabaseLayer
    {
        public async Task<IActionResult> AddAddress(
          UserAddressModel model,
          int userId
      )
        {
            try
            {
                using var con =
                    new NpgsqlConnection(
                        DbConnection
                    );

                await con.OpenAsync();

                // Default Address Reset

                if (model.IsDefault)
                {
                    string resetQuery = @"
UPDATE user_addresses
SET is_default = false
WHERE userid = @userid";

                    using var resetCmd =
                        new NpgsqlCommand(
                            resetQuery,
                            con
                        );

                    resetCmd.Parameters.AddWithValue(
     "@userid",
     userId
 );

                    await resetCmd.ExecuteNonQueryAsync();
                }

                string query = @"
INSERT INTO user_addresses
(
    userid,
    full_name,
    mobile,
    alternate_mobile,

    address_line1,
    address_line2,

    landmark,

    city,
    state,
    country,

    pincode,

    address_type,

    is_default
)
VALUES
(
    @userid,
    @fullname,
    @mobile,
    @alternatemobile,

    @addressline1,
    @addressline2,

    @landmark,

    @city,
    @state,
    @country,

    @pincode,

    @addresstype,

    @isdefault
)
RETURNING id";

                using var cmd =
                    new NpgsqlCommand(
                        query,
                        con
                    );

                cmd.Parameters.AddWithValue(
    "@userid",
    userId
);
                cmd.Parameters.AddWithValue(
                    "@fullname",
                    model.FullName
                );

                cmd.Parameters.AddWithValue(
                    "@mobile",
                    model.Mobile
                );

                cmd.Parameters.AddWithValue(
                    "@alternatemobile",
                    (object?)model.AlternateMobile
                    ?? DBNull.Value
                );

                cmd.Parameters.AddWithValue(
                    "@addressline1",
                    model.AddressLine1
                );

                cmd.Parameters.AddWithValue(
                    "@addressline2",
                    (object?)model.AddressLine2
                    ?? DBNull.Value
                );

                cmd.Parameters.AddWithValue(
                    "@landmark",
                    (object?)model.Landmark
                    ?? DBNull.Value
                );

                cmd.Parameters.AddWithValue(
                    "@city",
                    model.City
                );

                cmd.Parameters.AddWithValue(
                    "@state",
                    model.State
                );

                cmd.Parameters.AddWithValue(
                    "@country",
                    model.Country
                );

                cmd.Parameters.AddWithValue(
                    "@pincode",
                    model.Pincode
                );

                cmd.Parameters.AddWithValue(
                    "@addresstype",
                    model.AddressType
                );

                cmd.Parameters.AddWithValue(
                    "@isdefault",
                    model.IsDefault
                );

                var addressId =
                    await cmd.ExecuteScalarAsync();

                return new OkObjectResult(new
                {
                    success = true,
                    addressId,
                    message = "Address added successfully"
                });
            }
            catch (Exception ex)
            {
                return new ObjectResult(new
                {
                    success = false,
                    message = ex.Message,
                    innerException =
                        ex.InnerException?.Message
                })
                {
                    StatusCode = 500
                };
            }
        }


        public async Task<IActionResult> GetAddressList(
    int userId)
        {
            try
            {
                using var con =
                    new NpgsqlConnection(DbConnection);

                await con.OpenAsync();

                string query = @"
SELECT
    id,
    userid,
    full_name,
    mobile,
    alternate_mobile,
    address_line1,
    address_line2,
    landmark,
    city,
    state,
    country,
    pincode,
    address_type,
    is_default,
    createdat
FROM user_addresses
WHERE userid = @userid
ORDER BY
    is_default DESC,
    id DESC";

                using var cmd =
                    new NpgsqlCommand(query, con);

                cmd.Parameters.AddWithValue(
                    "@userid",
                    userId
                );

                var data =
                    new List<object>();

                using var reader =
                    await cmd.ExecuteReaderAsync();

                while (await reader.ReadAsync())
                {
                    data.Add(new
                    {
                        Id =
                            Convert.ToInt32(
                                reader["id"]
                            ),

                        UserId =
                            Convert.ToInt32(
                                reader["userid"]
                            ),

                        FullName =
                            reader["full_name"]
                            ?.ToString(),

                        Mobile =
                            reader["mobile"]
                            ?.ToString(),

                        AlternateMobile =
                            reader["alternate_mobile"]
                            == DBNull.Value
                            ? null
                            : reader["alternate_mobile"]
                            .ToString(),

                        AddressLine1 =
                            reader["address_line1"]
                            ?.ToString(),

                        AddressLine2 =
                            reader["address_line2"]
                            == DBNull.Value
                            ? null
                            : reader["address_line2"]
                            .ToString(),

                        Landmark =
                            reader["landmark"]
                            == DBNull.Value
                            ? null
                            : reader["landmark"]
                            .ToString(),

                        City =
                            reader["city"]
                            ?.ToString(),

                        State =
                            reader["state"]
                            ?.ToString(),

                        Country =
                            reader["country"]
                            ?.ToString(),

                        Pincode =
                            reader["pincode"]
                            ?.ToString(),

                        AddressType =
                            reader["address_type"]
                            ?.ToString(),

                        IsDefault =
                            Convert.ToBoolean(
                                reader["is_default"]
                            ),

                        CreatedAt =
                            Convert.ToDateTime(
                                reader["createdat"]
                            )
                    });
                }

                return new OkObjectResult(new
                {
                    success = true,
                    userId,
                    total = data.Count,
                    data
                });
            }
            catch (Exception ex)
            {
                return new ObjectResult(new
                {
                    success = false,
                    message = ex.Message,
                    innerException =
                        ex.InnerException?.Message
                })
                {
                    StatusCode = 500
                };
            }
        }



        public async Task<IActionResult> DeleteAddress(int id, int userId)
        {
            try
            {
                using var con =
                    new NpgsqlConnection(DbConnection);
                await con.OpenAsync();
                string query = @"
                    DELETE FROM user_addresses
                    WHERE id = @id AND userid = @userid
                ";

                using var cmd = new NpgsqlCommand(query, con);
                cmd.Parameters.AddWithValue("@id", id);
                cmd.Parameters.AddWithValue("@userid", userId);

                var result = await cmd.ExecuteNonQueryAsync();
                if (result > 0)
                {
                    return new OkObjectResult(new
                    {
                        success = true,
                        message = "Address deleted successfully."
                    });
                }
                return new NotFoundObjectResult(new
                {
                    success = false,
                    message = "Address not found."
                });
            }
            catch (Exception ex)
            {
                return new ObjectResult(new
                {
                    success = false,
                    message = ex.Message,
                    innerException =
                        ex.InnerException?.Message
                })
                {
                    StatusCode = 500
                };
            }
        }










    }
}
