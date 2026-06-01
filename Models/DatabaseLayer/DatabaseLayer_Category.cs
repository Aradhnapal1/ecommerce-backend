using CloudinaryDotNet;
using CloudinaryDotNet.Actions;
using Microsoft.AspNetCore.Mvc;
using Npgsql;

namespace Ecommerce_Backend.Models.DatabaseLayer
{
   public partial interface IDatabaseLayer
    {
        Task<IActionResult> AddCategory([FromForm] CategoryModel category);
        Task<IActionResult> GetCategories();
        Task<IActionResult> UpdateCategory(int id, [FromForm] CategoryModel category);
        Task<IActionResult> DeleteCategory(int id);
    }
    public partial class DataBaseLayer : IDatabaseLayer
    {

        public async Task<IActionResult> AddCategory(CategoryModel category)
        {
            try
            {
                using var con = new NpgsqlConnection(DbConnection);
                await con.OpenAsync();

                // Validation
                if (string.IsNullOrWhiteSpace(category.CategoryName))
                {
                    return new BadRequestObjectResult(new
                    {
                        status = false,
                        message = "Category name is required"
                    });
                }

                // Check Parent Category Exists
                if (category.ParentId.HasValue)
                {
                    using var parentCmd = new NpgsqlCommand(
                        "SELECT COUNT(*) FROM categories WHERE id=@id",
                        con);

                    parentCmd.Parameters.AddWithValue("@id", category.ParentId.Value);

                    var parentCount =
                        Convert.ToInt32(await parentCmd.ExecuteScalarAsync());

                    if (parentCount == 0)
                    {
                        return new BadRequestObjectResult(new
                        {
                            status = false,
                            message = "Parent category not found"
                        });
                    }
                }

                // Duplicate Check
                using (var duplicateCmd = new NpgsqlCommand(@"
            SELECT COUNT(*)
            FROM categories
            WHERE LOWER(category_name)=LOWER(@category_name)
            AND (
                parent_id = @parent_id
                OR
                (parent_id IS NULL AND @parent_id IS NULL)
            )
        ", con))
                {
                    duplicateCmd.Parameters.AddWithValue(
                        "@category_name",
                        category.CategoryName.Trim());

                    duplicateCmd.Parameters.AddWithValue(
                        "@parent_id",
                        category.ParentId.HasValue
                            ? category.ParentId.Value
                            : DBNull.Value);

                    var duplicateCount =
                        Convert.ToInt32(await duplicateCmd.ExecuteScalarAsync());

                    if (duplicateCount > 0)
                    {
                        return new BadRequestObjectResult(new
                        {
                            status = false,
                            message = "Category already exists"
                        });
                    }
                }

                string imageUrl = "";

                // Cloudinary Upload
                if (category.CategoryFile != null &&
                    category.CategoryFile.Length > 0)
                {
                    var account = new Account(
                        _configuration["CloudinarySettings:CloudName"],
                        _configuration["CloudinarySettings:ApiKey"],
                        _configuration["CloudinarySettings:ApiSecret"]
                    );

                    var cloudinary = new Cloudinary(account);

                    using var stream =
                        category.CategoryFile.OpenReadStream();

                    var uploadParams = new ImageUploadParams
                    {
                        File = new FileDescription(
                            category.CategoryFile.FileName,
                            stream
                        ),
                        Folder = "categories"
                    };

                    var uploadResult =
                        await cloudinary.UploadAsync(uploadParams);

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

                // Insert Category
                using var cmd = new NpgsqlCommand(@"
            INSERT INTO categories
            (
                category_name,
                parent_id,
                category_image,
                is_active
            )
            VALUES
            (
                @category_name,
                @parent_id,
                @category_image,
                @is_active
            )
            RETURNING id;
        ", con);

                cmd.Parameters.AddWithValue(
                    "@category_name",
                    category.CategoryName.Trim());

                cmd.Parameters.AddWithValue(
                    "@parent_id",
                    category.ParentId.HasValue
                        ? category.ParentId.Value
                        : DBNull.Value);

                cmd.Parameters.AddWithValue(
                    "@category_image",
                    imageUrl);

                cmd.Parameters.AddWithValue(
                    "@is_active",
                    category.IsActive);

                var id =
                    Convert.ToInt32(await cmd.ExecuteScalarAsync());

                return new OkObjectResult(new
                {
                    status = true,
                    message = "Category added successfully",
                    id = id,
                    categoryName = category.CategoryName,
                    parentId = category.ParentId,
                    categoryImage = imageUrl,
                    isActive = category.IsActive
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
        public async Task<IActionResult> GetCategories()
        {
            try
            {
                using var con = new NpgsqlConnection(DbConnection);
                await con.OpenAsync();

                using var cmd = new NpgsqlCommand(@"
            SELECT
                id,
                category_name,
                parent_id,
                category_image,
                is_active
            FROM categories
            ORDER BY id
        ", con);

                using var reader = await cmd.ExecuteReaderAsync();

                var categories = new List<CategoryTreeModel>();

                while (await reader.ReadAsync())
                {
                    categories.Add(new CategoryTreeModel
                    {
                        Id = reader.GetInt32(0),
                        CategoryName = reader.GetString(1),
                        ParentId = reader.IsDBNull(2)
                            ? null
                            : reader.GetInt32(2),
                        CategoryImage = reader.IsDBNull(3)
                            ? null
                            : reader.GetString(3),
                        IsActive = reader.GetBoolean(4)
                    });
                }

                // Dictionary for fast lookup
                var lookup = categories.ToDictionary(x => x.Id);

                // Root Categories
                var roots = new List<CategoryTreeModel>();

                foreach (var category in categories)
                {
                    if (category.ParentId == null)
                    {
                        roots.Add(category);
                    }
                    else if (lookup.ContainsKey(category.ParentId.Value))
                    {
                        lookup[category.ParentId.Value]
                            .Children
                            .Add(category);
                    }
                }

                return new OkObjectResult(new
                {
                    status = true,
                    data = roots
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


        public async Task<IActionResult> UpdateCategory(int id, CategoryModel category)
        {
            try
            {
                using var con = new NpgsqlConnection(DbConnection);
                await con.OpenAsync();

                // Check category exists
                using (var checkCmd = new NpgsqlCommand(
                    "SELECT COUNT(*) FROM categories WHERE id=@id",
                    con))
                {
                    checkCmd.Parameters.AddWithValue("@id", id);

                    var count = Convert.ToInt32(
                        await checkCmd.ExecuteScalarAsync());

                    if (count == 0)
                    {
                        return new BadRequestObjectResult(new
                        {
                            status = false,
                            message = "Category not found"
                        });
                    }
                }

                // Parent validation
                if (category.ParentId.HasValue)
                {
                    if (category.ParentId.Value == id)
                    {
                        return new BadRequestObjectResult(new
                        {
                            status = false,
                            message = "Category cannot be its own parent"
                        });
                    }

                    using var parentCmd = new NpgsqlCommand(
                        "SELECT COUNT(*) FROM categories WHERE id=@parentId",
                        con);

                    parentCmd.Parameters.AddWithValue(
                        "@parentId",
                        category.ParentId.Value);

                    var parentCount = Convert.ToInt32(
                        await parentCmd.ExecuteScalarAsync());

                    if (parentCount == 0)
                    {
                        return new BadRequestObjectResult(new
                        {
                            status = false,
                            message = "Parent category not found"
                        });
                    }
                }

                string imageUrl = category.CategoryImage ?? "";

                // New image uploaded
                if (category.CategoryFile != null &&
                    category.CategoryFile.Length > 0)
                {
                    var account = new Account(
                        _configuration["CloudinarySettings:CloudName"],
                        _configuration["CloudinarySettings:ApiKey"],
                        _configuration["CloudinarySettings:ApiSecret"]
                    );

                    var cloudinary = new Cloudinary(account);

                    // Get old image
                    string oldImageUrl = "";

                    using (var getCmd = new NpgsqlCommand(
                        "SELECT category_image FROM categories WHERE id=@id",
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

                                publicId =
                                    System.Text.RegularExpressions.Regex
                                    .Replace(publicId, @"^v\d+\/", "");

                                publicId = Path.Combine(
                                    Path.GetDirectoryName(publicId) ?? "",
                                    Path.GetFileNameWithoutExtension(publicId)
                                ).Replace("\\", "/");

                                await cloudinary.DestroyAsync(
                                    new DeletionParams(publicId));
                            }
                        }
                        catch
                        {
                            // Ignore delete errors
                        }
                    }

                    // Upload new image
                    using var stream =
                        category.CategoryFile.OpenReadStream();

                    var uploadParams = new ImageUploadParams
                    {
                        File = new FileDescription(
                            category.CategoryFile.FileName,
                            stream
                        ),
                        Folder = "categories"
                    };

                    var uploadResult =
                        await cloudinary.UploadAsync(uploadParams);

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

                // Update Category
                using var cmd = new NpgsqlCommand(@"
            UPDATE categories
            SET
                category_name = @category_name,
                parent_id = @parent_id,
                category_image = @category_image,
                is_active = @is_active
            WHERE id = @id
        ", con);

                cmd.Parameters.AddWithValue(
                    "@category_name",
                    category.CategoryName ?? "");

                cmd.Parameters.AddWithValue(
                    "@parent_id",
                    category.ParentId.HasValue
                        ? category.ParentId.Value
                        : DBNull.Value);

                cmd.Parameters.AddWithValue(
                    "@category_image",
                    imageUrl);

                cmd.Parameters.AddWithValue(
                    "@is_active",
                    category.IsActive);

                cmd.Parameters.AddWithValue("@id", id);

                await cmd.ExecuteNonQueryAsync();

                return new OkObjectResult(new
                {
                    status = true,
                    message = "Category updated successfully",
                    id = id,
                    categoryName = category.CategoryName,
                    parentId = category.ParentId,
                    categoryImage = imageUrl,
                    isActive = category.IsActive
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




        public async Task<IActionResult> DeleteCategory(int id)
        {
            try
            {
                using var con = new NpgsqlConnection(DbConnection);
                await con.OpenAsync();
                // Check category exists
                using (var checkCmd = new NpgsqlCommand(
                    "SELECT COUNT(*) FROM categories WHERE id=@id",
                    con))
                {
                    checkCmd.Parameters.AddWithValue("@id", id);
                    var count = Convert.ToInt32(
                        await checkCmd.ExecuteScalarAsync());
                    if (count == 0)
                    {
                        return new BadRequestObjectResult(new
                        {
                            status = false,
                            message = "Category not found"
                        });
                    }
                }
                // Check if category has children
                using (var childCmd = new NpgsqlCommand(
                    "SELECT COUNT(*) FROM categories WHERE parent_id=@id",
                    con))
                {
                    childCmd.Parameters.AddWithValue("@id", id);
                    var childCount = Convert.ToInt32(
                        await childCmd.ExecuteScalarAsync());
                    if (childCount > 0)
                    {
                        return new BadRequestObjectResult(new
                        {
                            status = false,
                            message = "Cannot delete category with subcategories"
                        });
                    }
                }
                // Delete category
                using var cmd = new NpgsqlCommand(
                    "DELETE FROM categories WHERE id=@id", con);
                cmd.Parameters.AddWithValue("@id", id);
                await cmd.ExecuteNonQueryAsync();
                return new OkObjectResult(new
                {
                    status = true,
                    message = "Category deleted successfully"
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
