using System.Diagnostics;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;

namespace EpsonScanApi.Services;

/// <summary>
/// RapidOCR 부품번호 보조 인식기 — 파이썬 사이드카(rapid_sidecar.py)를 HTTP로 호출.
/// C# 서버는 Epson DLL 때문에 x86이고 .NET용 onnxruntime은 x86 네이티브가 없어 인프로세스 불가.
/// → RapidOCR(파이썬)을 별도 프로세스로 띄워 part_no만 받아온다.
///
/// 납품(고객 PC에 파이썬 미설치) 대비: 파이썬 런타임을 앱 폴더에 동봉하고,
/// 기본값으로 **앱 폴더 기준 상대경로**의 번들 파이썬을 자동 기동한다(설치 위치 무관).
///   - 파이썬:   {앱폴더}\pyruntime\python.exe   (appsettings RapidPythonExe로 덮어쓰기 가능)
///   - 스크립트: {앱폴더}\rapid_sidecar.py        (appsettings RapidSidecarScript로 덮어쓰기 가능)
/// 사이드카가 없거나 실패하면 ("", 0) 반환 → Epson 결과로 폴백(무해).
/// </summary>
public sealed class OcrRapid
{
    private readonly string _url;
    private readonly string _exe;       // PyInstaller로 구운 단일 exe (가장 쉬운 배포)
    private readonly string _python;    // 또는 번들 파이썬 + 스크립트
    private readonly string _script;
    private readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(60) };
    private readonly object _lock = new();
    private bool _launchTried;

    public OcrRapid(IConfiguration cfg)
    {
        string baseDir = AppContext.BaseDirectory;
        _url    = (cfg["RapidSidecarUrl"] ?? "http://127.0.0.1:8011").TrimEnd('/');
        _exe    = Resolve(cfg["RapidSidecarExe"],    baseDir, "rapid_sidecar.exe");
        _python = Resolve(cfg["RapidPythonExe"],     baseDir, Path.Combine("pyruntime", "python.exe"));
        _script = Resolve(cfg["RapidSidecarScript"], baseDir, "rapid_sidecar.py");
    }

    // 설정값이 절대경로면 그대로, 상대경로면 앱폴더 기준, 없으면 기본(앱폴더\fallback).
    private static string Resolve(string? configured, string baseDir, string fallback)
    {
        if (!string.IsNullOrWhiteSpace(configured))
            return Path.IsPathRooted(configured) ? configured : Path.Combine(baseDir, configured);
        return Path.Combine(baseDir, fallback);
    }

    public (string PartNo, string Sub, double Conf) ReadPartNo(string imagePath)
    {
        var r = PostOnce(imagePath);
        if (r != null) return r.Value;
        EnsureLaunched();                 // 첫 실패 시 1회 자동 기동
        r = PostOnce(imagePath);
        return r ?? ("", "", 0.0);
    }

    public bool Available()
    {
        if (HealthOk()) return true;
        EnsureLaunched();
        return HealthOk();
    }

    // /health 진단용: C#이 어떤 경로에서 사이드카를 찾는지·존재하는지·연결되는지 그대로 노출
    public Dictionary<string, object?> Diagnostics() => new()
    {
        ["available"]     = Available(),
        ["url"]           = _url,
        ["exe"]           = _exe,
        ["exe_exists"]    = File.Exists(_exe),
        ["python"]        = _python,
        ["python_exists"] = File.Exists(_python),
        ["script"]        = _script,
        ["script_exists"] = File.Exists(_script),
    };

    private bool HealthOk()
    {
        try
        {
            var resp = _http.GetAsync($"{_url}/health").GetAwaiter().GetResult();
            if (!resp.IsSuccessStatusCode) return false;
            using var s = resp.Content.ReadAsStream();
            using var doc = JsonDocument.Parse(s);
            return doc.RootElement.TryGetProperty("available", out var a)
                   && a.ValueKind == JsonValueKind.True;
        }
        catch { return false; }
    }

    private (string, string, double)? PostOnce(string imagePath)
    {
        try
        {
            // 주의: PostAsJsonAsync는 chunked 전송이라 사이드카(파이썬 stdlib)가 Content-Length로
            // 본문을 못 읽는다. 길이를 명시한 StringContent로 보내야 한다.
            string json = JsonSerializer.Serialize(new { image_path = imagePath });
            using var body = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
            var resp = _http.PostAsync($"{_url}/part_no", body).GetAwaiter().GetResult();
            if (!resp.IsSuccessStatusCode) return null;
            using var s = resp.Content.ReadAsStream();
            using var doc = JsonDocument.Parse(s);
            var root = doc.RootElement;
            string pn  = root.TryGetProperty("part_no", out var p) ? (p.GetString() ?? "") : "";
            string sub = root.TryGetProperty("part_no_sub", out var su) ? (su.GetString() ?? "") : "";
            double cf  = root.TryGetProperty("conf", out var c) && c.ValueKind == JsonValueKind.Number
                        ? c.GetDouble() : 0.0;
            return (pn, sub, cf);
        }
        catch { return null; }
    }

    private void EnsureLaunched()
    {
        lock (_lock)
        {
            if (_launchTried) return;
            _launchTried = true;

            // 1순위: PyInstaller 단일 exe (배포 시 파이썬 불필요). 2순위: 번들 파이썬 + 스크립트.
            ProcessStartInfo psi;
            if (File.Exists(_exe))
                psi = new ProcessStartInfo(_exe) { WorkingDirectory = Path.GetDirectoryName(_exe)! };
            else if (File.Exists(_python) && File.Exists(_script))
                psi = new ProcessStartInfo(_python, $"\"{_script}\"") { WorkingDirectory = Path.GetDirectoryName(_script)! };
            else
                return;

            try
            {
                psi.UseShellExecute = false;
                psi.CreateNoWindow  = true;
                Process.Start(psi);
                for (int i = 0; i < 40; i++)   // 모델 로드 대기(최대 40초)
                {
                    Thread.Sleep(1000);
                    if (HealthReachable()) break;
                }
            }
            catch { }
        }
    }

    // 서버가 떠서 응답하는지(available 무관) — 기동 대기용
    private bool HealthReachable()
    {
        try { return _http.GetAsync($"{_url}/health").GetAwaiter().GetResult().IsSuccessStatusCode; }
        catch { return false; }
    }
}
