using System.ComponentModel;
using System.Text.Json.Serialization;

namespace EpsonScanApi.Models;

// 공통 정규화 헬퍼: Swagger "Try it out"의 플레이스홀더("string", 0 등)나
// 잘못 입력된 값을 문서화된 기본값으로 보정한다.
internal static class Norm
{
    public static bool IsPlaceholder(string? v)
    {
        if (string.IsNullOrWhiteSpace(v)) return true;
        return v.Trim().ToLowerInvariant() is "string" or "null";
    }

    // 허용된 값 집합 안에 있으면 소문자로 정규화, 아니면 기본값.
    public static string Pick(string? v, string[] allowed, string def)
    {
        if (v == null) return def;
        var t = v.Trim().ToLowerInvariant();
        return allowed.Contains(t) ? t : def;
    }
}

public class ScanRequest
{
    [JsonPropertyName("device_id")]  public string? DeviceId { get; set; }
    [JsonPropertyName("dpi")][DefaultValue(300)]        public int Dpi { get; set; } = 300;
    [JsonPropertyName("mode")][DefaultValue("gray")]    public string Mode { get; set; } = "gray";      // color|gray|bw
    [JsonPropertyName("source")][DefaultValue("flatbed")] public string Source { get; set; } = "flatbed"; // flatbed|feeder
    [JsonPropertyName("fmt")][DefaultValue("bmp")]      public string Fmt { get; set; } = "bmp";        // bmp|png|jpeg

    public void Normalize()
    {
        if (Norm.IsPlaceholder(DeviceId)) DeviceId = null;
        if (Dpi <= 0) Dpi = 300;
        Mode = Norm.Pick(Mode, new[] { "color", "gray", "bw" }, "gray");
        Source = Norm.Pick(Source, new[] { "flatbed", "feeder" }, "flatbed");
        var fmt = Norm.Pick(Fmt, new[] { "bmp", "png", "jpeg", "jpg" }, "bmp");
        Fmt = fmt == "jpg" ? "jpeg" : fmt;
    }
}

public class PreprocessRequest
{
    [JsonPropertyName("grayscale")][DefaultValue(false)]    public bool Grayscale { get; set; }
    [JsonPropertyName("autocontrast")][DefaultValue(false)] public bool Autocontrast { get; set; }
    [JsonPropertyName("binarize")][DefaultValue("none")]    public string Binarize { get; set; } = "none"; // none|otsu|fixed
    [JsonPropertyName("threshold")][DefaultValue(160)]      public int Threshold { get; set; } = 160;
    [JsonPropertyName("deskew")][DefaultValue(false)]       public bool Deskew { get; set; }
    [JsonPropertyName("rotate")][DefaultValue(0)]           public int Rotate { get; set; }
    [JsonPropertyName("resize_maxdim")][DefaultValue(0)]    public int ResizeMaxdim { get; set; }
    [JsonPropertyName("denoise")][DefaultValue(false)]      public bool Denoise { get; set; }
    [JsonPropertyName("border_crop")][DefaultValue(0)]      public int BorderCrop { get; set; }

    public void Normalize()
    {
        // 핵심: 알 수 없는 값("string" 등)이 오면 이진화하지 않음(none).
        Binarize = Norm.Pick(Binarize, new[] { "none", "otsu", "fixed" }, "none");
        Threshold = Math.Clamp(Threshold, 0, 255);
        if (Binarize == "fixed" && Threshold <= 0) Threshold = 160; // fixed인데 0이면 전부 검정 → 기본값
        if (ResizeMaxdim < 0) ResizeMaxdim = 0;
        if (BorderCrop < 0) BorderCrop = 0;
    }
}

public class PdfRequest
{
    [JsonPropertyName("lang")][DefaultValue("kor+eng")]  public string Lang { get; set; } = "kor+eng";
    [JsonPropertyName("use_processed")][DefaultValue(true)] public bool UseProcessed { get; set; } = true;
    [JsonPropertyName("engine")][DefaultValue("auto")]   public string Engine { get; set; } = "auto"; // auto|epson|tesseract

    public void Normalize()
    {
        if (Norm.IsPlaceholder(Lang)) Lang = "kor+eng";
        Engine = Norm.Pick(Engine, new[] { "auto", "epson", "tesseract" }, "auto");
    }
}

public class RedactRequest
{
    [JsonPropertyName("rects")] public List<int[]> Rects { get; set; } = new(); // [[x,y,w,h],...]
    [JsonPropertyName("fill")][DefaultValue("white")]  public string Fill { get; set; } = "white";     // white|black

    public void Normalize()
    {
        Fill = Norm.Pick(Fill, new[] { "white", "black" }, "white");
        // 잘못된 rect([0] 같은 것) 제거: 정확히 4개 값(x,y,w,h)인 것만 유지
        Rects = (Rects ?? new()).Where(r => r != null && r.Length >= 4).ToList();
    }
}

public class CardRequest
{
    [JsonPropertyName("dpi")][DefaultValue(300)]   public int Dpi { get; set; } = 300;
    [JsonPropertyName("debug")][DefaultValue(false)] public bool Debug { get; set; }

    public void Normalize()
    {
        if (Dpi <= 0) Dpi = 300;
    }
}

public class ScanToPdfRequest
{
    [JsonPropertyName("scan")]       public ScanRequest Scan { get; set; } = new();
    [JsonPropertyName("card")]       public CardRequest? Card { get; set; } = new();
    [JsonPropertyName("preprocess")] public PreprocessRequest? Preprocess { get; set; }
    [JsonPropertyName("redact")]     public RedactRequest? Redact { get; set; }
    [JsonPropertyName("pdf")]        public PdfRequest Pdf { get; set; } = new();

    public void Normalize()
    {
        Scan ??= new();
        Scan.Normalize();
        Card?.Normalize();
        Preprocess?.Normalize();
        Redact?.Normalize();
        Pdf ??= new();
        Pdf.Normalize();
    }
}
