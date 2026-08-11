using System.Text.Json;
using Microsoft.Extensions.Configuration;

namespace EpsonScanApi.Services;

/// <summary>
/// HTTP 없이 이미지 파일 한 장을 Epson OCR로 처리하는 x86 로컬 작업자 모드입니다.
///
/// 사용:
///   EpsonScanApi.exe --ocr-file "C:\scan.png" --ocr-result "C:\result.json" --lang "kor+eng"
/// </summary>
public static class OcrFileWorker
{
    public static bool IsRequested(string[] args) =>
        args != null && args.Any(a => string.Equals(a, "--ocr-file", StringComparison.OrdinalIgnoreCase));

    public static int Run(string[] args)
    {
        string imagePath = GetOption(args, "--ocr-file");
        string resultPath = GetOption(args, "--ocr-result");
        string language = GetOption(args, "--lang");
        bool skipCardExtraction = HasOption(args, "--ocr-no-card");
        if (string.IsNullOrWhiteSpace(language)) language = "kor+eng";

        if (string.IsNullOrWhiteSpace(resultPath))
        {
            Console.Error.WriteLine("--ocr-result 경로가 필요합니다.");
            return 2;
        }

        try
        {
            if (string.IsNullOrWhiteSpace(imagePath) || !File.Exists(imagePath))
                throw new FileNotFoundException("OCR할 이미지 파일을 찾을 수 없습니다.", imagePath);

            string? resultDirectory = Path.GetDirectoryName(Path.GetFullPath(resultPath));
            if (!string.IsNullOrWhiteSpace(resultDirectory))
                Directory.CreateDirectory(resultDirectory);

            IConfiguration configuration = new ConfigurationBuilder()
                .SetBasePath(AppContext.BaseDirectory)
                .AddJsonFile("appsettings.json", optional: true)
                .AddEnvironmentVariables()
                .Build();

            TuningOptions.Load(configuration);

            // The ADF produces a full page even when only a small parts label is
            // loaded.  OCRing the whole page makes the label text needlessly
            // small and lets the page whitespace distort layout detection.  Use
            // the same card extraction stage as the sample server pipeline.
            string ocrSourcePath = Path.Combine(
                Path.GetDirectoryName(imagePath) ?? AppContext.BaseDirectory,
                Path.GetFileNameWithoutExtension(imagePath) + "_label.bmp");
            Dictionary<string, object> cardInfo;
            try
            {
                if (skipCardExtraction)
                {
                    ocrSourcePath = imagePath;
                    cardInfo = new Dictionary<string, object>
                    {
                        ["cropped"] = false,
                        ["skipped"] = true,
                    };
                }
                else
                {
                    var extraction = CardExtractor.ExtractLabel(imagePath, ocrSourcePath);
                    ocrSourcePath = extraction.OutPath;
                    cardInfo = extraction.Info;
                }
            }
            catch (Exception extractionError)
            {
                // A label crop failure must not prevent a usable OCR attempt.
                // Fall back to the original image and make the reason visible.
                ocrSourcePath = imagePath;
                cardInfo = new Dictionary<string, object>
                {
                    ["cropped"] = false,
                    ["fallback_reason"] = extractionError.Message,
                };
                Console.Error.WriteLine("[OcrFileWorker] Label extraction fallback: " + extractionError);
            }

            using var epson = new OcrEpson();
            Dictionary<string, object> ocr;
            try
            {
                LabelOrientationResolver.Result orientation = LabelOrientationResolver.RecognizeBest(
                    epson,
                    ocrSourcePath,
                    language);
                ocrSourcePath = orientation.ImagePath;
                ocr = orientation.Ocr;
                cardInfo["orientation"] = orientation.Info;
            }
            catch (Exception orientationError)
            {
                // Orientation selection is an enhancement.  Preserve a normal
                // OCR attempt if a malformed image prevents candidate creation.
                cardInfo["orientation_fallback_reason"] = orientationError.Message;
                ocr = epson.ImageToSearchablePdf(
                    ocrSourcePath,
                    outPdf: string.Empty,
                    lang: language,
                    detectOrientation: false,
                    partNoOverride: null,
                    buildPdf: false);
            }

            if (cardInfo.TryGetValue("cropped", out object? croppedValue) &&
                croppedValue is bool cropped && cropped &&
                PartsLabelOcrRefiner.TryRefine(epson, ocrSourcePath, ocr, out string refinedText, out string refinedPartNo))
            {
                ocr["engine_raw_text"] = ocr.TryGetValue("text", out object? rawText) ? rawText : string.Empty;
                ocr["text"] = refinedText;
                ocr["part_no"] = refinedPartNo;
                ocr["label_row_refinement"] = true;
            }

            ApplyEpsonPartDecision(ocr);

            WriteResult(resultPath, new
            {
                success = true,
                image_path = imagePath,
                ocr_source_path = ocrSourcePath,
                card = cardInfo,
                ocr,
            });
            return 0;
        }
        catch (Exception ex)
        {
            try
            {
                WriteResult(resultPath, new
                {
                    success = false,
                    error = ex.Message,
                });
            }
            catch
            {
                // 결과 파일도 쓸 수 없는 경우에는 프로세스 종료 코드로만 오류를 알린다.
            }

            Console.Error.WriteLine(ex);
            return 1;
        }
    }

