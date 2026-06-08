using Microsoft.AspNetCore.Http;
namespace Ecommerce_Backend.Models

{
    public class UserRegisterRequest
    {
        public int id { get; set; }
        public string? first_name { get; set; }
        public string? last_name { get; set; }
        public string? email { get; set; }
        public string? phone_number { get; set; }
        public string? password { get; set; }
        public string? role { get; set; }
    }

    public class UserVerifyOtpRequest
    {
        public string? email { get; set; }
        public string? otp { get; set; }
    }

    public class UserLoginRequest
    {
        public string? email { get; set; }
        public string? password { get; set; }
    }

    public class UserLoginResponse
    {
        public int id { get; set; }
        public string? first_name { get; set; }
        public string? last_name { get; set; }
        public string? email { get; set; }
        public string? phone_number { get; set; }
        public string? role { get; set; }
        public string? token { get; set; }
    }



    public class SizeModel
    {
        public int Id { get; set; }
        public string? SizeName { get; set; }
        public DateTime CreatedAt { get; set; }
        public bool IsActive { get; set; } = true;
    }

    public class ColorResponse
    {
        public int Id { get; set; }

        public string ColorName { get; set; }

        public string? ColorCode { get; set; }

        public bool Status { get; set; }

        public DateTime CreatedAt { get; set; }

    }

    public class ColorApiResponse
    {
        public HexData hex { get; set; }
    }

    public class HexData
    {
        public string value { get; set; }
    }


    public class BrandModel
    {
        public int Id { get; set; }
        public string? BrandName { get; set; }
        public string? BrandImg { get; set; }
        public bool IsActive { get; set; }

        public IFormFile? BrandFile { get; set; }
    }

    public class BlogModel
    {
        public int id { get; set; }
        public string BlogName { get; set; }
        public string? BlogImg { get; set; }
        public IFormFile? BlogFile { get; set; }
        public bool Status { get; set; }
        public string Description {  get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }

    public class CloudinarySettings
    {
        public string CloudName { get; set; } = string.Empty;
        public string ApiKey { get; set; } = string.Empty;
        public string ApiSecret { get; set; } = string.Empty;
    }

    public class CategoryModel
    {
        public int Id { get; set; }

        public string CategoryName { get; set; } = "";

        public int? ParentId { get; set; }

        // Database me save hone wali image URL
        public string? CategoryImage { get; set; }

        // Upload ke liye file
        public IFormFile? CategoryFile { get; set; }

        public bool IsActive { get; set; }
    }
    public class CategoryTreeModel
    {
        public int Id { get; set; }

        public string CategoryName { get; set; } = "";

        public int? ParentId { get; set; }

        public string? CategoryImage { get; set; }

        public bool IsActive { get; set; }

        public List<CategoryTreeModel> Children { get; set; } = new();
    }

    public class ContactModel
    {
        public int Id { get; set; }

        public string FirstName { get; set; } = "";

        public string LastName { get; set; } = "";

        public string Email { get; set; } = "";

        public string? PhoneNumber { get; set; }

        public string Message { get; set; } = "";

        public DateTime CreatedAt { get; set; }
    }

    public class ProductModel
    {
        public int Id { get; set; }

        public string ProductName { get; set; }
        public string? Slug { get; set; }
        public string? Type { get; set; }
        public string? ShortDescription { get; set; }
        public string? Description { get; set; }

        public string? SKU { get; set; }

        public int? BrandId { get; set; }
        public int? CategoryId { get; set; }

        public decimal? BasePrice { get; set; }
        public decimal MRP { get; set; }
        public decimal? DiscountPrice { get; set; }
        public decimal? SalePrice { get; set; }
        public decimal GST { get; set; }

        public int Stock { get; set; }

        public string? ProductImageUrl { get; set; }

        // PostgreSQL TEXT[]
        public string[]? GalleryImages { get; set; }
        public IFormFile? ProductImage { get; set; }
        public List<IFormFile>? GalleryFiles { get; set; }

        public string[]? Sizes { get; set; }

        public string? Color { get; set; }

        public string? BrandName { get; set; }
        public string? CategoryName { get; set; }
        public string? ColorName { get; set; }

        public List<string>? SizeNames { get; set; }

        public bool IsActive { get; set; }

        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }

    public class BannerModel
    {
        public int Id { get; set; }
        public string? BannerName { get; set; }
        public string? BannerDescription { get; set; }
        public string? BannerImg { get; set; }
        public string? BannerType { get; set; }
        public string? BannerLink { get; set; }
        public IFormFile? BannerFile { get; set; }
        public bool Status { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }


    public class ProductVariantModel
    {
        public int Id { get; set; }

        public int ProductId { get; set; }
        public string? VariantName { get; set; }
        public string? Slug { get; set; }


        public string? SKU { get; set; }

        public string[]? Sizes { get; set; }

        public string? Color { get; set; }

        public decimal? MRP { get; set; }

        public decimal? DiscountPercent { get; set; }
        public decimal GST { get; set; }

        public decimal? BasePrice { get; set; }

        public decimal? SalePrice { get; set; }

        public int Stock { get; set; }

        public string? VariantImageUrl { get; set; }

        public string[]? GalleryImages { get; set; }

        public IFormFile? VariantImage { get; set; }

        public List<IFormFile>? GalleryFiles { get; set; }

        public bool IsActive { get; set; }

        public DateTime CreatedAt { get; set; }

        public DateTime UpdatedAt { get; set; }
    }
    public class CouponModel
    {
        public int Id { get; set; }
        public string CouponCode { get; set; }
        public string CouponType { get; set; }
        public decimal DiscountAmount { get; set; }
        public decimal MinimumPurchaseAmount { get; set; }
        public decimal? MaximumDiscountAmount { get; set; }
        public int UsageLimit { get; set; } = 1;
        public DateTime StartDate { get; set; }

        public DateTime ExpiryDate { get; set; }

        public bool IsActive { get; set; } = true;

        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }
}
