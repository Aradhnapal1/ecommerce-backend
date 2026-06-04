using CloudinaryDotNet;
using CloudinaryDotNet.Actions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Identity.Client;
using Npgsql;
using NuGet.Packaging.Signing;
using System.Reflection.Metadata;
namespace Ecommerce_Backend.Models.DatabaseLayer

{
    public partial interface IDatabaseLayer
    {
        Task<List<BannerModel>> GetBanner();
        Task<IActionResult> AddBanner([FromForm] BannerModel banner);
        Task<IActionResult> UpdateBanner(int id, [FromForm] BannerModel banner);
        Task<IActionResult> DeleteBanner(int id);
    }

    public partial class DataBaseLayer : IDatabaseLayer
    {
        public async Task<List<BannerModel>> GetBanner()
        {
            var banners = new List<BannerModel>();

            try
            {
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
                    banners.Add(new BannerModel
                    {
                        Id = reader.GetInt32(0),
                        BannerName = reader.IsDBNull(1) ? string.Empty : reader.GetString(1),
                        BannerDescription = reader.IsDBNull(2) ? string.Empty : reader.GetString(2),
                        BannerImg = reader.IsDBNull(3) ? string.Empty : reader.GetString(3),
                        BannerType = reader.IsDBNull(4) ? string.Empty : reader.GetString(4),
                        BannerLink = reader.IsDBNull(5) ? string.Empty : reader.GetString(5),
                        Status = !reader.IsDBNull(6) && reader.GetBoolean(6),
                        CreatedAt = reader.IsDBNull(7)
                            ? DateTime.MinValue
                            : reader.GetDateTime(7)
                    });
                }

                return banners;
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to fetch banners: {ex.Message}");
            }
        }

        public async Task<IActionResult> AddBanner([FromForm] BannerModel banner)
        {
            try
            {
                using var con = new NpgsqlConnection(DbConnection);
                await con.OpenAsync();

                // Duplicate Check
                using var checkCmd = new NpgsqlCommand(
                    "SELECT COUNT(*) FROM banners WHERE LOWER(banner_name) = LOWER(@banner_name)",
                    con
                );

                checkCmd.Parameters.AddWithValue("@banner_name", banner.BannerName ?? "");

                var count = Convert.ToInt32(await checkCmd.ExecuteScalarAsync());

                if (count > 0)
                {
                    return new BadRequestObjectResult(new
                    {
                        status = false,
                        message = "Banner with the same name already exists"
                    });
                }

                // Cloudinary Uploadd
                var account = new Account(
                    _configuration["CloudinarySettings:CloudName"],
                    _configuration["CloudinarySettings:ApiKey"],
                    _configuration["CloudinarySettings:ApiSecret"]
                );

                var cloudinary = new Cloudinary(account);

                string imageUrl = "";

                using (var stream = banner.BannerFile?.OpenReadStream())
                {
                    if (stream != null)
                    {
                        var uploadParams = new CloudinaryDotNet.Actions.ImageUploadParams
                        {
                            File = new CloudinaryDotNet.FileDescription(
                                banner.BannerFile.FileName,
                                stream
                            ),
                            Folder = "banner_images"
                        };

                        var uploadResult = await cloudinary.UploadAsync(uploadParams);

                        imageUrl = uploadResult.SecureUrl.ToString();
                    }
                }

                using var cmd = new NpgsqlCommand(
                    @"INSERT INTO banners
            (
                banner_name,
                banner_description,
                banner_image,
                banner_type,
                banner_link,
                active
            )
            VALUES
            (
                @banner_name,
                @banner_description,
                @banner_image,
                @banner_type,
                @banner_link,
                @active
            )",
                    con
                );

                cmd.Parameters.AddWithValue("@banner_name", banner.BannerName ?? "");
                cmd.Parameters.AddWithValue("@banner_description", banner.BannerDescription ?? "");
                cmd.Parameters.AddWithValue("@banner_image", imageUrl);
                cmd.Parameters.AddWithValue("@banner_type", banner.BannerType ?? "");
                cmd.Parameters.AddWithValue("@banner_link", banner.BannerLink ?? "");
                cmd.Parameters.AddWithValue("@active", banner.Status);

                await cmd.ExecuteNonQueryAsync();

                return new OkObjectResult(new
                {
                    status = true,
                    message = "Banner added successfully",
                });
            }
            catch (Exception ex)
            {
                return new ObjectResult(new
                {
                    status = false,
                    message = "Error adding banner: " + ex.Message
                })
                {
                    StatusCode = 500
                };
            }
        }

