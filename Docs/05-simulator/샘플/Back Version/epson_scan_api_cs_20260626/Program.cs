using System.Runtime.Versioning;
using EpsonScanApi.Models;
using EpsonScanApi.Services;
using Microsoft.AspNetCore.Mvc;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c => c.SwaggerDoc("v2", new() { Title = "Epson Scan API", Version = "2.0.0" }));

// 서비스 DI 등록
builder.Services.AddSingleton<JobRegistry>();
builder.Services.AddSingleton<OcrEpson>();
builder.Services.AddSingleton<OcrTesseract>();
builder.Services.AddSingleton<OcrEngine>();

var app = builder.Build();
app.UseSwagger();
app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v2/swagger.json", "Epson Scan API v2"));

// ── 출력 디렉토리 / Jobs 설정 ────────────────────────────────────────────────
var outputDir = app.Configuration["ScanOutputDir"] ?? @"D:\epson_scans";
Directory.CreateDirectory(outputDir);
app.Services.GetRequiredService<JobRegistry>().Configure(Path.Combine(outputDir, "jobs.json"));

Console.WriteLine($"[Scan API] 출력 디렉토리: {outputDir}");
Console.WriteLine($"[Scan API] 설정 파일:     appsettings.json → ScanOutputDir 키로 변경 가능");
Console.WriteLine($"[Scan API] 엔진 상태:     GET /health 로 확인");

// ── 유틸 ─────────────────────────────────────────────────────────────────────
static string? OcrSourcePath(JobModel j)
{
    var p = j.OcrSrcPath;
    if (p != null && File.Exists(p)) return p;
    return j.ImagePath;
}

// ── /health ──────────────────────────────────────────────────────────────────
app.MapGet("/health", (OcrEngine ocr) => Results.Ok(new
{
    status = "ok", output_dir = outputDir,
    engines = ocr.EngineStatus(),
})).WithTags("System");

// ── /scanners ────────────────────────────────────────────────────────────────
app.MapGet("/scanners", () =>
{
    if (!OperatingSystem.IsWindows())
        return Results.Problem("WIA는 Windows 전용입니다.", statusCode: 500);
    try { return Results.Ok(new { scanners = ScannerWia.ListScanners() }); }
    catch (Exception ex) { return Results.Problem($"스캐너 조회 실패: {ex.Message}", statusCode: 500); }
}).WithTags("Scanner");

// ── /languages ───────────────────────────────────────────────────────────────
app.MapGet("/languages", (OcrEngine ocr) => Results.Ok(new
{
    languages = ocr.AvailableLanguages(),
    engines   = ocr.EngineStatus(),
})).WithTags("OCR");

// ── POST /scan ───────────────────────────────────────────────────────────────
app.MapPost("/scan", ([FromBody] ScanRequest req, JobRegistry jobs) =>
{
    if (!OperatingSystem.IsWindows())
        return Results.Problem("WIA는 Windows 전용입니다.", statusCode: 500);

    req.Normalize();
    string? dev = req.DeviceId;

    var job = jobs.Create("scanning", req);
    string ext = req.Fmt == "jpeg" ? "jpg" : req.Fmt;
    string imgPath = Path.Combine(outputDir, $"{job.Id}_raw.{ext}");

    try
    {
        ScannerWia.Scan(imgPath, dev, req.Dpi, req.Mode, req.Source, req.Fmt);
    }
    catch (ScannerError ex)
    {
        jobs.Update(job.Id, j => { j.Status = "error"; j.Error = ex.Message; });
        return Results.Conflict(new { detail = ex.Message });
    }
    catch (Exception ex)
    {
        jobs.Update(job.Id, j => { j.Status = "error"; j.Error = ex.Message; });
        return Results.Problem($"스캔 실패: {ex.Message}", statusCode: 500);
    }
    return Results.Ok(jobs.Update(job.Id, j => { j.Status = "scanned"; j.ImagePath = imgPath; }));
}).WithTags("Scanner");

// ── GET /jobs ─────────────────────────────────────────────────────────────────
app.MapGet("/jobs", (JobRegistry jobs) =>
{
    var all = jobs.ListAll();
    return Results.Ok(new { count = all.Count, jobs = all });
}).WithTags("Jobs");

