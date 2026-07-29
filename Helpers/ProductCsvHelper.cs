using System.Globalization;
using System.Text;

namespace Ecommerce_Backend.Helpers
{
    public static class ProductCsvHelper
    {
        public static readonly string[] Headers =
        [
            "ProductName",
            "Type",
            "ShortDescription",
            "Description",
            "SKU",
            "Brand",
            "Category",
            "Color",
            "ColorCode",
            "Sizes",
            "MRP",
            "DiscountPercent",
            "GST",
            "Stock",
            "ProductImageUrl",
            "GalleryImageUrls",
            "IsActive"
        ];

        public static byte[] BuildSampleFile()
        {
            var sb = new StringBuilder();
            sb.AppendLine(string.Join(",", Headers));
            sb.AppendLine(string.Join(",",
            [
                Escape("Sample T-Shirt"),
                Escape("Apparel"),
                Escape("Soft cotton tee"),
                Escape("Full product description goes here"),
                Escape("SKU-SAMPLE-001"),
                Escape("Nike"),
                Escape("Men Clothing"),
                Escape("Black"),
                Escape("#000000"),
                Escape("S|M|L"),
                "999",
                "10",
                "18",
                "50",
                Escape("https://via.placeholder.com/600x600.png?text=Main"),
                Escape("https://via.placeholder.com/600x600.png?text=Gallery1|https://via.placeholder.com/600x600.png?text=Gallery2"),
                "true"
            ]));

            // UTF-8 BOM so Excel opens Hindi/special chars correctly
            var preamble = Encoding.UTF8.GetPreamble();
            var body = Encoding.UTF8.GetBytes(sb.ToString());
            var result = new byte[preamble.Length + body.Length];
            Buffer.BlockCopy(preamble, 0, result, 0, preamble.Length);
            Buffer.BlockCopy(body, 0, result, preamble.Length, body.Length);
            return result;
        }

        public static byte[] BuildExportFile(IEnumerable<ProductImportRow> rows)
        {
            var sb = new StringBuilder();
            sb.AppendLine(string.Join(",", Headers));

            foreach (var row in rows)
            {
                sb.AppendLine(string.Join(",",
                [
                    Escape(row.ProductName),
                    Escape(row.Type),
                    Escape(row.ShortDescription),
                    Escape(row.Description),
                    Escape(row.SKU),
                    Escape(row.Brand),
                    Escape(row.Category),
                    Escape(row.Color),
                    Escape(row.ColorCode),
                    Escape(row.Sizes),
                    FormatDecimal(row.MRP),
                    FormatDecimal(row.DiscountPercent),
                    FormatDecimal(row.GST),
                    row.Stock.ToString(CultureInfo.InvariantCulture),
                    Escape(row.ProductImageUrl),
                    Escape(row.GalleryImageUrls),
                    row.IsActive ? "true" : "false"
                ]));
            }

            var preamble = Encoding.UTF8.GetPreamble();
            var body = Encoding.UTF8.GetBytes(sb.ToString());
            var result = new byte[preamble.Length + body.Length];
            Buffer.BlockCopy(preamble, 0, result, 0, preamble.Length);
            Buffer.BlockCopy(body, 0, result, preamble.Length, body.Length);
            return result;
        }

