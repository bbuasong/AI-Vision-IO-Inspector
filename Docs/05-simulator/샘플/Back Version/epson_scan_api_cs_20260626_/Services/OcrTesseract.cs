using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Tesseract;

namespace EpsonScanApi.Services;

public sealed class OcrTesseract : IDisposable
{
    private static readonly string[] TessExeCandidates =
    [
        @"C:\Program Files\Tesseract-OCR\tesseract.exe",
        @"C:\Program Files (x86)\Tesseract-OCR\tesseract.exe",
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                     @"Programs\Tesseract-OCR\tesseract.exe"),
    ];
    private static readonly string[] TessDataCandidates =
    [
        @"C:\Program Files\Tesseract-OCR\tessdata",
        @"C:\Program Files (x86)\Tesseract-OCR\tessdata",
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                     @"Programs\Tesseract-OCR\tessdata"),
    ];

    public List<string> AvailableLanguages()
    {
        var (dir, langs) = FindTessdata(new[] { "eng" });
        return langs.Order().ToList();
    }

    public Dictionary<string, object> ImageToSearchablePdf(string imagePath, string outPdf, string lang = "kor+eng")
    {
        var (useLang, tessDataDir) = ResolveLang(lang);
        if (string.IsNullOrEmpty(useLang))
            throw new OcrError("사용 가능한 언어 데이터가 없습니다. tessdata에 eng/kor.traineddata를 넣으세요.");

        string tessExe = FindTesseractExe() ?? "tesseract";

        // tesseract CLI로 PDF 생성 (내장 PDF 렌더러)
        string outBase = Path.Combine(Path.GetDirectoryName(outPdf)!, Path.GetFileNameWithoutExtension(outPdf));
        var psi = new System.Diagnostics.ProcessStartInfo(tessExe)
        {
            ArgumentList = { imagePath, outBase, "-l", useLang, "pdf" },
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError  = true,
            CreateNoWindow = true,
        };
        if (tessDataDir != null)
            psi.EnvironmentVariables["TESSDATA_PREFIX"] = tessDataDir;

        using var proc = System.Diagnostics.Process.Start(psi)!;
        proc.WaitForExit(60_000);

        string expected = outBase + ".pdf";
        if (!File.Exists(expected))
            throw new OcrError($"Tesseract PDF 생성 실패 (exit={proc.ExitCode})");
        if (expected != outPdf) File.Move(expected, outPdf, overwrite: true);

        return new Dictionary<string, object>
        {
            ["out_pdf"] = outPdf, ["lang"] = useLang, ["pages"] = 1, ["engine"] = "Tesseract",
        };
    }

    private (string UseLang, string? Dir) ResolveLang(string requested)
    {
        var want = requested.Split('+');
        var (dir, have) = FindTessdata(want);
        var use = want.Where(l => have.Contains(l)).ToList();
        if (use.Count == 0 && have.Contains("eng")) use.Add("eng");
        return (string.Join("+", use), dir);
    }

    private (string? Dir, HashSet<string> Langs) FindTessdata(string[] wantLangs)
    {
        var cands = new List<string>();
        if (Environment.GetEnvironmentVariable("TESSDATA_PREFIX") is string ep) cands.Add(ep);
        cands.AddRange(TessDataCandidates);

        (string? dir, HashSet<string> langs) best = default;
        foreach (var d in cands)
        {
            if (!Directory.Exists(d)) continue;
            var langs = Directory.EnumerateFiles(d, "*.traineddata")
                                 .Select(f => Path.GetFileNameWithoutExtension(f)!)
                                 .ToHashSet();
            if (langs.Count == 0) continue;
            if (best.dir == null) best = (d, langs);
            if (wantLangs.Any(l => langs.Contains(l))) return (d, langs);
        }
        return (best.dir, best.langs ?? new());
    }

    private static string? FindTesseractExe()
    {
        foreach (var p in TessExeCandidates)
            if (File.Exists(p)) return p;
        return null;
    }

    public void Dispose() { }
}
