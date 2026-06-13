using Npgsql;
using System.Text.RegularExpressions;

namespace Ecommerce_Backend.Helpers
{
    public static class SlugHelper
    {
        public static string GenerateSlug(string? text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return "item";

            text = text.ToLowerInvariant().Trim();
            text = Regex.Replace(text, @"[^a-z0-9\s-]", "");
            text = Regex.Replace(text, @"\s+", "-");
            text = Regex.Replace(text, @"-+", "-").Trim('-');

            return string.IsNullOrWhiteSpace(text) ? "item" : text;
        }

        public static async Task<string> GenerateUniqueSlugAsync(
            NpgsqlConnection con,
            string tableName,
            string slugColumn,
            string sourceText,
            int? excludeId = null,
            string idColumn = "id")
        {
            var baseSlug = GenerateSlug(sourceText);
            var slug = baseSlug;
            var counter = 1;

            while (true)
            {
                var sql = excludeId.HasValue
                    ? $"SELECT COUNT(*) FROM {tableName} WHERE {slugColumn} = @slug AND {idColumn} <> @excludeId"
                    : $"SELECT COUNT(*) FROM {tableName} WHERE {slugColumn} = @slug";

                using var checkCmd = new NpgsqlCommand(sql, con);
                checkCmd.Parameters.AddWithValue("@slug", slug);

                if (excludeId.HasValue)
                    checkCmd.Parameters.AddWithValue("@excludeId", excludeId.Value);

                var count = Convert.ToInt32(await checkCmd.ExecuteScalarAsync());
                if (count == 0)
                    return slug;

                slug = $"{baseSlug}-{counter}";
                counter++;
            }
        }
    }
}