app.MapGet("/jobs/{jid}", (string jid, JobRegistry jobs) =>
{
    var j = jobs.Get(jid);
    return j != null ? Results.Ok(j) : Results.NotFound(new { detail = "작업을 찾을 수 없습니다." });
}).WithTags("Jobs");

app.MapDelete("/jobs/{jid}", (string jid, JobRegistry jobs) =>
    Results.Ok(new { deleted = jobs.Delete(jid) })).WithTags("Jobs");

// ── POST /jobs/{jid}/preprocess ──────────────────────────────────────────────
app.MapPost("/jobs/{jid}/preprocess", (string jid, [FromBody] PreprocessRequest opts, JobRegistry jobs) =>
{
    var j = jobs.Get(jid);
    if (j == null) return Results.NotFound(new { detail = "작업을 찾을 수 없습니다." });
    string? src = OcrSourcePath(j);
    if (src == null || !File.Exists(src)) return Results.Conflict(new { detail = "스캔 이미지가 없습니다." });

    opts.Normalize();
    string outPath = Path.Combine(outputDir, $"{jid}_proc.png");
    try { Preprocessor.ProcessFile(src, outPath, opts); }
    catch (Exception ex) { return Results.Problem($"전처리 실패: {ex.Message}", statusCode: 500); }

    return Results.Ok(jobs.Update(jid, j2 =>
    {
        j2.Status = "preprocessed"; j2.ProcessedPath = outPath; j2.OcrSrcPath = outPath;
    }));
}).WithTags("Processing");

// ── POST /jobs/{jid}/redact ──────────────────────────────────────────────────
app.MapPost("/jobs/{jid}/redact", (string jid, [FromBody] RedactRequest req, JobRegistry jobs) =>
{
    var j = jobs.Get(jid);
    if (j == null) return Results.NotFound(new { detail = "작업을 찾을 수 없습니다." });
    string? src = OcrSourcePath(j);
    if (src == null || !File.Exists(src)) return Results.Conflict(new { detail = "원본 이미지가 없습니다." });

    req.Normalize();
    string outPath = Path.Combine(outputDir, $"{jid}_redacted.png");
    try
    {
        string saved = Redactor.CoverRegions(src, outPath, req.Rects, req.Fill);
        return Results.Ok(jobs.Update(jid, j2 =>
        {
            j2.Status = "redacted"; j2.RedactedPath = saved; j2.OcrSrcPath = saved;
        }));
    }
    catch (Exception ex) { return Results.Problem($"영역 덮기 실패: {ex.Message}", statusCode: 500); }
}).WithTags("Processing");

// ── POST /jobs/{jid}/extract-card ────────────────────────────────────────────
app.MapPost("/jobs/{jid}/extract-card", (string jid, [FromBody] CardRequest req, JobRegistry jobs) =>
{
    var j = jobs.Get(jid);
    if (j == null) return Results.NotFound(new { detail = "작업을 찾을 수 없습니다." });
    if (j.ImagePath == null || !File.Exists(j.ImagePath))
        return Results.Conflict(new { detail = "스캔 이미지가 없습니다." });

    req.Normalize();
    string outPath = Path.Combine(outputDir, $"{jid}_card.png");
    try
    {
        var (saved, info) = CardExtractor.ExtractLabel(j.ImagePath, outPath, req.Dpi);
        return Results.Ok(jobs.Update(jid, j2 =>
        {
            j2.Status = "card"; j2.CardPath = saved; j2.OcrSrcPath = saved;
            j2.CardLog = $"cs: angle={info["angle"]}, cropped={info["cropped"]}";
        }));
    }
    catch (Exception ex) { return Results.Problem($"카드 추출 실패: {ex.Message}", statusCode: 500); }
}).WithTags("Processing");

