using System.Runtime.Versioning;
using System.Text.Json;
using EpsonScanApi.Models;
using EpsonScanApi.Services;
using Microsoft.AspNetCore.Mvc;

// 32비트 프로세스 메모리 보호: ImageSharp 기본 메모리 풀은 64비트 기준이라 커서,
// x86 프로세스에서 큰 스캔(2550x4650)을 반복하면 풀이 주소공간을 잠식해 OutOfMemory가 난다.
// 풀을 끄면(0) 버퍼를 보존하지 않고 매번 GC가 회수 → 32비트에서 누적 고갈을 방지.
SixLabors.ImageSharp.Configuration.Default.MemoryAllocator =
    SixLabors.ImageSharp.Memory.MemoryAllocator.Create(
        new SixLabors.ImageSharp.Memory.MemoryAllocatorOptions { MaximumPoolSizeMegabytes = 0 });

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c => c.SwaggerDoc("v2", new() { Title = "Epson Scan API", Version = "2.0.0" }));

// 서비스 DI 등록
builder.Services.AddSingleton<JobRegistry>();
builder.Services.AddSingleton<OcrEpson>();
builder.Services.AddSingleton<OcrTesseract>();
builder.Services.AddSingleton<OcrRapid>();     // RapidOCR part_no 보조 인식기(사이드카)
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

// 작업에 저장된 OCR 결과에서 part_no와 품질 신뢰도를 꺼낸다.
// j.Ocr은 인메모리면 Dictionary<string,object>, JSON 복원 후면 JsonElement.
static (string PartNo, double Quality) ReadPartNoFromJob(JobModel j)
{
    string pn = ""; double q = 0;
    switch (j.Ocr)
    {
        case Dictionary<string, object> d:
            if (d.TryGetValue("part_no", out var p) && p != null) pn = p.ToString() ?? "";
            if (d.TryGetValue("quality", out var qq) && qq is Dictionary<string, object> qd
                && qd.TryGetValue("confidence", out var c) && c != null)
                double.TryParse(c.ToString(), out q);
            break;
        case JsonElement je when je.ValueKind == JsonValueKind.Object:
            if (je.TryGetProperty("part_no", out var pe) && pe.ValueKind == JsonValueKind.String)
                pn = pe.GetString() ?? "";
            if (je.TryGetProperty("quality", out var qe) && qe.ValueKind == JsonValueKind.Object
                && qe.TryGetProperty("confidence", out var ce) && ce.ValueKind == JsonValueKind.Number)
                q = ce.GetDouble();
            break;
    }
    return (pn, q);
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
        // 적응형: 확신 낮으면(품질 불량 / part_no 비어있음 / 두 엔진 불일치) 재스캔 권장 신호
        bool curRescan1 = info.TryGetValue("needs_rescan", out var nr1) && nr1 is true;
        info["needs_rescan"] = curRescan1 || !isOk
            || !info.TryGetValue("part_no", out var pn1) || string.IsNullOrEmpty(pn1 as string);
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

// ── POST /vote ────────────────────────────────────────────────────────────────
// 다중 스캔 다수결: 같은 라벨을 여러 번(권장 3회) 다시 올려 스캔한 작업 ID들을 묶어
// part_no를 하나로 합의한다. 적응형 워크플로에서 "확신 낮은(needs_rescan)" 라벨만
// 추가로 1~2번 더 스캔한 뒤 호출하면 된다.
app.MapPost("/vote", ([FromBody] VoteRequest req, JobRegistry jobs) =>
{
    req ??= new VoteRequest();
    req.Normalize();
    if (req.JobIds.Count == 0)
        return Results.BadRequest(new { detail = "job_ids가 필요합니다 (같은 라벨의 스캔 작업 ID 2개 이상)." });

    var reads = new List<(string, double)>();
    var candidates = new List<object>();
    foreach (var id in req.JobIds)
    {
        var j = jobs.Get(id);
        if (j == null) return Results.NotFound(new { detail = $"작업을 찾을 수 없습니다: {id}" });
        var (pn, q) = ReadPartNoFromJob(j);
        reads.Add((pn, q));
        candidates.Add(new { job_id = id, part_no = pn, quality = q });
    }

    var r = PartNoVoter.Vote(reads);
    return Results.Ok(new
    {
        part_no      = r.PartNo,
        confidence   = r.Confidence,
        n            = r.N,
        needs_review = r.NeedsReview,
        candidates,
    });
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
        catch (OutOfMemoryException)
        {
            // 32비트 메모리 부족: 크롭을 건너뛰고 원본으로 OCR 진행(스캔을 500으로 죽이지 않음).
            GC.Collect(); GC.WaitForPendingFinalizers();
            jobs.Update(job.Id, j =>
            {
                j.Status = "card"; j.OcrSrcPath = j.ImagePath;   // 원본을 OCR 소스로
                j.CardLog = "cs: card skipped (OOM) — 원본으로 OCR";
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
        bool curRescan2 = info.TryGetValue("needs_rescan", out var nr2) && nr2 is true;
        info["needs_rescan"] = curRescan2 || !isOk
            || !info.TryGetValue("part_no", out var pn2) || string.IsNullOrEmpty(pn2 as string);
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

// 사이드카 예열: 서버 시작 직후 백그라운드로 RapidOCR 사이드카를 미리 기동·로딩한다.
// (첫 스캔이 사이드카 콜드스타트보다 먼저 실행돼 part_no가 비는 레이스 방지)
_ = Task.Run(() =>
{
    try { app.Services.GetRequiredService<OcrRapid>().Available(); } catch { }
});

// ── 서버 시작 ─────────────────────────────────────────────────────────────────
var host = app.Configuration["ScanApiHost"] ?? "127.0.0.1";
var port = int.Parse(app.Configuration["ScanApiPort"] ?? "8000");
app.Run($"http://{host}:{port}");