        public static List<ProductImportRow> Parse(Stream csvStream)
        {
            using var reader = new StreamReader(csvStream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
            var content = reader.ReadToEnd();
            var lines = SplitLines(content);
            if (lines.Count == 0)
                return [];

            var headerCells = ParseCsvLine(lines[0]);
            var map = BuildHeaderMap(headerCells);

            var rows = new List<ProductImportRow>();
            for (var i = 1; i < lines.Count; i++)
            {
                if (string.IsNullOrWhiteSpace(lines[i]))
                    continue;

                var cells = ParseCsvLine(lines[i]);
                string Get(string name) =>
                    map.TryGetValue(name, out var idx) && idx < cells.Count
                        ? cells[idx].Trim()
                        : string.Empty;

                var row = new ProductImportRow
                {
                    RowNumber = i + 1,
                    ProductName = Get("ProductName"),
                    Type = NullIfEmpty(Get("Type")),
                    ShortDescription = NullIfEmpty(Get("ShortDescription")),
                    Description = NullIfEmpty(Get("Description")),
                    SKU = NullIfEmpty(Get("SKU")),
                    Brand = NullIfEmpty(Get("Brand")),
                    Category = NullIfEmpty(Get("Category")),
                    Color = NullIfEmpty(Get("Color")),
                    ColorCode = NullIfEmpty(Get("ColorCode")),
                    Sizes = NullIfEmpty(Get("Sizes")),
                    ProductImageUrl = NullIfEmpty(Get("ProductImageUrl")),
                    GalleryImageUrls = NullIfEmpty(Get("GalleryImageUrls")),
                    IsActive = ParseBool(Get("IsActive"), defaultValue: true)
                };

                if (decimal.TryParse(Get("MRP"), NumberStyles.Any, CultureInfo.InvariantCulture, out var mrp))
                    row.MRP = mrp;
                if (decimal.TryParse(Get("DiscountPercent"), NumberStyles.Any, CultureInfo.InvariantCulture, out var disc))
                    row.DiscountPercent = disc;
                if (decimal.TryParse(Get("GST"), NumberStyles.Any, CultureInfo.InvariantCulture, out var gst))
                    row.GST = gst;
                if (int.TryParse(Get("Stock"), NumberStyles.Integer, CultureInfo.InvariantCulture, out var stock))
                    row.Stock = stock;

                rows.Add(row);
            }

            return rows;
        }

        public static IEnumerable<string> SplitList(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
                yield break;

            foreach (var part in value.Split(['|', ';', ','], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                if (!string.IsNullOrWhiteSpace(part))
                    yield return part;
            }
        }

        private static Dictionary<string, int> BuildHeaderMap(List<string> headerCells)
        {
            var map = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            for (var i = 0; i < headerCells.Count; i++)
            {
                var key = headerCells[i].Trim().Trim('\uFEFF');
                if (!string.IsNullOrWhiteSpace(key) && !map.ContainsKey(key))
                    map[key] = i;
            }

            return map;
        }

        private static List<string> SplitLines(string content)
        {
            var lines = new List<string>();
            var sb = new StringBuilder();
            var inQuotes = false;

            for (var i = 0; i < content.Length; i++)
            {
                var c = content[i];
                if (c == '"')
                {
                    inQuotes = !inQuotes;
                    sb.Append(c);
                    continue;
                }

                if (!inQuotes && (c == '\n' || c == '\r'))
                {
                    if (c == '\r' && i + 1 < content.Length && content[i + 1] == '\n')
                        i++;

                    lines.Add(sb.ToString());
                    sb.Clear();
                    continue;
                }

                sb.Append(c);
            }

            if (sb.Length > 0)
                lines.Add(sb.ToString());

            return lines;
        }

        private static List<string> ParseCsvLine(string line)
        {
            var cells = new List<string>();
            var sb = new StringBuilder();
            var inQuotes = false;

            for (var i = 0; i < line.Length; i++)
            {
                var c = line[i];
                if (c == '"')
                {
                    if (inQuotes && i + 1 < line.Length && line[i + 1] == '"')
                    {
                        sb.Append('"');
                        i++;
                    }
                    else
                    {
                        inQuotes = !inQuotes;
                    }

                    continue;
                }

                if (c == ',' && !inQuotes)
                {
                    cells.Add(sb.ToString());
                    sb.Clear();
                    continue;
                }

                sb.Append(c);
            }

            cells.Add(sb.ToString());
            return cells;
        }

        private static string Escape(string? value)
        {
            value ??= string.Empty;
            if (value.Contains('"') || value.Contains(',') || value.Contains('\n') || value.Contains('\r'))
                return $"\"{value.Replace("\"", "\"\"")}\"";
            return value;
        }

        private static string FormatDecimal(decimal value) =>
            value.ToString(CultureInfo.InvariantCulture);

        private static string? NullIfEmpty(string value) =>
            string.IsNullOrWhiteSpace(value) ? null : value.Trim();

        private static bool ParseBool(string value, bool defaultValue)
        {
            if (string.IsNullOrWhiteSpace(value))
                return defaultValue;

            return value.Equals("true", StringComparison.OrdinalIgnoreCase)
                   || value.Equals("1", StringComparison.OrdinalIgnoreCase)
                   || value.Equals("yes", StringComparison.OrdinalIgnoreCase)
                   || value.Equals("y", StringComparison.OrdinalIgnoreCase);
        }
    }

    public class ProductImportRow
    {
        public int RowNumber { get; set; }
        public string ProductName { get; set; } = string.Empty;
        public string? Type { get; set; }
        public string? ShortDescription { get; set; }
        public string? Description { get; set; }
        public string? SKU { get; set; }
        public string? Brand { get; set; }
        public string? Category { get; set; }
        public string? Color { get; set; }
        public string? ColorCode { get; set; }
        public string? Sizes { get; set; }
        public decimal MRP { get; set; }
        public decimal DiscountPercent { get; set; }
        public decimal GST { get; set; }
        public int Stock { get; set; }
        public string? ProductImageUrl { get; set; }
        public string? GalleryImageUrls { get; set; }
        public bool IsActive { get; set; } = true;
    }
}
