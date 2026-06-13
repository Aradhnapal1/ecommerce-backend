using CloudinaryDotNet;
using CloudinaryDotNet.Actions;
using Ecommerce_Backend.Helpers;
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
        Task<IActionResult> GetCategoriesByTypeHome();
        Task<IActionResult> GetCategoriesByHeroSection();
        Task<IActionResult> GetCategoriesByBrowseCategory();
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
                var slug = await SlugHelper.GenerateUniqueSlugAsync(
                    con, "categories", "slug", category.CategoryName.Trim());

                using var cmd = new NpgsqlCommand(@"
            INSERT INTO categories
            (
                category_name,
                slug,
                parent_id,
                type,
                category_image,
                browsecategory,
                herosection,
                is_active
            )
            VALUES
            (
                @category_name,
                @slug,
                @parent_id,
                @type,
                @category_image,
                @browsecategory,
                @herosection,
                @is_active
            )
            RETURNING id;
        ", con);

                cmd.Parameters.AddWithValue(
                    "@category_name",
                    category.CategoryName.Trim());

                cmd.Parameters.AddWithValue("@slug", slug);

                cmd.Parameters.AddWithValue(
                    "@parent_id",
                    category.ParentId.HasValue
                        ? category.ParentId.Value
                        : DBNull.Value);

                cmd.Parameters.AddWithValue(
                    "@type",
                    category.Type);

                cmd.Parameters.AddWithValue(
                    "@category_image",
                    imageUrl);
                cmd.Parameters.AddWithValue(
                    "@browsecategory",
                    category.BrowseCategory);


                cmd.Parameters.AddWithValue(
                    "@herosection", category.HeroSection
                    );

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
                    slug = slug,
                    parentId = category.ParentId,
                    type = category.Type,
                    categoryImage = imageUrl,
                    browseCategory = category.BrowseCategory,
                    heroSection = category.HeroSection,
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
                slug,
                parent_id,
                type,
                category_image,
                browsecategory,
                heroSection,
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
                        Id = Convert.ToInt32(reader["id"]),
                        CategoryName = reader["category_name"]?.ToString() ?? "",
                        Slug = reader["slug"]?.ToString(),
                        ParentId = reader["parent_id"] == DBNull.Value
                            ? null
                            : Convert.ToInt32(reader["parent_id"]),

                        Type = reader["type"]?.ToString() ?? "",

                        CategoryImage = reader["category_image"] == DBNull.Value
                            ? null
                            : reader["category_image"]?.ToString(),

                        BrowseCategory = Convert.ToBoolean(reader["browsecategory"]),
                        HeroSection = Convert.ToBoolean(reader["heroSection"]),

                        IsActive = Convert.ToBoolean(reader["is_active"]),

                        Children = new List<CategoryTreeModel>()
                    });
                }

                // Fast lookup dictionary
                var lookup = categories.ToDictionary(x => x.Id);

                // Root categories
                var roots = new List<CategoryTreeModel>();

                foreach (var category in categories)
                {
                    if (category.ParentId == null)
                    {
                        roots.Add(category);
                    }
                    else if (lookup.TryGetValue(category.ParentId.Value, out var parent))
                    {
                        parent.Children.Add(category);
                    }
                }

                return new OkObjectResult(new
                {
                    status = true,
                    message = "Categories fetched successfully",
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

        public async Task<IActionResult> GetCategoriesByTypeHome()
        {
            try
            {
                using var con = new NpgsqlConnection(DbConnection);
                await con.OpenAsync();

                using var cmd = new NpgsqlCommand(@"
            SELECT
                id,
                category_name,
                slug,
                parent_id,
                type,
                category_image,
                browsecategory,
                herosection,
                is_active
            FROM categories
            WHERE type = 'Home'
            ORDER BY id
        ", con);

                using var reader = await cmd.ExecuteReaderAsync();

                var categories = new List<CategoryTreeModel>();

                while (await reader.ReadAsync())
                {
                    categories.Add(new CategoryTreeModel
                    {
                        Id = Convert.ToInt32(reader["id"]),
                        CategoryName = reader["category_name"] == DBNull.Value ? "" : reader["category_name"].ToString() ?? "",
                        Slug = reader["slug"] == DBNull.Value ? null : reader["slug"].ToString(),
                        ParentId = reader["parent_id"] == DBNull.Value
                            ? null
                            : Convert.ToInt32(reader["parent_id"]),

                        Type = reader["type"] == DBNull.Value ? null : reader["type"].ToString(),

                        CategoryImage = reader["category_image"] == DBNull.Value
                            ? null
                            : reader["category_image"].ToString(),

                        BrowseCategory = reader["browsecategory"] != DBNull.Value ? Convert.ToBoolean(reader["browsecategory"]) : (bool?)null,
                        HeroSection = reader["herosection"] != DBNull.Value && Convert.ToBoolean(reader["herosection"]),

                        IsActive = reader["is_active"] != DBNull.Value && Convert.ToBoolean(reader["is_active"]),

                        Children = new List<CategoryTreeModel>()
                    });
                }

                // Fast lookup dictionary
                var lookup = categories.ToDictionary(x => x.Id);

                // Root categories
                var roots = new List<CategoryTreeModel>();

                foreach (var category in categories)
                {
                    if (category.ParentId == null)
                    {
                        roots.Add(category);
                    }
                    else if (lookup.TryGetValue(category.ParentId.Value, out var parent))
                    {
                        parent.Children.Add(category);
                    }
                    else
                    {
                        // Includes subcategories even if the strict parent isn't in 'Home' type 
                        roots.Add(category);
                    }
                }

                return new OkObjectResult(new
                {
                    status = true,
                    message = "Categories fetched successfully",
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

        public async Task<IActionResult> GetCategoriesByHeroSection()
        {
            try
            {
                using var con = new NpgsqlConnection(DbConnection);
                await con.OpenAsync();

                using var cmd = new NpgsqlCommand(@"
            SELECT
                id,
                category_name,
                slug,
                parent_id,
                type,
                category_image,
                browsecategory,
                herosection,
                is_active
            FROM categories
            WHERE herosection = true
            ORDER BY id
        ", con);

                using var reader = await cmd.ExecuteReaderAsync();

                var categories = new List<CategoryTreeModel>();

                while (await reader.ReadAsync())
                {
                    categories.Add(new CategoryTreeModel
                    {
                        Id = Convert.ToInt32(reader["id"]),
                        CategoryName = reader["category_name"] == DBNull.Value ? "" : reader["category_name"].ToString() ?? "",
                        Slug = reader["slug"] == DBNull.Value ? null : reader["slug"].ToString(),
                        ParentId = reader["parent_id"] == DBNull.Value
                            ? null
                            : Convert.ToInt32(reader["parent_id"]),

                        Type = reader["type"] == DBNull.Value ? null : reader["type"].ToString(),

                        CategoryImage = reader["category_image"] == DBNull.Value
                            ? null
                            : reader["category_image"].ToString(),

                        BrowseCategory = reader["browsecategory"] != DBNull.Value ? Convert.ToBoolean(reader["browsecategory"]) : (bool?)null,
                        HeroSection = reader["herosection"] != DBNull.Value && Convert.ToBoolean(reader["herosection"]),

                        IsActive = reader["is_active"] != DBNull.Value && Convert.ToBoolean(reader["is_active"]),

                        Children = new List<CategoryTreeModel>()
                    });
                }

                // Fast lookup dictionary
                var lookup = categories.ToDictionary(x => x.Id);

                // Root categories
                var roots = new List<CategoryTreeModel>();

                foreach (var category in categories)
                {
                    if (category.ParentId == null)
                    {
                        roots.Add(category);
                    }
                    else if (lookup.TryGetValue(category.ParentId.Value, out var parent))
                    {
                        parent.Children.Add(category);
                    }
                    else
                    {
                        roots.Add(category);
                    }
                }

                return new OkObjectResult(new
                {
                    status = true,
                    message = "Categories fetched successfully",
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

        public async Task<IActionResult> GetCategoriesByBrowseCategory()
        {
            try
            {
                using var con = new NpgsqlConnection(DbConnection);
                await con.OpenAsync();

                using var cmd = new NpgsqlCommand(@"
            SELECT
                id,
                category_name,
                slug,
                parent_id,
                type,
                category_image,
                browsecategory,
                herosection,
                is_active
            FROM categories
            WHERE browsecategory = true
            ORDER BY id
        ", con);

                using var reader = await cmd.ExecuteReaderAsync();

                var categories = new List<CategoryTreeModel>();

                while (await reader.ReadAsync())
                {
                    categories.Add(new CategoryTreeModel
                    {
                        Id = Convert.ToInt32(reader["id"]),
                        CategoryName = reader["category_name"] == DBNull.Value ? "" : reader["category_name"].ToString() ?? "",
                        Slug = reader["slug"] == DBNull.Value ? null : reader["slug"].ToString(),
                        ParentId = reader["parent_id"] == DBNull.Value
                            ? null
                            : Convert.ToInt32(reader["parent_id"]),

                        Type = reader["type"] == DBNull.Value ? null : reader["type"].ToString(),

                        CategoryImage = reader["category_image"] == DBNull.Value
                            ? null
                            : reader["category_image"].ToString(),

                        BrowseCategory = reader["browsecategory"] != DBNull.Value ? Convert.ToBoolean(reader["browsecategory"]) : (bool?)null,
                        HeroSection = reader["herosection"] != DBNull.Value && Convert.ToBoolean(reader["herosection"]),

                        IsActive = reader["is_active"] != DBNull.Value && Convert.ToBoolean(reader["is_active"]),

                        Children = new List<CategoryTreeModel>()
                    });
                }

                // Fast lookup dictionary
                var lookup = categories.ToDictionary(x => x.Id);

                // Root categories
                var roots = new List<CategoryTreeModel>();

                foreach (var category in categories)
                {
                    if (category.ParentId == null)
                    {
                        roots.Add(category);
                    }
                    else if (lookup.TryGetValue(category.ParentId.Value, out var parent))
                    {
                        parent.Children.Add(category);
                    }
                    else
                    {
                        roots.Add(category);
                    }
                }

                return new OkObjectResult(new
                {
                    status = true,
                    message = "Categories fetched successfully",
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
                var slug = await SlugHelper.GenerateUniqueSlugAsync(
                    con, "categories", "slug", category.CategoryName ?? "", id);

                using var cmd = new NpgsqlCommand(@"
            UPDATE categories
            SET
                category_name = @category_name,
                slug = @slug,
                parent_id = @parent_id,
                    type = @type,
                category_image = @category_image,
                 browsecategory = @browsecategory,
                heroSection = @heroSection,
                is_active = @is_active
            WHERE id = @id
        ", con);

                cmd.Parameters.AddWithValue(
                    "@category_name",
                    category.CategoryName ?? "");

                cmd.Parameters.AddWithValue("@slug", slug);

                cmd.Parameters.AddWithValue(
                    "@parent_id",
                    category.ParentId.HasValue
                        ? category.ParentId.Value
                        : DBNull.Value);
                cmd.Parameters.AddWithValue(
                    "@type",
                    category.Type ?? "");

                cmd.Parameters.AddWithValue(
                    "@category_image",
                    imageUrl);

                cmd.Parameters.AddWithValue(
                    "@browsecategory",
                    category.BrowseCategory);

                cmd.Parameters.AddWithValue(
                    "@herosection",
                    category.HeroSection);

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
                    slug = slug,
                    parentId = category.ParentId,
                    categoryImage = imageUrl,
                    heroSection = category.HeroSection,
                    browseCategory = category.BrowseCategory,
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