        public async Task<IActionResult> UpdateBanner(int id, [FromForm] BannerModel banner)
        {
            try
            {
                using var con = new NpgsqlConnection(DbConnection);
                await con.OpenAsync();

                // Check if Banner exists
                using var checkCmd = new NpgsqlCommand(
                    "SELECT COUNT(*) FROM banners WHERE id = @id",
                    con
                );

                checkCmd.Parameters.AddWithValue("@id", id);

                var count = Convert.ToInt32(await checkCmd.ExecuteScalarAsync());

                if (count == 0)
                {
                    return new NotFoundObjectResult(new
                    {
                        status = false,
                        message = "Banner not found"
                    });
                }

                string imageUrl = banner.BannerImg ?? "";

                // Upload new image if provided
                if (banner.BannerFile != null && banner.BannerFile.Length > 0)
                {
                    var account = new Account(
                        _configuration["CloudinarySettings:CloudName"],
                        _configuration["CloudinarySettings:ApiKey"],
                        _configuration["CloudinarySettings:ApiSecret"]
                    );

                    var cloudinary = new Cloudinary(account);

                    // Get old image url
                    string oldImageUrl = "";

                    using (var getCmd = new NpgsqlCommand(
                        "SELECT banner_image FROM banners WHERE id = @id",
                        con))
                    {
                        getCmd.Parameters.AddWithValue("@id", id);

                        var result = await getCmd.ExecuteScalarAsync();
                        oldImageUrl = result?.ToString() ?? "";
                    }

                    // Delete old image from Cloudinary
                    if (!string.IsNullOrEmpty(oldImageUrl))
                    {
                        try
                        {
                            var uri = new Uri(oldImageUrl);

                            var parts = uri.AbsolutePath.Split("/upload/");

                            if (parts.Length > 1)
                            {
                                var publicId = parts[1];

                                // Remove version number
                                publicId = System.Text.RegularExpressions.Regex
                                    .Replace(publicId, @"^v\d+\/", "");

                                // Remove extension
                                publicId = Path.Combine(
                                    Path.GetDirectoryName(publicId) ?? "",
                                    Path.GetFileNameWithoutExtension(publicId)
                                ).Replace("\\", "/");

                                await cloudinary.DestroyAsync(
                                    new DeletionParams(publicId)
                                );
                            }
                        }
                        catch
                        {
                            // Ignore delete errors
                        }
                    }

                    // Upload new image
                    using var stream = banner.BannerFile.OpenReadStream();

                    var uploadParams = new ImageUploadParams
                    {
                        File = new FileDescription(
                            banner.BannerFile.FileName,
                            stream
                        ),
                        Folder = "banner_images"
                    };

                    var uploadResult = await cloudinary.UploadAsync(uploadParams);

                    if (uploadResult.Error != null)
                    {
                        return new BadRequestObjectResult(new
                        {
                            status = false,
                            message = uploadResult.Error.Message
                        });
                    }

                    imageUrl = uploadResult.SecureUrl.ToString();
                }

                // Update Banner
                using var cmd = new NpgsqlCommand(@"
            UPDATE banners
            SET
                banner_name = @banner_name,
                banner_description = @banner_description,
                banner_image = @banner_image,
                banner_type = @banner_type,
                banner_link = @banner_link,
                active = @active
            WHERE id = @id
        ", con);

                cmd.Parameters.AddWithValue("@banner_name", banner.BannerName ?? "");
                cmd.Parameters.AddWithValue("@banner_description", banner.BannerDescription ?? "");
                cmd.Parameters.AddWithValue("@banner_image", imageUrl);
                cmd.Parameters.AddWithValue("@banner_type", banner.BannerType ?? "");
                cmd.Parameters.AddWithValue("@banner_link", banner.BannerLink ?? "");
                cmd.Parameters.AddWithValue("@active", banner.Status);
                cmd.Parameters.AddWithValue("@id", id);

                await cmd.ExecuteNonQueryAsync();

                return new OkObjectResult(new
                {
                    status = true,
                    message = "Banner updated successfully"
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

        public async Task<IActionResult> DeleteBanner(int id)
        {
            try
            {
                using var con = new NpgsqlConnection(DbConnection);
                await con.OpenAsync();
                string imageUrl = "";
                using (var getCmd = new NpgsqlCommand(
                    "SELECT banner_image FROM banners WHERE id = @id",
                    con))
                {
                    getCmd.Parameters.AddWithValue("@id", id);
                    var result = await getCmd.ExecuteScalarAsync();
                    if (result == null)
                    {
                        return new NotFoundObjectResult(new
                        {
                            status = false,
                            message = "Blog not found"
                        });
                    }

                    imageUrl = result.ToString() ?? "";
                }
                if(!string.IsNullOrEmpty(imageUrl))
                {
                    var account = new Account(
                        _configuration["CloudinarySettings:CloudName"],
                        _configuration["CloudinarySettings:ApiKey"],
                        _configuration["CloudinarySettings:ApiSecret"]
                    );
                    var cloudinary = new Cloudinary(account);
                    try
                    {
                        var uri = new Uri(imageUrl);
                        var parts = uri.AbsolutePath.Split("/upload/");
                        if (parts.Length > 1)
                        {
                            var publicId = parts[1];
                            // Remove version number
                            publicId = System.Text.RegularExpressions.Regex
                                .Replace(publicId, @"^v\d+\/", "");
                            // Remove extension
                            publicId = Path.Combine(
                                Path.GetDirectoryName(publicId) ?? "",
                                Path.GetFileNameWithoutExtension(publicId)
                            ).Replace("\\", "/");
                            await cloudinary.DestroyAsync(
                                new DeletionParams(publicId)
                            );
                        }
                    }
                    catch
                    {
                        // Ignore delete errors
                    }
                } 

                using var deleteCmd = new NpgsqlCommand(
                    "DELETE FROM banners WHERE id = @id",
                    con
                );
                deleteCmd.Parameters.AddWithValue("@id", id);

                await deleteCmd.ExecuteNonQueryAsync();

                return new OkObjectResult(new
                {
                    status = true,
                    message = "Blog  deleted successfully"
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
    }
}