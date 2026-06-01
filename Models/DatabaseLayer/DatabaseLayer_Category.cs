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
    }
}
