using System.Text.RegularExpressions;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace EpsonScanApi.Services;

/// <summary>
/// Selects the most readable label orientation after the page has been cropped
/// and deskewed.  It handles 180-degree loading, sideways labels and a
/// mirrored scan without relying on the scanner orientation flag.
/// </summary>
internal static class LabelOrientationResolver
{
    private sealed record Candidate(
        int Rotation,
        bool Mirrored,
        string Path,
        Dictionary<string, object> Ocr,
        double LayoutScore,
        double OcrScore)
    {
        public double Score => LayoutScore + OcrScore;
    }

    internal sealed record Result(
        string ImagePath,
        Dictionary<string, object> Ocr,
        Dictionary<string, object> Info);

    public static Result RecognizeBest(OcrEpson engine, string imagePath, string language)
    {
        using var source = Image.Load<Rgba32>(imagePath);
        double ratio = (double)source.Width / Math.Max(1, source.Height);

        // A horizontal parts label only needs 0/180 degrees.  For a portrait
        // or nearly square crop, include quarter-turn candidates as well.
        int[] rotations = ratio >= 1.35
            ? new[] { 0, 180 }
            : new[] { 0, 90, 180, 270 };

        string workDirectory = Path.Combine(Path.GetTempPath(), "OCRSample", "orientation", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(workDirectory);
        try
        {
            var candidates = new List<Candidate>();
            // A USB/WIA scan can be loaded upside down, but it does not
            // produce a left/right mirror image.  Mirrored candidates can
            // fabricate plausible-looking Latin text and must not win the
            // automatic part-number decision.
            foreach (bool mirrored in new[] { false })
            {
                foreach (int rotation in rotations)
                {
                    string candidatePath = Path.Combine(workDirectory, $"orientation_{rotation}_{(mirrored ? "mirror" : "normal")}.bmp");
                    using Image<Rgba32> candidate = CreateCandidate(source, rotation, mirrored);
                    candidate.SaveAsBmp(candidatePath);

                    Dictionary<string, object> ocr = engine.ImageToSearchablePdf(
                        candidatePath,
                        outPdf: string.Empty,
                        lang: language,
                        detectOrientation: false,
                        partNoOverride: null,
                        buildPdf: false);

                    candidates.Add(new Candidate(
                        rotation,
                        mirrored,
                        candidatePath,
                        ocr,
                        LayoutScore(candidate),
                        OcrScore(ocr)));
                }
            }

            Candidate best = candidates
                .OrderByDescending(candidate => candidate.Score)
                .ThenBy(candidate => candidate.Mirrored)
                .ThenBy(candidate => candidate.Rotation == 0 ? 0 : 1)
                .First();

            string selectedPath = imagePath;
            if (best.Rotation != 0 || best.Mirrored)
            {
                string directory = Path.GetDirectoryName(imagePath) ?? AppContext.BaseDirectory;
                string outputName = Path.GetFileNameWithoutExtension(imagePath) + "_oriented.bmp";
                selectedPath = Path.Combine(directory, outputName);
                File.Copy(best.Path, selectedPath, overwrite: true);
            }

            var candidateInfo = candidates.Select(candidate => new Dictionary<string, object>
            {
                ["rotation"] = candidate.Rotation,
                ["mirrored"] = candidate.Mirrored,
                ["score"] = Math.Round(candidate.Score, 2),
                ["layout_score"] = Math.Round(candidate.LayoutScore, 2),
                ["ocr_score"] = Math.Round(candidate.OcrScore, 2),
                ["part_no"] = GetString(candidate.Ocr, "part_no"),
            }).ToList();

            var info = new Dictionary<string, object>
            {
                ["method"] = "layout+ocr",
                ["rotation"] = best.Rotation,
                ["mirrored"] = best.Mirrored,
                ["score"] = Math.Round(best.Score, 2),
                ["candidates"] = candidateInfo,
            };
            return new Result(selectedPath, best.Ocr, info);
        }
        finally
        {
            try { Directory.Delete(workDirectory, true); } catch { }
        }
    }

    private static Image<Rgba32> CreateCandidate(Image<Rgba32> source, int rotation, bool mirrored)
    {
        Image<Rgba32> candidate = source.Clone();
        candidate.Mutate(context =>
        {
            if (mirrored)
                context.Flip(FlipMode.Horizontal);
            if (rotation != 0)
                context.Rotate(rotation).BackgroundColor(Color.White);
        });
        return candidate;
    }

    private static double OcrScore(Dictionary<string, object> ocr)
    {
        string text = GetString(ocr, "text");
        int printable = text.Count(IsReadableCharacter);
        int meaningful = text.Count(ch => char.IsLetterOrDigit(ch));
        int words = Regex.Matches(text, @"[A-Za-z0-9가-힣]{2,}").Count;
        double confidence = GetNumber(GetObject(ocr, "quality"), "confidence");
        double validRatio = GetNumber(GetObject(ocr, "quality"), "valid_ratio");
        int letters = (int)GetNumber(ocr, "letters");

        double score = confidence * 5.0 + validRatio * 7.0;
        score += Math.Min(8.0, meaningful / 14.0);
        score += Math.Min(4.0, words / 3.0);
        score += Math.Min(3.0, letters / 70.0);
        if (text.Length > 0)
            score += 3.0 * printable / text.Length;
        score += PartNoScore(GetString(ocr, "part_no"));
        if (Regex.IsMatch(text, @"\d{6,}"))
            score += 1.5;
        return score;
    }

    private static double PartNoScore(string partNo)
    {
        string normalized = Regex.Replace(partNo ?? string.Empty, @"\s+", string.Empty)
            .Replace('\u2010', '-')
            .Replace('\u2011', '-')
            .Replace('\u2012', '-')
            .Replace('\u2013', '-')
            .Replace('\u2014', '-');
        if (string.IsNullOrEmpty(normalized))
            return 0.0;

        bool validCharacters = normalized.All(ch => char.IsLetterOrDigit(ch) || ch == '-');
        bool hasLetter = normalized.Any(char.IsLetter);
        bool hasDigit = normalized.Any(char.IsDigit);
        bool sensibleLength = normalized.Length is >= 6 and <= 20;
        bool sensibleSegments = !normalized.StartsWith('-') && !normalized.EndsWith('-') &&
                                !normalized.Contains("--") &&
                                normalized.Split('-').All(segment => segment.Length is > 0 and <= 10);
        return validCharacters && hasLetter && hasDigit && sensibleLength && sensibleSegments
            ? 7.0
            : -10.0;
    }

    // A barcode normally has much more rapid black/white change in the lower
    // label band than the upper band.  A dark inspection panel, if present,
    // is also expected on the left in the upright orientation.  Both terms
    // are bounded so ordinary labels without those features still fall back
    // to OCR readability rather than being forced into a wrong direction.
    private static double LayoutScore(Image<Rgba32> image)
    {
        double bottomTransitions = HorizontalTransitionDensity(image, 0.63, 0.93);
        double topTransitions = HorizontalTransitionDensity(image, 0.07, 0.37);
        double leftInk = DarkPixelDensity(image, 0.00, 0.20, 0.10, 0.90);
        double rightInk = DarkPixelDensity(image, 0.80, 1.00, 0.10, 0.90);

        double barcode = Math.Clamp((bottomTransitions - topTransitions) * 32.0, -10.0, 10.0);
        double panel = Math.Clamp((leftInk - rightInk) * 22.0, -5.0, 5.0);
        return barcode + panel;
    }

    private static double HorizontalTransitionDensity(Image<Rgba32> image, double top, double bottom)
    {
        int y0 = Math.Clamp((int)Math.Round(image.Height * top), 0, image.Height - 1);
        int y1 = Math.Clamp((int)Math.Round(image.Height * bottom), y0 + 1, image.Height);
        int yStep = Math.Max(1, (y1 - y0) / 24);
        int xStep = Math.Max(1, image.Width / 600);
        long transitions = 0;
        long comparisons = 0;

        image.ProcessPixelRows(accessor =>
        {
            for (int y = y0; y < y1; y += yStep)
            {
                Span<Rgba32> row = accessor.GetRowSpan(y);
                bool previous = IsDark(row[0]);
                for (int x = xStep; x < row.Length; x += xStep)
                {
                    bool current = IsDark(row[x]);
                    if (current != previous) transitions++;
                    previous = current;
                    comparisons++;
                }
            }
        });
        return comparisons == 0 ? 0.0 : (double)transitions / comparisons;
    }

    private static double DarkPixelDensity(Image<Rgba32> image, double left, double right, double top, double bottom)
    {
        int x0 = Math.Clamp((int)Math.Round(image.Width * left), 0, image.Width - 1);
        int x1 = Math.Clamp((int)Math.Round(image.Width * right), x0 + 1, image.Width);
        int y0 = Math.Clamp((int)Math.Round(image.Height * top), 0, image.Height - 1);
        int y1 = Math.Clamp((int)Math.Round(image.Height * bottom), y0 + 1, image.Height);
        int xStep = Math.Max(1, (x1 - x0) / 240);
        int yStep = Math.Max(1, (y1 - y0) / 240);
        long dark = 0;
        long total = 0;

        image.ProcessPixelRows(accessor =>
        {
            for (int y = y0; y < y1; y += yStep)
            {
                Span<Rgba32> row = accessor.GetRowSpan(y);
                for (int x = x0; x < x1; x += xStep)
                {
                    if (IsDark(row[x])) dark++;
                    total++;
                }
            }
        });
        return total == 0 ? 0.0 : (double)dark / total;
    }

    private static bool IsDark(Rgba32 pixel) =>
        pixel.R * 30 + pixel.G * 59 + pixel.B * 11 < 15000;

    private static bool IsReadableCharacter(char character) =>
        char.IsWhiteSpace(character) || char.IsLetterOrDigit(character) || "-_/.:*()[]".Contains(character);

    private static Dictionary<string, object>? GetObject(Dictionary<string, object> source, string key) =>
        source.TryGetValue(key, out object? value) ? value as Dictionary<string, object> : null;

    private static string GetString(Dictionary<string, object> source, string key) =>
        source.TryGetValue(key, out object? value) ? value?.ToString() ?? string.Empty : string.Empty;

    private static double GetNumber(Dictionary<string, object>? source, string key)
    {
        if (source == null || !source.TryGetValue(key, out object? value) || value == null)
            return 0.0;
        return double.TryParse(value.ToString(), out double number) ? number : 0.0;
    }
}
