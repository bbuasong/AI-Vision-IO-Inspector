using System.Diagnostics;
using System.Text.RegularExpressions;

namespace EpsonScanApi.Services;

/// <summary>
/// Invokes the one-shot x64 Windows OCR helper for the Latin part-number row.
/// It has no network or server dependency; the helper reads one local BMP and
/// writes one line to standard output.
/// </summary>
internal static class WindowsPartOcrClient
{
    private const int TimeoutMilliseconds = 15000;

    public static bool TryRecognize(string imagePath, out string partNo, out string reason)
    {
        partNo = string.Empty;
        reason = string.Empty;
        if (string.IsNullOrWhiteSpace(imagePath) || !File.Exists(imagePath))
        {
            reason = "Windows OCR용 부품번호 크롭이 없습니다.";
            return false;
        }

        string helper = Path.Combine(AppContext.BaseDirectory, "WindowsPartOcr.exe");
        if (!File.Exists(helper))
        {
            reason = "WindowsPartOcr.exe가 배포되어 있지 않습니다.";
            return false;
        }

        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = helper,
                Arguments = Quote(imagePath),
                WorkingDirectory = AppContext.BaseDirectory,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };
            using Process? process = Process.Start(startInfo);
            if (process == null)
            {
                reason = "Windows OCR 보조 프로그램을 시작하지 못했습니다.";
                return false;
            }

            string output = process.StandardOutput.ReadToEnd();
            string error = process.StandardError.ReadToEnd();
            if (!process.WaitForExit(TimeoutMilliseconds))
            {
                try { process.Kill(true); } catch { }
                reason = "Windows OCR 보조 프로그램 시간이 초과되었습니다.";
                return false;
            }

            partNo = ExtractPartNo(output);
            if (process.ExitCode == 0 && !string.IsNullOrWhiteSpace(partNo))
                return true;

            reason = string.IsNullOrWhiteSpace(error)
                ? "Windows OCR에서 유효한 부품번호를 찾지 못했습니다."
                : error.Trim();
            return false;
        }
        catch (Exception ex)
        {
            reason = ex.Message;
            return false;
        }
    }

    private static string ExtractPartNo(string value)
    {
        string normalized = Regex.Replace((value ?? string.Empty).ToUpperInvariant(), @"\s+", string.Empty);
        Match match = Regex.Match(
            normalized,
            @"(?=[A-Z0-9-]{5,})(?=[A-Z0-9-]*[A-Z])(?=[A-Z0-9-]*\d)[A-Z0-9][A-Z0-9-]{4,}");
        return match.Success ? Regex.Replace(match.Value, "-{2,}", "-") : string.Empty;
    }

    private static string Quote(string value) => "\"" + value.Replace("\"", "\\\"") + "\"";
}
