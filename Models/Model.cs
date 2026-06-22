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
        public string? ProfileImageUrl { get; set; }
        public DateTime? DateOfBirth { get; set; }
        public string? Gender { get; set; }
    }



    public class SizeModel
    {
        public int Id { get; set; }
        public string? SizeName { get; set; }
        public string? Slug { get; set; }
        public DateTime CreatedAt { get; set; }
        public bool IsActive { get; set; } = true;
    }

    public class ColorResponse
    {
        public int Id { get; set; }

        public string ColorName { get; set; }

        public string? Slug { get; set; }

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
        public string? Slug { get; set; }
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
        public string Description { get; set; }
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

        public string? Slug { get; set; }

        public int? ParentId { get; set; }
        public string? Type { get; set; } = "";

        // Database me save hone wali image URL
        public string? CategoryImage { get; set; }

        // Upload ke liye file
        public IFormFile? CategoryFile { get; set; }
        public bool? BrowseCategory { get; set; }
        public bool HeroSection { get; set; }

        public bool IsActive { get; set; }
    }
    public class CategoryTreeModel
    {
        public int Id { get; set; }

        public string CategoryName { get; set; } = "";

        public string? Slug { get; set; }

        public int? ParentId { get; set; }
        public string? Type { get; set; }

        public string? CategoryImage { get; set; }
        public bool? BrowseCategory { get; set; }
        public bool HeroSection { get; set; }

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

        public int[]? Sizes { get; set; }

        public string? Color { get; set; }

        public string? BrandName { get; set; }
        public string? BrandSlug { get; set; }
        public string? CategoryName { get; set; }
        public string? CategorySlug { get; set; }
        public string? ColorName { get; set; }
        public string? ColorSlug { get; set; }

        public List<string>? SizeNames { get; set; }
        public List<string>? SizeSlugs { get; set; }

        public decimal AverageRating { get; set; }
        public int TotalReviews { get; set; }

        public bool IsActive { get; set; }

        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }

    public class ProductFilterRequest
    {
        public int? CategoryId { get; set; }
        public int[]? CategoryIds { get; set; }
        public string? CategorySlug { get; set; }
        public string[]? CategorySlugs { get; set; }

        public int? BrandId { get; set; }
        public int[]? BrandIds { get; set; }
        public string? BrandSlug { get; set; }
        public string[]? BrandSlugs { get; set; }

        public int? ColorId { get; set; }
        public int[]? ColorIds { get; set; }
        public string? ColorSlug { get; set; }
        public string[]? ColorSlugs { get; set; }

        public int? SizeId { get; set; }
        public int[]? SizeIds { get; set; }
        public string? SizeSlug { get; set; }
        public string[]? SizeSlugs { get; set; }
        public decimal? MinPrice { get; set; }
        public decimal? MaxPrice { get; set; }
        public decimal? MinDiscount { get; set; }
        public decimal[]? DiscountPercents { get; set; }
        public bool? HasDiscount { get; set; }
        public string? Search { get; set; }
        public string? Q { get; set; }
        public string? SortBy { get; set; }
        public bool? InStock { get; set; }
        public bool UseGlobalSearch { get; set; }
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 20;

        public string? ResolvedSearch =>
            !string.IsNullOrWhiteSpace(Search) ? Search.Trim()
            : !string.IsNullOrWhiteSpace(Q) ? Q.Trim()
            : null;

        public decimal[] ResolvedDiscountPercents =>
            DiscountPercents is { Length: > 0 }
                ? DiscountPercents
                : MinDiscount.HasValue ? new[] { MinDiscount.Value } : Array.Empty<decimal>();

        public int[] ResolvedCategoryIds =>
            CategoryIds is { Length: > 0 }
                ? CategoryIds
                : CategoryId.HasValue ? new[] { CategoryId.Value } : Array.Empty<int>();

        public int[] ResolvedBrandIds =>
            BrandIds is { Length: > 0 }
                ? BrandIds
                : BrandId.HasValue ? new[] { BrandId.Value } : Array.Empty<int>();

        public int[] ResolvedColorIds =>
            ColorIds is { Length: > 0 }
                ? ColorIds
                : ColorId.HasValue ? new[] { ColorId.Value } : Array.Empty<int>();

        public int[] ResolvedSizeIds =>
            SizeIds is { Length: > 0 }
                ? SizeIds
                : SizeId.HasValue ? new[] { SizeId.Value } : Array.Empty<int>();

        public string[] ResolvedCategorySlugs =>
            NormalizeSlugs(CategorySlugs, CategorySlug);

        public string[] ResolvedBrandSlugs =>
            NormalizeSlugs(BrandSlugs, BrandSlug);

        public string[] ResolvedColorSlugs =>
            NormalizeSlugs(ColorSlugs, ColorSlug);

        public string[] ResolvedSizeSlugs =>
            NormalizeSlugs(SizeSlugs, SizeSlug);

        private static string[] NormalizeSlugs(string[]? slugs, string? singleSlug)
        {
            if (slugs is { Length: > 0 })
                return slugs
                    .SelectMany(s => s.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries))
                    .Where(s => !string.IsNullOrWhiteSpace(s))
                    .Select(s => s.Trim().ToLowerInvariant())
                    .Distinct()
                    .ToArray();

            return !string.IsNullOrWhiteSpace(singleSlug)
                ? singleSlug.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(s => s.Trim().ToLowerInvariant())
                    .ToArray()
                : Array.Empty<string>();
        }

        public bool HasFilters() =>
            ResolvedCategoryIds.Length > 0 ||
            ResolvedBrandIds.Length > 0 ||
            ResolvedColorIds.Length > 0 ||
            ResolvedSizeIds.Length > 0 ||
            ResolvedCategorySlugs.Length > 0 ||
            ResolvedBrandSlugs.Length > 0 ||
            ResolvedColorSlugs.Length > 0 ||
            ResolvedSizeSlugs.Length > 0 ||
            (MinPrice.HasValue && MinPrice.Value > 0) ||
            (MaxPrice.HasValue && MaxPrice.Value > 0) ||
            ResolvedDiscountPercents.Length > 0 ||
            HasDiscount.HasValue ||
            InStock.HasValue ||
            !string.IsNullOrWhiteSpace(ResolvedSearch) ||
            !string.IsNullOrWhiteSpace(SortBy) ||
            Page > 1 ||
            PageSize != 20;
    }

    public class ProductSearchRequest : ProductFilterRequest
    {
        public new string Q { get; set; } = string.Empty;
    }

    public class ProductReviewSummary
    {
        public decimal AverageRating { get; set; }
        public int TotalReviews { get; set; }
    }

    public class ProductDetailModel
    {
        public ProductModel Product { get; set; } = new();
        public List<ProductVariantModel> Variants { get; set; } = new();
        public ProductReviewSummary Reviews { get; set; } = new();
        public List<ProductModel> RelatedProducts { get; set; } = new();
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

        // ONLY SIZE IDS
        public int[]? Sizes { get; set; }

        // COLOR IS STRING (IMPORTANT FIX)
        public string? Color { get; set; }

        // OPTIONAL: SIZE NAMES (JOIN RESULT)
        public List<string>? SizeNames { get; set; }

        public string? ColorName { get; set; }

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
        public int UsageLimit { get; set; } = 1;
        public DateTime StartDate { get; set; }

        public DateTime ExpiryDate { get; set; }

        public bool IsActive { get; set; } = true;

        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }







    public class WishlistModel

    {

        public int Id { get; set; }
        public int? UserId { get; set; }
        public int ProductId { get; set; }

        public int? VariantId { get; set; }

        public int? ColorId { get; set; }

        public int? SizeId { get; set; }

        public string? IpAddress { get; set; }

        public DateTime CreatedAt { get; set; }

        public object? Item { get; set; }
    }

    public class CompareModel
    {
        public int Id { get; set; }
        public int? UserId { get; set; }
        public int ProductId { get; set; }
        public int? VariantId { get; set; }
        public int? ColorId { get; set; }
        public int? SizeId { get; set; }
        public string? IpAddress { get; set; }
        public DateTime CreatedAt { get; set; }
        public object? Item { get; set; }
    }







    public class AddCartModel
    {
        public int Id { get; set; }

        public int? UserId { get; set; }

        public int ProductId { get; set; }

        public int? VariantId { get; set; }

        public int? ColorId { get; set; }

        public int? SizeId { get; set; }

        // Common frontend aliases (form field names)
        public int? Color { get; set; }

        public int? Size { get; set; }

        public string? ColorSlug { get; set; }

        public string? SizeSlug { get; set; }

        public int Quantity { get; set; } = 1;

        public string? IpAddress { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public void NormalizeFormFields()
        {
            ColorId ??= Color;
            SizeId ??= Size;
        }
    }



    public class UpdateCartQuantityModel
    {
        public int CartId { get; set; }
        public int Quantity { get; set; }
        public int? UserId { get; set; }
        public string? IpAddress { get; set; }
    }
//edfrg
    public class ApplyCouponModel
    {
        public string CouponCode { get; set; }
    }

    public class ForgotPasswordRequest
    {
        public string Email { get; set; }
    }

    public class ResetPasswordRequest
    {
        public string Email { get; set; }
        public string Token { get; set; }
        public string NewPassword { get; set; }
    }

    public class ChangePasswordRequest
    {
        public string OldPassword { get; set; } = string.Empty;
        public string NewPassword { get; set; } = string.Empty;
    }

    public class UpdateProfileRequest
    {
        public string? FirstName { get; set; }
        public string? LastName { get; set; }
        public string? PhoneNumber { get; set; }
        public IFormFile? ProfileImage { get; set; }
        public DateTime? DateOfBirth { get; set; }
        public string? Gender { get; set; }
    }

    public class UserAddressModel
    {
        public int Id { get; set; }

        public string FullName { get; set; }

        public string Mobile { get; set; }

        public string? AlternateMobile { get; set; }

        public string AddressLine1 { get; set; }

        public string? AddressLine2 { get; set; }

        public string? Landmark { get; set; }

        public string City { get; set; }

        public string State { get; set; }

        public string Country { get; set; } = "India";

        public string Pincode { get; set; }

        public string AddressType { get; set; } = "HOME";

        public bool IsDefault { get; set; }
    }

    public class CreateOrderModel
    {
        public int AddressId { get; set; }

        public string PaymentMethod { get; set; }

        public string? CouponCode { get; set; }
    }

    public class BuyNowModel
    {
        public int AddressId { get; set; }
        public int ProductId { get; set; }
        public int? VariantId { get; set; }
        public int? ColorId { get; set; }
        public int? SizeId { get; set; }
        public int Quantity { get; set; } = 1;
        public string PaymentMethod { get; set; } = "ONLINE";
        public string? CouponCode { get; set; }
    }

    public class VerifyPaymentModel
    {
        public int OrderId { get; set; }
        public string RazorpayOrderId { get; set; } = string.Empty;
        public string RazorpayPaymentId { get; set; } = string.Empty;
        public string RazorpaySignature { get; set; } = string.Empty;
    }

    public class OrderInvoiceEmailModel
    {
        public string CustomerName { get; set; } = string.Empty;
        public string OrderNumber { get; set; } = string.Empty;
        public decimal Subtotal { get; set; }
        public decimal DiscountAmount { get; set; }
        public decimal FinalAmount { get; set; }
        public string PaymentMethod { get; set; } = string.Empty;
        public string PaymentStatus { get; set; } = string.Empty;
        public List<OrderItemModel> Items { get; set; } = new();
    }


    public class CartItemModel
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public int ProductId { get; set; }
        public int VariantId { get; set; }
        public int Quantity { get; set; }
        public decimal Price { get; set; }
        public decimal MRP { get; set; }
        public string? ProductName { get; set; }
        public string? ProductImageUrl { get; set; }
        public string? SKU { get; set; }
        public string? ColorName { get; set; }
        public string? SizeName { get; set; }
    }

    public class OrderDetailsModel
    {
        public int Id { get; set; }
        public string OrderNumber { get; set; }
        public int UserId { get; set; }
        public int AddressId { get; set; }
        public string FullName { get; set; }
        public string Mobile { get; set; }
        public string PaymentMethod { get; set; }
        public string PaymentStatus { get; set; }
        public string OrderStatus { get; set; }
        public decimal Subtotal { get; set; }
        public decimal DiscountAmount { get; set; }
        public decimal FinalAmount { get; set; }
        public string? CouponCode { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class OrderItemModel
    {
        public int Id { get; set; }
        public int OrderId { get; set; }
        public int ProductId { get; set; }
        public string? ProductName { get; set; }
        public int VariantId { get; set; }
        public int Quantity { get; set; }
        public decimal Price { get; set; }
        public decimal Total { get; set; }
        public decimal MRP { get; set; }
        public string? ProductImageUrl { get; set; }
        public string? SKU { get; set; }
        public string? ColorName { get; set; }
        public string? SizeName { get; set; }
    }

    public class ProductReviewModel
    {
        public int Id { get; set; }
        public int ProductId { get; set; }
        public int UserId { get; set; }
        public string? FirstName { get; set; }
        public string? LastName { get; set; }
        public string? Email { get; set; }
        public string? ProfileImageUrl { get; set; }
        public int Rating { get; set; }
        public string? Comment { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class AddReviewRequest
    {
        public int ProductId { get; set; }
        public int Rating { get; set; }
        public string? Comment { get; set; }
    }
}