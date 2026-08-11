namespace EpsonScanApi.Services;

public class OcrEngine(OcrEpson epson, OcrTesseract tesseract)
{
    public Dictionary<string, object> EngineStatus() => new()
    {
        ["epson"] = new Dictionary<string, object?>
        {
            ["available"] = epson.IsAvailable(out string? reason),
            ["reason"]    = reason,
        },
        ["tesseract_languages"] = tesseract.AvailableLanguages(),
    };

    public Dictionary<string, object> AvailableLanguages()
    {
        bool epOk = epson.IsAvailable(out _);
        return new()
        {
            ["epson"]      = epOk ? new[] { "kor" } : Array.Empty<string>(),
            ["tesseract"]  = tesseract.AvailableLanguages(),
        };
    }

    public Dictionary<string, object> ImageToSearchablePdf(string imagePath, string outPdf,
                                                            string lang = "kor+eng", string engine = "auto")
    {
        engine = (engine ?? "auto").ToLower();
        if (engine is "auto" or "epson")
        {
            try
            {
                return epson.ImageToSearchablePdf(imagePath, outPdf, lang);
            }
            catch (EpsonOcrError ex)
            {
                if (engine == "epson")
                    throw new OcrError($"Epson 엔진 실패: {ex.Message}");
                var info = tesseract.ImageToSearchablePdf(imagePath, outPdf, lang);
                info["engine"] = $"Tesseract (Epson 폴백: {ex.Message})";
                return info;
            }
        }
        var r = tesseract.ImageToSearchablePdf(imagePath, outPdf, lang);
        r.TryAdd("engine", "Tesseract");
        return r;
    }
}

public class OcrError(string msg) : Exception(msg);
