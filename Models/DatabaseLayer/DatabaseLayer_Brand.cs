using CloudinaryDotNet;
using CloudinaryDotNet.Actions;
using Microsoft.AspNetCore.Mvc;
using Npgsql;

namespace Ecommerce_Backend.Models.DatabaseLayer
{
    public partial interface IDatabaseLayer
    {
        Task<IActionResult> AddBrand(BrandModel brand);
        Task<IActionResult> GetAllBrands();
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
                using var cmd = new NpgsqlCommand(@"
                    INSERT INTO brands
                    (
                        brand_name,
                        brand_img,
is_active
                    )
                    VALUES
                    (
                        @brand_name,
                        @brand_img,
                        @is_active
                    )
                    RETURNING id;
                ", con);

                cmd.Parameters.AddWithValue("@brand_name", brand.BrandName ?? "");
                cmd.Parameters.AddWithValue("@brand_img", imageUrl);
                cmd.Parameters.AddWithValue("@is_active", brand.IsActive);

                var id = Convert.ToInt32(await cmd.ExecuteScalarAsync());

                return new OkObjectResult(new
                {
                    status = true,
                    message = "Brand added successfully",
                    id = id,
                    brandName = brand.BrandName,
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
                    "SELECT id, brand_name, brand_img, is_active FROM brands",
                    con
                );
                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    brands.Add(new BrandModel
                    {
                        Id = reader.GetInt32(0),
                        BrandName = reader.GetString(1),
                        BrandImg = reader.GetString(2),
                        IsActive = reader.GetBoolean(3)
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


    }
}