// ── POST /jobs/{jid}/pdf ──────────────────────────────────────────────────────
app.MapPost("/jobs/{jid}/pdf", (string jid, [FromBody] PdfRequest req, JobRegistry jobs, OcrEngine ocr) =>
{
    var j = jobs.Get(jid);
    if (j == null) return Results.NotFound(new { detail = "작업을 찾을 수 없습니다." });
    string? src = OcrSourcePath(j);
    if (src == null || !File.Exists(src)) return Results.Conflict(new { detail = "OCR할 이미지가 없습니다." });

    req.Normalize();
    string outPdf = Path.Combine(outputDir, $"{jid}.pdf");
    try
    {
        var info   = ocr.ImageToSearchablePdf(src, outPdf, req.Lang, req.Engine);
        var quality = info.TryGetValue("quality", out var q) ? q as Dictionary<string, object> : null;
        bool isOk   = quality == null || (quality.TryGetValue("ok", out var ok) && ok is true);
        string status = isOk ? "done" : "low_quality";
        return Results.Ok(jobs.Update(jid, j2 =>
        {
            j2.Status = status; j2.PdfPath = outPdf; j2.Ocr = info;
        }));
    }
    catch (OcrError ex)
    {
        jobs.Update(jid, j2 => { j2.Status = "error"; j2.Error = ex.Message; });
        return Results.Conflict(new { detail = ex.Message });
    }
    catch (Exception ex)
    {
        jobs.Update(jid, j2 => { j2.Status = "error"; j2.Error = ex.Message; });
        return Results.Problem($"OCR/PDF 실패: {ex.Message}", statusCode: 500);
    }
}).WithTags("OCR");

// ── GET /jobs/{jid}/fields ────────────────────────────────────────────────────
app.MapGet("/jobs/{jid}/fields", (string jid, JobRegistry jobs) =>
{
    var j = jobs.Get(jid);
    if (j == null) return Results.NotFound(new { detail = "작업을 찾을 수 없습니다." });
    var ocr = j.Ocr as Dictionary<string, object> ?? new();
    ocr.TryGetValue("fields", out var fields);
    ocr.TryGetValue("lines",  out var lines);
    ocr.TryGetValue("text",   out var text);
    return Results.Ok(new { fields, lines, text });
}).WithTags("OCR");

// ── GET /jobs/{jid}/download/{kind} ──────────────────────────────────────────
app.MapGet("/jobs/{jid}/download/{kind}", (string jid, string kind, JobRegistry jobs) =>
{
    var j = jobs.Get(jid);
    if (j == null) return Results.NotFound(new { detail = "작업을 찾을 수 없습니다." });
    string? path = kind switch
    {
        "image"    => j.ImagePath,
        "processed"=> j.ProcessedPath,
        "card"     => j.CardPath,
        "redacted" => j.RedactedPath,
        "pdf"      => j.PdfPath,
        _          => null,
    };
    if (path == null) return Results.BadRequest(new { detail = "kind는 image|processed|card|redacted|pdf 중 하나여야 합니다." });
    if (!File.Exists(path)) return Results.NotFound(new { detail = "파일이 없습니다." });
    return Results.File(path, contentType: path.EndsWith(".pdf") ? "application/pdf" : "image/png",
                        fileDownloadName: Path.GetFileName(path));
}).WithTags("Jobs");