    private static void ApplyEpsonPartDecision(Dictionary<string, object> ocr)
    {
        string partNo = GetString(ocr, "part_no");
        string fullCandidate = GetString(ocr, "part_no_epson_full");
        string rowCandidate = GetString(ocr, "part_no_epson_row");
        var failures = new List<string>();

        if (!IsEpsonPartNoCandidate(partNo, out string partNoReason))
            failures.Add(partNoReason);

        bool fullValid = IsEpsonPartNoCandidate(fullCandidate, out _);
        bool rowValid = IsEpsonPartNoCandidate(rowCandidate, out _);
        if (fullValid && rowValid && !SamePartNo(fullCandidate, rowCandidate))
            failures.Add("전체 라벨과 전용 행의 Epson 부품번호 결과가 서로 다릅니다.");

        Dictionary<string, object>? quality = ocr.TryGetValue("quality", out object? qualityValue)
            ? qualityValue as Dictionary<string, object>
            : null;
        bool qualityOk = quality == null || GetBool(quality, "ok");
        if (!qualityOk)
            failures.Add("Epson OCR 품질 기준에 미달했습니다.");

        ocr["part_no_epson"] = partNo;
        if (failures.Count == 0)
        {
            ocr["part_no_source"] = "epson";
            ocr["needs_confirmation"] = false;
            return;
        }

        // Keep a rejected candidate only in the diagnostic JSON.  It must
        // never be presented as the final part-number decision in the UI.
        ocr["part_no"] = string.Empty;
        ocr["part_no_sub"] = string.Empty;
        ocr["part_no_source"] = "epson_failed";
        ocr["part_no_failure_reason"] = string.Join(" ", failures);
        ocr["needs_confirmation"] = true;

        if (quality == null)
        {
            quality = new Dictionary<string, object>();
            ocr["quality"] = quality;
        }

        double glyphConfidence = GetNumber(quality, "confidence");
        quality["glyph_confidence"] = glyphConfidence;
        quality["confidence"] = 0.0;
        quality["ok"] = false;
        quality["reason"] = "Epson OCR 부품번호 판정 실패: " + string.Join(" ", failures);
    }

    private static bool IsEpsonPartNoCandidate(string value, out string reason)
    {
        string normalized = CleanPartNo(value)
            .Replace('\u2010', '-')
            .Replace('\u2011', '-')
            .Replace('\u2012', '-')
            .Replace('\u2013', '-')
            .Replace('\u2014', '-');

        if (normalized.Length < 6)
        {
            reason = "부품번호 후보가 없거나 너무 짧습니다.";
            return false;
        }

        if (normalized[0] == '-' || normalized[^1] == '-' || normalized.Contains("--") ||
            normalized.Any(ch => !char.IsLetterOrDigit(ch) && ch != '-') ||
            !normalized.Any(char.IsLetter) || !normalized.Any(char.IsDigit))
        {
            reason = "부품번호 형식이 올바르지 않습니다.";
            return false;
        }

        if (normalized.Split('-').Any(segment => segment.Length > 10))
        {
            reason = "부품번호에 비정상적으로 긴 연속 문자열이 있습니다.";
            return false;
        }

        reason = string.Empty;
        return true;
    }

