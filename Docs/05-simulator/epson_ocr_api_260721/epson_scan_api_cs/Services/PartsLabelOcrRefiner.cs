using System.Text.RegularExpressions;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace EpsonScanApi.Services;

/// <summary>
/// Applies a dedicated OCR pass only to the first row of the fixed parts
/// label.  The full-label pass is retained for Korean text and barcodes.
/// </summary>
internal static class PartsLabelOcrRefiner
{
    private sealed record Region(string Name, double X, double Y, double Width, double Height);

    // The left inspection panel is deliberately excluded.  Its vertical
    // strokes can otherwise be recognized as a leading I/1 before the part
    // number.  The coordinates include the small border kept by CardExtractor.
    private static readonly Region PartRegion = new("part", 0.160, 0.205, 0.220, 0.170);

    public static bool TryRefine(
        OcrEpson engine,
        string labelPath,
        Dictionary<string, object> fullResult,
        out string text,
        out string partNo)
    {
        text = string.Empty;
        partNo = string.Empty;

        try
        {
            using var label = Image.Load<Rgba32>(labelPath);
            double ratio = (double)label.Width / Math.Max(1, label.Height);
            // Deskew expands the output canvas.  A strongly tilted horizontal
            // label can therefore temporarily look almost square even though
            // its text rows are valid.  Reject only genuinely narrow/invalid
            // images and let FindFirstTextRow make the final decision.
            if (label.Width < 500 || label.Height < 200 || ratio < 0.75 || ratio > 5.0)
                return false;

            string workDirectory = Path.Combine(
                Path.GetTempPath(), "OCRSample", "part-row", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(workDirectory);
            try
            {
                // The crop can contain a different amount of white margin at
                // each skew angle.  Locate the first substantial text band
                // instead of assuming a fixed Y coordinate; retain the old
                // profile only as a safe fallback for unfamiliar labels.
                Rectangle rectangle = FindTextRow(label, 0) ??
                                      ToRectangle(label.Width, label.Height, PartRegion);
                string rowPath = Path.Combine(workDirectory, "part.bmp");
                using (Image<Rgba32> row = label.Clone())
                {
                    // The fixed Epson Korean profile can join adjacent narrow
                    // digits (notably "0161") into one Hangul glyph.  Keeping
                    // the height but doubling only the width separates those
                    // characters without altering their strokes.
                    row.Mutate(context => context
                        .Crop(rectangle)
                        .Resize(rectangle.Width * 2, rectangle.Height, KnownResamplers.NearestNeighbor));
                    row.SaveAsBmp(rowPath);
                }

                // OcrEpson is intentionally configured with Epson's Korean
                // profile; the resized row above is the safe way to preserve
                // the Korean full-label result while recognizing this serial.
                Dictionary<string, object> partResult = engine.ImageToSearchablePdf(
                    rowPath,
                    outPdf: string.Empty,
                    lang: "kor+eng",
                    detectOrientation: false,
                    partNoOverride: null,
                    buildPdf: false);

                string rowPartNo = FindPartNo(GetText(partResult));
                string partCropPath = Path.Combine(
                    Path.GetDirectoryName(labelPath) ?? workDirectory,
                    Path.GetFileNameWithoutExtension(labelPath) + "_part.bmp");
                using (Image<Rgba32> originalRow = label.Clone())
                {
                    // Preserve the clean first-row crop for the local x64
                    // Windows OCR second opinion.  It is deliberately kept
                    // separate from the Korean Epson image pipeline.
                    originalRow.Mutate(context => context.Crop(rectangle));
                    originalRow.SaveAsBmp(partCropPath);
                }
                string fullLabelPartNo = GetText(fullResult, "part_no");
                fullResult["part_no_epson_full"] = fullLabelPartNo;
                fullResult["part_no_epson_row"] = rowPartNo;
                partNo = ChoosePartNo(fullLabelPartNo, rowPartNo);
                fullResult["part_crop_path"] = partCropPath;
                if (string.IsNullOrWhiteSpace(partNo))
                    return false;

                text = ReplacePartNoInFullText(GetText(fullResult), partNo);
                Rectangle? descriptionRectangle = FindTextRow(label, 1);
                if (descriptionRectangle.HasValue)
                {
                    string descriptionPath = Path.Combine(workDirectory, "description.bmp");
                    using (Image<Rgba32> descriptionRow = label.Clone())
                    {
                        descriptionRow.Mutate(context => context
                            .Crop(descriptionRectangle.Value)
                            .Resize(descriptionRectangle.Value.Width * 2, descriptionRectangle.Value.Height, KnownResamplers.NearestNeighbor));
                        descriptionRow.SaveAsBmp(descriptionPath);
                    }

                    Dictionary<string, object> descriptionResult = engine.ImageToSearchablePdf(
                        descriptionPath,
                        outPdf: string.Empty,
                        lang: "kor+eng",
                        detectOrientation: false,
                        partNoOverride: null,
                        buildPdf: false);
                    string description = CleanLine(GetText(descriptionResult));
                    if (ShouldReplaceDescription(GetText(fullResult), description))
                        text = ReplaceDescriptionInFullText(text, description);
                }
                text = NormalizeKnownLabelText(text);
                return true;
            }
            finally
            {
                try { Directory.Delete(workDirectory, true); } catch { }
            }
        }
        catch
        {
            text = string.Empty;
            partNo = string.Empty;
            return false;
        }
    }

    private static Rectangle ToRectangle(int imageWidth, int imageHeight, Region region)
    {
        int x = Math.Max(0, (int)Math.Round(imageWidth * region.X));
        int y = Math.Max(0, (int)Math.Round(imageHeight * region.Y));
        int width = Math.Min(imageWidth - x, Math.Max(1, (int)Math.Round(imageWidth * region.Width)));
        int height = Math.Min(imageHeight - y, Math.Max(1, (int)Math.Round(imageHeight * region.Height)));
        return new Rectangle(x, y, width, height);
    }

    private static Rectangle? FindTextRow(Image<Rgba32> label, int rowIndex)
    {
        int width = label.Width;
        int height = label.Height;
        int textLeft = Math.Clamp((int)Math.Round(width * 0.20), 0, width - 1);
        // The optional "(KR)" mark sits above the quantity column and is
        // taller than the adjacent text.  Including it makes the description
        // and company rows look like a single vertical ink band.  The first
        // two left-hand label rows always fit in the left half of this format.
        int textRight = Math.Clamp((int)Math.Round(width * 0.50), textLeft + 1, width);
        int yStart = Math.Clamp((int)Math.Round(height * 0.06), 0, height - 1);
        int yEnd = Math.Clamp((int)Math.Round(height * 0.70), yStart + 1, height);
        int minimumInkPerRow = Math.Max(8, (textRight - textLeft) / 100);
        int[] rowInk = new int[height];

        label.ProcessPixelRows(accessor =>
        {
            for (int y = yStart; y < yEnd; y++)
            {
                Span<Rgba32> row = accessor.GetRowSpan(y);
                int ink = 0;
                for (int x = textLeft; x < textRight; x++)
                {
                    if (IsDark(row[x])) ink++;
                }
                rowInk[y] = ink;
            }
        });

        int mergeGap = Math.Max(5, height / 110);
        int minimumBandHeight = Math.Max(18, height / 35);
        int bandStart = -1;
        int lastInkRow = -1;
        var bands = new List<(int Top, int Bottom)>();
        for (int y = yStart; y < yEnd; y++)
        {
            if (rowInk[y] >= minimumInkPerRow)
            {
                if (bandStart < 0) bandStart = y;
                lastInkRow = y;
                continue;
            }

            if (bandStart >= 0 && y - lastInkRow > mergeGap)
            {
                if (lastInkRow - bandStart + 1 >= minimumBandHeight)
                    bands.Add((bandStart, lastInkRow));
                bandStart = -1;
                lastInkRow = -1;
            }
        }
        if (bandStart >= 0 && lastInkRow - bandStart + 1 >= minimumBandHeight)
            bands.Add((bandStart, lastInkRow));
        if (rowIndex < 0 || rowIndex >= bands.Count)
            return null;
        var selectedBand = bands[rowIndex];

        int boundLeft = Math.Clamp((int)Math.Round(width * 0.13), 0, width - 1);
        int boundRight = Math.Clamp((int)Math.Round(width * 0.52), boundLeft + 1, width);
        int[] columnInk = new int[width];
        label.ProcessPixelRows(accessor =>
        {
            for (int y = selectedBand.Top; y <= selectedBand.Bottom; y++)
            {
                Span<Rgba32> row = accessor.GetRowSpan(y);
                for (int x = boundLeft; x < boundRight; x++)
                {
                    if (IsDark(row[x])) columnInk[x]++;
                }
            }
        });

        int firstInk = -1;
        int lastInk = -1;
        for (int x = boundLeft; x < boundRight; x++)
        {
            if (columnInk[x] <= 0) continue;
            if (firstInk < 0) firstInk = x;
            lastInk = x;
        }
        if (firstInk < 0 || lastInk <= firstInk)
            return null;

        // When the black inspection panel touches the search range, skip its
        // long following white gap and start at the actual printed part row.
        if (firstInk <= boundLeft + 4)
        {
            // The gap between the panel and the first printed character can
            // be only 10-15 pixels on a strongly skewed label.  The previous
            // threshold missed that gap, leaving panel ink in the OCR crop
            // and changing a clear "71FG" into "11FG".
            int gapNeeded = Math.Max(8, width / 180);
            int blankRun = 0;
            bool sawInk = false;
            for (int x = boundLeft; x < boundRight; x++)
            {
                if (columnInk[x] > 0)
                {
                    if (sawInk && blankRun >= gapNeeded)
                    {
                        firstInk = x;
                        break;
                    }
                    sawInk = true;
                    blankRun = 0;
                }
                else if (sawInk)
                {
                    blankRun++;
                }
            }
        }

        // Epson's line recognizer loses small glyph details when a crop ends
        // immediately after the ink.  In particular, a tightly cropped
        // "ELBOW-90" was read as "ELBOW-go".  Keep meaningful white padding
        // around the selected band, while the x=20..60% detection range still
        // keeps the right-side quantity column out of this crop.
        int marginLeft = Math.Max(12, width / 150);
        int marginRight = rowIndex == 0
            ? Math.Max(35, width / 30)
            : Math.Max(80, width / 18);
        int marginY = Math.Max(15, height / 45);
        int x0 = Math.Max(0, firstInk - marginLeft);
        int x1 = Math.Min(width, lastInk + marginRight + 1);
        int y0 = Math.Max(0, selectedBand.Top - marginY);
        int y1 = Math.Min(height, selectedBand.Bottom + marginY + 1);
        return x1 > x0 && y1 > y0 ? new Rectangle(x0, y0, x1 - x0, y1 - y0) : null;
    }

    private static bool IsDark(Rgba32 pixel) =>
        pixel.R * 30 + pixel.G * 59 + pixel.B * 11 < 15000;

    private static string GetText(Dictionary<string, object> result)
    {
        return result.TryGetValue("text", out object? value) ? value?.ToString() ?? string.Empty : string.Empty;
    }

    private static string GetText(Dictionary<string, object> result, string key)
    {
        return result.TryGetValue(key, out object? value) ? value?.ToString() ?? string.Empty : string.Empty;
    }

    private static string ChoosePartNo(string fullLabelPartNo, string rowPartNo)
    {
        string full = CleanLine(fullLabelPartNo).Replace(" ", string.Empty);
        if (IsPartNoCandidate(full))
        {
            // The dedicated crop occasionally contains a thin remnant of the
            // left inspection panel.  If it merely prepends a character to the
            // full-label candidate (H31HA instead of 31HA), the original OCR
            // result is the trustworthy value.
            return full;
        }
        return rowPartNo;
    }

    private static bool IsPartNoCandidate(string value) =>
        Regex.IsMatch(value, @"^[A-Z0-9][A-Z0-9-]{4,}$", RegexOptions.IgnoreCase) &&
        value.Any(char.IsLetter) && value.Any(char.IsDigit);

    private static string FindPartNo(string value)
    {
        // A valid part number can begin with a digit (for example
        // "31HA-20011"), so do not assume a leading alphabetic character.
        // The two lookaheads prevent a barcode-only numeric value from being
        // accepted as the first-row part number.
        Match match = Regex.Match(
            value.ToUpperInvariant(),
            @"(?=[A-Z0-9-]{5,})(?=[A-Z0-9-]*[A-Z])(?=[A-Z0-9-]*\d)[A-Z0-9][A-Z0-9-]{4,}");
        return match.Success ? match.Value : string.Empty;
    }

    private static bool IsDescriptionCandidate(string value) =>
        value.Length is >= 4 and <= 80 && Regex.IsMatch(value, @"[A-Z]{2,}", RegexOptions.IgnoreCase);

    private static bool ShouldReplaceDescription(string fullText, string candidate)
    {
        if (!IsDescriptionCandidate(candidate))
            return false;

        string original = fullText.Replace("\r", string.Empty)
            .Split('\n')
            .Select(CleanLine)
            .FirstOrDefault(line =>
                !IsPartNoCandidate(line.Replace(" ", string.Empty)) &&
                Regex.IsMatch(line, @"^[A-Za-z][A-Za-z0-9 .-]{3,}$")) ?? string.Empty;
        if (string.IsNullOrWhiteSpace(original))
            return true;

        string source = NormalizeDescription(original);
        string refined = NormalizeDescription(candidate);
        if (source.Length == 0 || refined.Length == 0)
            return false;

        // A legitimate correction normally changes only one or two confused
        // characters (ELBOW-go -> ELBOW-90).  Reject a crop that appends QR,
        // quantity, or barcode text to an otherwise sound full-line read.
        if (refined.Length > source.Length + 2)
            return false;

        int distance = LevenshteinDistance(source, refined);
        return distance <= Math.Max(2, source.Length / 3);
    }

    private static string NormalizeDescription(string value) =>
        Regex.Replace((value ?? string.Empty).ToUpperInvariant(), @"[^A-Z0-9]", string.Empty);

    private static int LevenshteinDistance(string left, string right)
    {
        int[] previous = Enumerable.Range(0, right.Length + 1).ToArray();
        for (int i = 1; i <= left.Length; i++)
        {
            int[] current = new int[right.Length + 1];
            current[0] = i;
            for (int j = 1; j <= right.Length; j++)
            {
                int cost = left[i - 1] == right[j - 1] ? 0 : 1;
                current[j] = Math.Min(
                    Math.Min(current[j - 1] + 1, previous[j] + 1),
                    previous[j - 1] + cost);
            }
            previous = current;
        }
        return previous[right.Length];
    }

    private static string ReplaceDescriptionInFullText(string fullText, string description)
    {
        var lines = fullText.Replace("\r", string.Empty)
            .Split('\n')
            .Select(CleanLine)
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .ToList();
        int descriptionLine = lines.FindIndex(1, line => Regex.IsMatch(line, @"[A-Za-z]{2,}"));
        if (descriptionLine >= 0)
            lines[descriptionLine] = description;
        return string.Join(Environment.NewLine, lines);
    }

    internal static string ReplacePartNoInFullText(string fullText, string partNo)
    {
        var lines = fullText.Replace("\r", string.Empty)
            .Split('\n')
            .Select(CleanLine)
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .ToList();

        int partLine = lines.FindIndex(line =>
            Regex.IsMatch(line, @"^[A-Z][A-Z0-9-]*\d[A-Z0-9-]*$", RegexOptions.IgnoreCase));
        if (partLine < 0)
        {
            // If the original first row was too damaged to match a part
            // number, discard only the leading OCR noise.  Do not replace the
            // following description line (for example "FORK ASSY-2400").
            while (lines.Count > 0 && !Regex.IsMatch(lines[0], @"^[A-Za-z]{2,}"))
                lines.RemoveAt(0);
            lines.Insert(0, partNo);
            return string.Join(Environment.NewLine, lines);
        }

        // A clipped QR/left border sometimes produces a standalone number
        // above the real part-number row.  It is not label text.
        if (partLine > 0 && lines.Take(partLine).All(line => Regex.IsMatch(line, @"^[^A-Za-z]+$")))
        {
            lines.RemoveRange(0, partLine);
            partLine = 0;
        }

        lines[partLine] = partNo;
        return string.Join(Environment.NewLine, lines);
    }

    private static string NormalizeKnownLabelText(string value)
    {
        var lines = value.Replace("\r", string.Empty)
            .Split('\n')
            .Select(CleanLine)
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .Select(line => Regex.Replace(line, @"^[^A-Za-z0-9]+(?=[A-Za-z0-9]{5,}$)", string.Empty))
            .ToList();

        // Barcode strokes sometimes become arbitrary short OCR lines.  In
        // this label template the compact tracking code is the last printed
        // text after the 16-digit management number, so discard only content
        // that follows that code.
        int managementLine = lines.FindIndex(line => Regex.IsMatch(line, @"\b\d{16}\b"));
        if (managementLine >= 0)
        {
            int trackingLine = lines.FindIndex(managementLine + 1,
                line => Regex.IsMatch(line, @"^[A-Za-z0-9]{6,}$"));
            if (trackingLine >= 0 && trackingLine + 1 < lines.Count)
                lines.RemoveRange(trackingLine + 1, lines.Count - trackingLine - 1);
        }

        for (int index = 0; index < lines.Count; index++)
        {
            // Normalize recurring supplier-label notation only when its
            // surrounding fixed text is present.
            lines[index] = Regex.Replace(
                lines[index],
                @"^(KR\d{4})\s*[《〈<]?\s*주\s*[)》〉>]?\s*",
                "$1 (주)");
            lines[index] = Regex.Replace(
                lines[index],
                @"(카톤\s+더블)[철월](?=\s+박스)",
                "$1월");
            lines[index] = Regex.Replace(
                lines[index],
                @"(박스)\s*\(?\s*내부포장\s*\)?",
                "$1( 내부포장 )");

            // Apply phrase corrections only when the fixed Korean label
            // anchors are present.  This avoids changing arbitrary OCR text
            // while correcting visually similar Hangul glyph confusion.
            lines[index] = Regex.Replace(lines[index], @"(포장제원\s+)미[둥등뇽]록", "$1미등록");
            lines[index] = Regex.Replace(lines[index], @"(선행포장\s+제외\s+대표)[퓸품]목", "$1품목");
            lines[index] = Regex.Replace(lines[index], @"주식[화회][사자]", "주식회사");

            // Supplier labels use the KR + four-digit supplier-code format.
            // When the Korean OCR profile turns the short Latin prefix into
            // Hangul (for example 지긋0130), restore it only on a line that
            // is immediately followed by a Korean company name.
            if (!Regex.IsMatch(lines[index], @"^KR\d{4}\s+[가-힣]") &&
                Regex.IsMatch(lines[index], @"^[^\s]{2}\d{4}\s+[가-힣]"))
            {
                lines[index] = Regex.Replace(lines[index], @"^[^\s]{2}(?=\d{4}\s+[가-힣])", "KR");
            }

            // The management number is followed by a MM/DD date.  A slash
            // is commonly recognized as a narrow 1 on this Epson profile.
            lines[index] = Regex.Replace(
                lines[index],
                @"(?<=\b\d{16}\s)(0[1-9]|1[0-2])[1I|](0[1-9]|[12]\d|3[01])\b",
                "$1/$2");
        }
        return string.Join(Environment.NewLine, lines);
    }

    private static string CleanLine(string value)
    {
        return Regex.Replace(value ?? string.Empty, @"\s+", " ").Trim();
    }
}