// ── POST /scan-to-pdf ─────────────────────────────────────────────────────────
app.MapPost("/scan-to-pdf", async ([Microsoft.AspNetCore.Mvc.FromBody] ScanToPdfRequest? body, JobRegistry jobs, OcrEngine ocr) =>
{
    var req = body ?? new();
    req.Normalize();

    // 순차적으로 각 단계 실행 (엔드포인트 재활용)
    if (!OperatingSystem.IsWindows())
        return Results.Problem("WIA는 Windows 전용입니다.", statusCode: 500);

    string? dev = req.Scan.DeviceId;
    var job = jobs.Create("scanning", req.Scan);
    string ext = req.Scan.Fmt == "jpeg" ? "jpg" : req.Scan.Fmt;
    string imgPath = Path.Combine(outputDir, $"{job.Id}_raw.{ext}");

    try { ScannerWia.Scan(imgPath, dev, req.Scan.Dpi, req.Scan.Mode, req.Scan.Source, req.Scan.Fmt); }
    catch (ScannerError ex)
    {
        jobs.Update(job.Id, j => { j.Status = "error"; j.Error = ex.Message; });
        return Results.Conflict(new { detail = ex.Message });
    }
    jobs.Update(job.Id, j => { j.Status = "scanned"; j.ImagePath = imgPath; });

    // 가공 단계는 선형 누적: 각 단계가 직전 결과 위에 적용 (Python scan_to_pdf와 동일).
    // Python은 단계 실패 시 예외를 던져 전체를 중단하므로, C#도 동일하게 오류를 전파한다.
    if (req.Card != null)
    {
        // extract-card: 항상 원본(image_path) 기준. 미검출은 정상(크롭만 생략), 실제 오류만 전파.
        string cardOut = Path.Combine(outputDir, $"{job.Id}_card.png");
        try
        {
            var (saved, info) = CardExtractor.ExtractLabel(imgPath, cardOut, req.Card.Dpi);
            jobs.Update(job.Id, j =>
            {
                j.Status = "card"; j.CardPath = saved; j.OcrSrcPath = saved;
                j.CardLog = $"cs: angle={info["angle"]}, cropped={info["cropped"]}";
            });
        }
        catch (Exception ex)
        {
            jobs.Update(job.Id, j => { j.Status = "error"; j.Error = ex.Message; });
            return Results.Problem($"카드 추출 실패: {ex.Message}", statusCode: 500);
        }
    }

    // Epson(OmniPage) 엔진은 자체 전처리(kRecPreprocessImg: 이진화·잡티제거·미세 deskew)를 하므로,
    // 수동 전처리를 겹쳐 걸면 오히려 인식이 나빠진다(특히 denoise가 잔글씨를 뭉갬).
    // → engine=auto/epson이면 생략하고, tesseract일 때만 적용. (수동 전처리를 꼭 쓰려면 /preprocess 단독 호출)
    string engineSel = (req.Pdf.Engine ?? "auto").Trim().ToLower();
    if (req.Preprocess != null && engineSel == "tesseract")
    {
        var cur = jobs.Get(job.Id)!;
        string? src = OcrSourcePath(cur);
        if (src == null || !File.Exists(src))
            return Results.Conflict(new { detail = "스캔 이미지가 없습니다." });
        string ppOut = Path.Combine(outputDir, $"{job.Id}_proc.png");
        try
        {
            Preprocessor.ProcessFile(src, ppOut, req.Preprocess);
            jobs.Update(job.Id, j => { j.Status = "preprocessed"; j.ProcessedPath = ppOut; j.OcrSrcPath = ppOut; });
        }
        catch (Exception ex)
        {
            return Results.Problem($"전처리 실패: {ex.Message}", statusCode: 500);
        }
    }

    if (req.Redact != null)
    {
        var cur = jobs.Get(job.Id)!;
        string? src = OcrSourcePath(cur);
        if (src == null || !File.Exists(src))
            return Results.Conflict(new { detail = "원본 이미지가 없습니다." });
        string rdOut = Path.Combine(outputDir, $"{job.Id}_redacted.png");
        try
        {
            string saved = Redactor.CoverRegions(src, rdOut, req.Redact.Rects, req.Redact.Fill);
            jobs.Update(job.Id, j => { j.Status = "redacted"; j.RedactedPath = saved; j.OcrSrcPath = saved; });
        }
        catch (Exception ex)
        {
            return Results.Problem($"영역 덮기 실패: {ex.Message}", statusCode: 500);
        }
    }

    var latestJob = jobs.Get(job.Id)!;
    string? ocrSrc = OcrSourcePath(latestJob);
    if (ocrSrc == null || !File.Exists(ocrSrc))
        return Results.Conflict(new { detail = "OCR할 이미지가 없습니다." });

    string outPdf = Path.Combine(outputDir, $"{job.Id}.pdf");
    try
    {
        var info    = ocr.ImageToSearchablePdf(ocrSrc, outPdf, req.Pdf.Lang, req.Pdf.Engine);
        var quality = info.TryGetValue("quality", out var q) ? q as Dictionary<string, object> : null;
        bool isOk   = quality == null || (quality.TryGetValue("ok", out var ok2) && ok2 is true);
        return Results.Ok(jobs.Update(job.Id, j =>
        {
            j.Status = isOk ? "done" : "low_quality"; j.PdfPath = outPdf; j.Ocr = info;
        }));
    }
    catch (OcrError ex)
    {
        jobs.Update(job.Id, j => { j.Status = "error"; j.Error = ex.Message; });
        return Results.Conflict(new { detail = ex.Message });
    }
    catch (Exception ex)
    {
        jobs.Update(job.Id, j => { j.Status = "error"; j.Error = ex.Message; });
        return Results.Problem($"OCR/PDF 실패: {ex.Message}", statusCode: 500);
    }
}).WithTags("Scanner");

// ── 서버 시작 ─────────────────────────────────────────────────────────────────
var host = app.Configuration["ScanApiHost"] ?? "127.0.0.1";
var port = int.Parse(app.Configuration["ScanApiPort"] ?? "8000");
app.Run($"http://{host}:{port}");