    private static void ApplyWindowsPartOcr(Dictionary<string, object> ocr)
    {
        if (!ocr.TryGetValue("part_crop_path", out object? cropValue) ||
            string.IsNullOrWhiteSpace(cropValue?.ToString()))
            return;

        string epsonPartNo = GetString(ocr, "part_no");
        if (!WindowsPartOcrClient.TryRecognize(cropValue.ToString()!, out string windowsPartNo, out string reason))
        {
            ocr["part_no_windows_reason"] = reason;
            return;
        }

        bool agreement = SamePartNo(epsonPartNo, windowsPartNo);
        ocr["part_no_epson"] = epsonPartNo;
        ocr["part_no_windows"] = windowsPartNo;
        ocr["part_no"] = windowsPartNo;
        ocr["part_no_source"] = agreement ? "epson+windows" : "windows_disagree";
        if (ocr.TryGetValue("text", out object? textValue))
            ocr["text"] = PartsLabelOcrRefiner.ReplacePartNoInFullText(
                textValue?.ToString() ?? string.Empty,
                windowsPartNo);

        Dictionary<string, object>? quality = ocr.TryGetValue("quality", out object? qualityValue)
            ? qualityValue as Dictionary<string, object>
            : null;
        if (quality == null)
            return;

        double glyphConfidence = GetNumber(quality, "confidence");
        quality["glyph_confidence"] = glyphConfidence;
        quality["part_number_agreement"] = agreement;
        quality["confidence_method"] = "glyph-validity + independent part-number agreement";
        if (agreement)
        {
            quality["confidence"] = Math.Min(0.99, Math.Max(glyphConfidence, 0.95));
            return;
        }

        // The Windows result is produced from a clean, English-only first-row
        // crop and is more appropriate for this field.  Still, disagreement
        // is evidence that the result must be reviewed, never a 96% claim.
        quality["confidence"] = 0.55;
        quality["ok"] = false;
        quality["reason"] =
            "부품번호 교차 OCR 불일치: Epson=" + epsonPartNo +
            ", Windows=" + windowsPartNo + ". Windows 전용 행 결과를 표시하며 확인이 필요합니다.";
        ocr["needs_confirmation"] = true;
    }

    private static string GetString(Dictionary<string, object> source, string key) =>
        source.TryGetValue(key, out object? value) ? value?.ToString() ?? string.Empty : string.Empty;

    private static double GetNumber(Dictionary<string, object> source, string key)
    {
        if (!source.TryGetValue(key, out object? value) || value == null)
            return 0.0;
        return double.TryParse(value.ToString(), System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture, out double number) ? number : 0.0;
    }

    private static bool GetBool(Dictionary<string, object> source, string key)
    {
        if (!source.TryGetValue(key, out object? value) || value == null)
            return false;
        return bool.TryParse(value.ToString(), out bool result) && result;
    }

    private static bool SamePartNo(string left, string right) =>
        string.Equals(CleanPartNo(left), CleanPartNo(right), StringComparison.Ordinal);

    private static string CleanPartNo(string value) =>
        System.Text.RegularExpressions.Regex.Replace(value ?? string.Empty, @"\s+", string.Empty)
            .ToUpperInvariant();

    private static string GetOption(string[] args, string name)
    {
        for (int i = 0; i < args.Length - 1; i++)
        {
            if (string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase))
                return args[i + 1];
        }
        return string.Empty;
    }

    private static bool HasOption(string[] args, string name)
    {
        return args != null && args.Any(a => string.Equals(a, name, StringComparison.OrdinalIgnoreCase));
    }

    private static void WriteResult(string resultPath, object result)
    {
        string json = JsonSerializer.Serialize(result, new JsonSerializerOptions
        {
            WriteIndented = false,
        });
        File.WriteAllText(resultPath, json);
    }
}
