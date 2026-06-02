using CloudinaryDotNet;
using CloudinaryDotNet.Actions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Identity.Client;
using Npgsql;
using NuGet.Packaging.Signing;

namespace Ecommerce_Backend.Models.DatabaseLayer
{
    public partial interface IDatabaseLayer
    {
        Task<IActionResult> GetBlog();
        Task<IActionResult> AddBlog([FromForm] BlogModel blog);
        Task<IActionResult> UpdateBlog(int id, [FromForm] BlogModel blog);
        Task<IActionResult> DeleteBlog(int id);
    }

    public partial class DataBaseLayer : IDatabaseLayer
    {
        public async Task<IActionResult> GetBlog()
        {
            try
            {
                var blogs = new List<BlogModel>();

                using var con = new NpgsqlConnection(DbConnection);
                await con.OpenAsync();

                using var cmd = new NpgsqlCommand(
                    @"SELECT id, blog_name, description, blog_image, status, created_at
              FROM blogs
              ORDER BY created_at DESC",
                    con
                );

                using var reader = await cmd.ExecuteReaderAsync();

                while (await reader.ReadAsync())
                {
                    blogs.Add(new BlogModel
                    {
                        id = reader.GetInt32(0),
                        BlogName = reader.IsDBNull(1) ? "" : reader.GetString(1),
                        Description = reader.IsDBNull(2) ? "" : reader.GetString(2),
                        BlogImg = reader.IsDBNull(3) ? "" : reader.GetString(3),
                        Status = reader.GetBoolean(4),
                        CreatedAt = reader.GetDateTime(5)
                    });
                }

                var result = blogs.Select(b => new
                {
                    b.id,
                    b.BlogName,
                    b.Description,
                    b.BlogImg,
                    b.Status,
                    b.CreatedAt
                });

                return new OkObjectResult(new
                {
                    status = true,
                    message = "Blogs retrieved successfully",
                    data = result
                });
            }
            catch (Exception ex) { return new ObjectResult(new { status = false, message = "Error retrieving blogs: " + ex.Message }) { StatusCode = 500 }; }
        }
        public async Task<IActionResult> AddBlog([FromForm] BlogModel blog)
        {
            try
            {
                using var con = new NpgsqlConnection(DbConnection);
                await con.OpenAsync();

                // Duplicate Check
                using var checkCmd = new NpgsqlCommand(
                    "SELECT COUNT(*) FROM blogs WHERE LOWER(blog_name) = LOWER(@blog_name)",
                    con
                );

                checkCmd.Parameters.AddWithValue("@blog_name", blog.BlogName ?? "");

                var count = Convert.ToInt32(await checkCmd.ExecuteScalarAsync());

                if (count > 0)
                {
                    return new BadRequestObjectResult(new
                    {
                        status = false,
                        message = "Blog with the same name already exists"
                    });
                }
                // Cloudinary Upload
                var account = new Account(
                    _configuration["CloudinarySettings:CloudName"],
                    _configuration["CloudinarySettings:ApiKey"],
                    _configuration["CloudinarySettings:ApiSecret"]
                );

                var cloudinary = new Cloudinary(account);
                string imageUrl = "";

                using (var stream = blog.BlogFile?.OpenReadStream())
                {
                    if (stream != null)
                    {
                        var uploadParams = new CloudinaryDotNet.Actions.ImageUploadParams
                        {
                            File = new CloudinaryDotNet.FileDescription(blog.BlogFile.FileName, stream),
                            Folder = "blog_images"
                        };
                        var uploadResult = await cloudinary.UploadAsync(uploadParams);
                        imageUrl = uploadResult.SecureUrl.ToString();
                    }
                }
                using var cmd = new NpgsqlCommand(
                    "INSERT INTO blogs (blog_name, description, blog_image, status) VALUES (@blog_name, @description, @blog_image, @status)",
                    con
                );

                cmd.Parameters.AddWithValue("@blog_name", blog.BlogName ?? "");
                cmd.Parameters.AddWithValue("@description", blog.Description ?? "");
                cmd.Parameters.AddWithValue("@blog_image", imageUrl);
                cmd.Parameters.AddWithValue("@status", blog.Status);

                await cmd.ExecuteNonQueryAsync();
                return new OkObjectResult(new
                {
                    status = true,
                    message = "Blog added successfully"
                });
            }
            catch (Exception ex)
            {
                return new ObjectResult(new
                {
                    status = false,
                    message = "Error adding blog: " + ex.Message
                })
                { StatusCode = 500 };
            }
        }
        public async Task<IActionResult> UpdateBlog(int id, [FromForm] BlogModel blog)
        {
            try
            {
                using var con = new NpgsqlConnection(DbConnection);
                await con.OpenAsync();

                // Check if blog exists
                using var checkCmd = new NpgsqlCommand(
                    "SELECT COUNT(*) FROM blogs WHERE id = @id",
                    con
                );

                checkCmd.Parameters.AddWithValue("@id", id);

                var count = Convert.ToInt32(await checkCmd.ExecuteScalarAsync());

                if (count == 0)
                {
                    return new NotFoundObjectResult(new
                    {
                        status = false,
                        message = "Blog not found"
                    });
                }

                string imageUrl = blog.BlogImg ?? "";

                // Upload new image if provided
                if (blog.BlogFile != null && blog.BlogFile.Length > 0)
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
                        "SELECT blog_image FROM blogs WHERE id=@id",
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
                    using var stream = blog.BlogFile.OpenReadStream();

                    var uploadParams = new ImageUploadParams
                    {
                        File = new FileDescription(
                            blog.BlogFile.FileName,
                            stream
                        ),
                        Folder = "blog_images"
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

                // Update Blog
                using var cmd = new NpgsqlCommand(@"
            UPDATE blogs
            SET
                blog_name = @blog_name,
                description = @description,
                blog_image = @blog_image,
                status = @status
            WHERE id = @id
        ", con);

                cmd.Parameters.AddWithValue("@blog_name", blog.BlogName ?? "");
                cmd.Parameters.AddWithValue("@description", blog.Description ?? "");
                cmd.Parameters.AddWithValue("@blog_image", imageUrl);
                cmd.Parameters.AddWithValue("@status", blog.Status);
                cmd.Parameters.AddWithValue("@id", id);

                await cmd.ExecuteNonQueryAsync();

                return new OkObjectResult(new
                {
                    status = true,
                    message = "Blog updated successfully",
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

        public async Task<IActionResult> DeleteBlog(int id)
        {
            try
            {
                using var con = new NpgsqlConnection(DbConnection);
                await con.OpenAsync();

                string imageUrl = "";

                // Get Blog Image URL
                using (var getCmd = new NpgsqlCommand(
                    "SELECT blog_image FROM blogs WHERE id=@id",
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

                // Delete image from Cloudinary
                if (!string.IsNullOrEmpty(imageUrl))
                {
                    try
                    {
                        var account = new Account(
                            _configuration["CloudinarySettings:CloudName"],
                            _configuration["CloudinarySettings:ApiKey"],
                            _configuration["CloudinarySettings:ApiSecret"]
                        );

                        var cloudinary = new Cloudinary(account);

                        var uri = new Uri(imageUrl);
                        var segments = uri.AbsolutePath.Split("/upload/");

                        if (segments.Length > 1)
                        {
                            var publicId = segments[1];

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
                        // Ignore Cloudinary delete errors
                    }
                }

                // Delete Blog from Database
                using var deleteCmd = new NpgsqlCommand(
                    "DELETE FROM blogs WHERE id=@id",
                    con);

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
 