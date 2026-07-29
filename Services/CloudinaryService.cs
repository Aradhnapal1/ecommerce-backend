using CloudinaryDotNet;
using CloudinaryDotNet.Actions;
using Microsoft.Extensions.Options;
using Microsoft.AspNetCore.Http;
using Ecommerce_Backend.Models;

namespace Ecommerce_Backend.Services
{
    public class CloudinaryService
    {
        private readonly Cloudinary _cloudinary;

        public CloudinaryService(IOptions<CloudinarySettings> config)
        {
            var account = new Account(
                config.Value.CloudName,
                config.Value.ApiKey,
                config.Value.ApiSecret
            );

            _cloudinary = new Cloudinary(account);
        }

        // 🔥 ADD IMAGE
        public async Task<(string Url, string PublicId)> UploadImageAsync(IFormFile file, string publicId)
        {
            if (file == null || file.Length == 0)
                return (string.Empty, string.Empty);

            using var stream = file.OpenReadStream();

            var uploadParams = new ImageUploadParams
            {
                File = new FileDescription(file.FileName, stream),
                PublicId = publicId,
                UseFilename = false,
                UniqueFilename = false
            };

            var result = await _cloudinary.UploadAsync(uploadParams);

            return (result.SecureUrl.ToString(), result.PublicId);
        }

        // 🔥 REPLACE IMAGE (FINAL FIX)
        public async Task<(string Url, string PublicId)> ReplaceImageAsync(IFormFile file, string publicId)
        {
            if (file == null || file.Length == 0)
                return (string.Empty, string.Empty);

            // 🔥 STEP 1: DELETE OLD IMAGE
            await _cloudinary.DestroyAsync(new DeletionParams(publicId));

            using var stream = file.OpenReadStream();

            // 🔥 STEP 2: UPLOAD NEW IMAGE
            var uploadParams = new ImageUploadParams
            {
                File = new FileDescription(file.FileName, stream),
                PublicId = publicId,
                UseFilename = false,
                UniqueFilename = false
            };

            var result = await _cloudinary.UploadAsync(uploadParams);

            return (result.SecureUrl.ToString(), result.PublicId);
        }

        // 🔥 DELETE IMAGE
        public async Task<bool> DeleteImageAsync(string publicId)
        {
            if (string.IsNullOrEmpty(publicId))
                return false;

            var result = await _cloudinary.DestroyAsync(new DeletionParams(publicId));

            return result.Result == "ok";
        }

        /// <summary>Upload image from remote URL (Cloudinary fetches the link).</summary>
        public async Task<string?> UploadFromUrlAsync(string imageUrl, string folder)
        {
            if (string.IsNullOrWhiteSpace(imageUrl))
                return null;

            if (!Uri.TryCreate(imageUrl.Trim(), UriKind.Absolute, out var uri) ||
                (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
            {
                throw new InvalidOperationException($"Invalid image URL: {imageUrl}");
            }

            var uploadParams = new ImageUploadParams
            {
                File = new FileDescription(uri.ToString()),
                Folder = folder
            };

            var result = await _cloudinary.UploadAsync(uploadParams);
            if (result.Error != null)
                throw new InvalidOperationException(result.Error.Message);

            return result.SecureUrl?.ToString();
        }

        /// <summary>Download image bytes then upload to Cloudinary (fallback for blocked remote fetch).</summary>
        public async Task<string?> DownloadAndUploadAsync(string imageUrl, string folder, HttpClient httpClient)
        {
            if (string.IsNullOrWhiteSpace(imageUrl))
                return null;

            using var response = await httpClient.GetAsync(imageUrl.Trim());
            response.EnsureSuccessStatusCode();

            await using var stream = await response.Content.ReadAsStreamAsync();
            var fileName = Path.GetFileName(new Uri(imageUrl).AbsolutePath);
            if (string.IsNullOrWhiteSpace(fileName))
                fileName = $"import-{Guid.NewGuid():N}.jpg";

            var uploadParams = new ImageUploadParams
            {
                File = new FileDescription(fileName, stream),
                Folder = folder
            };

            var result = await _cloudinary.UploadAsync(uploadParams);
            if (result.Error != null)
                throw new InvalidOperationException(result.Error.Message);

            return result.SecureUrl?.ToString();
        }
    }
}
