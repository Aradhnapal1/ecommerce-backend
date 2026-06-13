using CloudinaryDotNet;
using CloudinaryDotNet.Actions;
using Ecommerce_Backend.Helpers;
using Microsoft.AspNetCore.Mvc;
using Npgsql;

namespace Ecommerce_Backend.Models.DatabaseLayer
{
    public partial interface IDatabaseLayer
    {
        Task<IActionResult> AddBrand(BrandModel brand);
        Task<IActionResult> GetAllBrands();
        Task<IActionResult> UpdateBrand(int id, [FromForm] BrandModel brand);
        Task<IActionResult> DeleteBrand(int id);
    }

    public partial class DataBaseLayer : IDatabaseLayer
    {

        public async Task<IActionResult> AddBrand(BrandModel brand)
        {
            try
            {
                using var con = new NpgsqlConnection(DbConnection);
                await con.OpenAsync();

                // Duplicate Check
                using var checkCmd = new NpgsqlCommand(@"
                    SELECT COUNT(*)
                    FROM brands
                    WHERE LOWER(brand_name)=LOWER(@brand_name)
                ", con);

                checkCmd.Parameters.AddWithValue("@brand_name", brand.BrandName ?? "");

                var count = Convert.ToInt32(await checkCmd.ExecuteScalarAsync());

                if (count > 0)
                {
                    return new BadRequestObjectResult(new
                    {
                        status = false,
                        message = "Brand already exists"
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

                using (var stream = brand.BrandFile!.OpenReadStream())
                {
                    var uploadParams = new ImageUploadParams
                    {
                        File = new FileDescription(
                            brand.BrandFile.FileName,
                            stream
                        ),
                        Folder = "brands"
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

                // Insert Brand
                var slug = await SlugHelper.GenerateUniqueSlugAsync(
                    con, "brands", "slug", brand.BrandName ?? "");

                using var cmd = new NpgsqlCommand(@"
                    INSERT INTO brands
                    (
                        brand_name,
                        slug,
                        brand_img,
                        is_active
                    )
                    VALUES
                    (
                        @brand_name,
                        @slug,
                        @brand_img,
                        @is_active
                    )
                    RETURNING id;
                ", con);

                cmd.Parameters.AddWithValue("@brand_name", brand.BrandName ?? "");
                cmd.Parameters.AddWithValue("@slug", slug);
                cmd.Parameters.AddWithValue("@brand_img", imageUrl);
                cmd.Parameters.AddWithValue("@is_active", brand.IsActive);

                var id = Convert.ToInt32(await cmd.ExecuteScalarAsync());

                return new OkObjectResult(new
                {
                    status = true,
                    message = "Brand added successfully",
                    id = id,
                    brandName = brand.BrandName,
                    slug = slug,
                    brandImage = imageUrl,
                    isActive = brand.IsActive
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


        public async Task<IActionResult> GetAllBrands()
        {
            try
            {
                var brands = new List<BrandModel>();
                using var con = new NpgsqlConnection(DbConnection);
                await con.OpenAsync();
                using var cmd = new NpgsqlCommand(
                    "SELECT id, brand_name, slug, brand_img, is_active FROM brands",
                    con
                );
                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    brands.Add(new BrandModel
                    {
                        Id = reader.GetInt32(0),
                        BrandName = reader.GetString(1),
                        Slug = reader.GetString(2),
                        BrandImg = reader.GetString(3),
                        IsActive = reader.GetBoolean(4)
                    });
                }
                return new OkObjectResult(new
                {
                    status = true,
                    message = "Brands retrieved successfully",
                    data = brands
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


        public async Task<IActionResult> UpdateBrand(int id, [FromForm] BrandModel brand)
        {
            try
            {
                using var con = new NpgsqlConnection(DbConnection);
                await con.OpenAsync();
                // Check if brand exists
                using var checkCmd = new NpgsqlCommand(
                    "SELECT COUNT(*) FROM brands WHERE id=@id",
                    con
                );
                checkCmd.Parameters.AddWithValue("@id", id);
                var count = Convert.ToInt32(await checkCmd.ExecuteScalarAsync());
                if (count == 0)
                {
                    return new NotFoundObjectResult(new
                    {
                        status = false,
                        message = "Brand not found"
                    });
                }
                // Cloudinary Upload (if new file provided)
                // Cloudinary Upload (if new file provided)
                string imageUrl = brand.BrandImg ?? "";

                if (brand.BrandFile != null && brand.BrandFile.Length > 0)
                {
                    var account = new Account(
                        _configuration["CloudinarySettings:CloudName"],
                        _configuration["CloudinarySettings:ApiKey"],
                        _configuration["CloudinarySettings:ApiSecret"]
                    );

                    var cloudinary = new Cloudinary(account);

                    // Get old image url from database
                    string oldImageUrl = "";

                    using (var getCmd = new NpgsqlCommand(
                        "SELECT brand_img FROM brands WHERE id=@id",
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

                                // Remove file extension
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
                    using var stream = brand.BrandFile.OpenReadStream();

                    var uploadParams = new ImageUploadParams
                    {
                        File = new FileDescription(
                            brand.BrandFile.FileName,
                            stream
                        ),
                        Folder = "brands"
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
                // Update Brand
                var slug = await SlugHelper.GenerateUniqueSlugAsync(
                    con, "brands", "slug", brand.BrandName ?? "", id);

                using var cmd = new NpgsqlCommand(@"
                    UPDATE brands SET
                    brand_name=@brand_name,
                    slug=@slug,
                    brand_img=@brand_img,
                    is_active=@is_active
                    WHERE id=@id;
                ", con);
                cmd.Parameters.AddWithValue("@brand_name", brand.BrandName ?? "");
                cmd.Parameters.AddWithValue("@slug", slug);
                cmd.Parameters.AddWithValue("@brand_img", imageUrl);
                cmd.Parameters.AddWithValue("@is_active", brand.IsActive);
                cmd.Parameters.AddWithValue("@id", id);
                await cmd.ExecuteNonQueryAsync();
                return new OkObjectResult(new
                {
                    status = true,
                    message = "Brand updated successfully",
                    id = id,
                    brandName = brand.BrandName,
                    slug = slug,
                    brandImage = imageUrl,
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


        public async Task<IActionResult> DeleteBrand(int id)
        {
            try
            {
                using var con = new NpgsqlConnection(DbConnection);
                await con.OpenAsync();

                string imageUrl = "";

                // Get Brand Image URL
                using (var getCmd = new NpgsqlCommand(
                    "SELECT brand_img FROM brands WHERE id=@id", con))
                {
                    getCmd.Parameters.AddWithValue("@id", id);

                    var result = await getCmd.ExecuteScalarAsync();

                    if (result == null)
                    {
                        return new NotFoundObjectResult(new
                        {
                            status = false,
                            message = "Brand not found"
                        });
                    }

                    imageUrl = result.ToString() ?? "";
                }

                // Delete image from Cloudinary
                if (!string.IsNullOrEmpty(imageUrl))
                {
                    var account = new Account(
                        _configuration["CloudinarySettings:CloudName"],
                        _configuration["CloudinarySettings:ApiKey"],
                        _configuration["CloudinarySettings:ApiSecret"]
                    );

                    var cloudinary = new Cloudinary(account);

                    // Example URL:
                    // https://res.cloudinary.com/.../upload/v1780125501/brands/abc123.png

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

                        var deleteParams = new DeletionParams(publicId);

                        await cloudinary.DestroyAsync(deleteParams);
                    }
                }

                // Delete Brand from Database
                using var deleteCmd = new NpgsqlCommand(
                    "DELETE FROM brands WHERE id=@id",
                    con);

                deleteCmd.Parameters.AddWithValue("@id", id);

                await deleteCmd.ExecuteNonQueryAsync();

                return new OkObjectResult(new
                {
                    status = true,
                    message = "Brand and image deleted successfully"
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