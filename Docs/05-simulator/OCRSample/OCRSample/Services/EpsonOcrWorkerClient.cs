using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using OCRSample.Models;

namespace OCRSample.Services
{
    /// <summary>
    /// x86 Epson OCR 작업자를 자식 프로세스로 실행합니다.
    /// HTTP 포트나 네트워크 연결을 사용하지 않습니다.
    /// </summary>
    public sealed class EpsonOcrWorkerClient
    {
        private const int OcrTimeoutMilliseconds = 120000;
        private readonly JavaScriptSerializer _json = new JavaScriptSerializer();

        public Task<EpsonOcrResult> RecognizeAsync(
            string workerPath,
            string imagePath,
            string language)
        {
            return Task.Factory.StartNew(
                delegate { return Recognize(workerPath, imagePath, language); },
                CancellationToken.None,
                TaskCreationOptions.LongRunning,
                TaskScheduler.Default);
        }

        private EpsonOcrResult Recognize(string workerPath, string imagePath, string language)
        {
            if (string.IsNullOrWhiteSpace(workerPath) || !File.Exists(workerPath))
            {
                throw new FileNotFoundException(
                    "로컬 Epson OCR 작업자를 찾을 수 없습니다.",
                    workerPath);
            }

            if (string.IsNullOrWhiteSpace(imagePath) || !File.Exists(imagePath))
            {
                throw new FileNotFoundException("OCR할 스캔 이미지를 찾을 수 없습니다.", imagePath);
            }

            string resultPath = Path.Combine(
                Path.GetDirectoryName(imagePath),
                Path.GetFileNameWithoutExtension(imagePath) + ".ocr.json");

            string arguments =
                "--ocr-file " + Quote(imagePath) +
                " --ocr-result " + Quote(resultPath) +
                " --lang " + Quote(string.IsNullOrWhiteSpace(language) ? "kor+eng" : language);

            string executablePath = workerPath;
            string workerArguments = arguments;
            string workerDirectory = Path.GetDirectoryName(workerPath);
            string workerAssemblyPath = Path.ChangeExtension(workerPath, ".dll");
            string x86DotnetPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
                "dotnet",
                "dotnet.exe");

            // The WPF application is x64 but Epson's Kernel API worker is
            // x86.  On PCs that have both runtimes, launching the x86 apphost
            // directly can resolve the x64 hostfxr and fail with 0x800700C1.
            // Running its DLL through the explicit x86 dotnet host avoids that
            // architecture ambiguity.  Keep the EXE fallback for a future
            // self-contained worker deployment.
            if (File.Exists(x86DotnetPath) && File.Exists(workerAssemblyPath))
            {
                executablePath = x86DotnetPath;
                workerArguments = Quote(workerAssemblyPath) + " " + arguments;
            }

            var startInfo = new ProcessStartInfo
            {
                FileName = executablePath,
                Arguments = workerArguments,
                WorkingDirectory = workerDirectory,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            using (Process process = Process.Start(startInfo))
            {
                if (process == null)
                {
                    throw new InvalidOperationException("Epson OCR 작업자를 시작하지 못했습니다.");
                }

                Task<string> standardOutput = process.StandardOutput.ReadToEndAsync();
                Task<string> standardError = process.StandardError.ReadToEndAsync();

                if (!process.WaitForExit(OcrTimeoutMilliseconds))
                {
                    try { process.Kill(); } catch { }
                    throw new TimeoutException("Epson OCR 작업이 120초 안에 끝나지 않았습니다.");
                }

                Task.WaitAll(standardOutput, standardError);

                if (!File.Exists(resultPath))
                {
                    throw new InvalidOperationException(
                        "Epson OCR 작업 결과 파일이 생성되지 않았습니다. 종료 코드: " +
                        process.ExitCode + Environment.NewLine +
                        BuildDiagnostic(standardError.Result, standardOutput.Result));
                }

                Dictionary<string, object> root = Deserialize(File.ReadAllText(resultPath));
                if (!GetBool(root, "success"))
                {
                    throw new InvalidOperationException(
                        GetString(root, "error") ?? "Epson OCR 작업이 실패했습니다.");
                }

                Dictionary<string, object> ocr = GetObject(root, "ocr");
                if (ocr == null)
                {
                    throw new InvalidOperationException("Epson OCR 결과에 ocr 데이터가 없습니다.");
                }

                var result = new EpsonOcrResult
                {
                    Engine = GetString(ocr, "engine"),
                    PartNo = GetString(ocr, "part_no"),
                    PartNoSub = GetString(ocr, "part_no_sub"),
                    RawText = GetString(ocr, "text")
                };

                Dictionary<string, object> fields = GetObject(ocr, "fields");
                if (string.IsNullOrWhiteSpace(result.PartNo) && fields != null)
                {
                    result.PartNo = GetString(fields, "part_no");
                }
                if (string.IsNullOrWhiteSpace(result.PartNoSub) && fields != null)
                {
                    result.PartNoSub = GetString(fields, "part_no_sub");
                }

                Dictionary<string, object> quality = GetObject(ocr, "quality");
                if (quality != null)
                {
                    result.Confidence = GetDouble(quality, "confidence");
                    result.QualityOk = GetBool(quality, "ok");
                    result.QualityReason = GetString(quality, "reason");
                }
                else
                {
                    result.QualityOk = true;
                }

                result.NeedsConfirmation =
                    string.IsNullOrWhiteSpace(result.PartNo) ||
                    !result.QualityOk ||
                    result.Confidence < 0.80;
                return result;
            }
        }

        private Dictionary<string, object> Deserialize(string text)
        {
            Dictionary<string, object> value = _json.DeserializeObject(text) as Dictionary<string, object>;
            if (value == null)
            {
                throw new InvalidOperationException("Epson OCR 결과 JSON 형식이 올바르지 않습니다.");
            }
            return value;
        }

        private static Dictionary<string, object> GetObject(Dictionary<string, object> source, string name)
        {
            object value;
            return source != null && source.TryGetValue(name, out value)
                ? value as Dictionary<string, object>
                : null;
        }

        private static string GetString(Dictionary<string, object> source, string name)
        {
            object value;
            return source != null &&
                   source.TryGetValue(name, out value) &&
                   value != null
                ? Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty
                : string.Empty;
        }

        private static double GetDouble(Dictionary<string, object> source, string name)
        {
            object value;
            double number;
            return source != null &&
                   source.TryGetValue(name, out value) &&
                   value != null &&
                   double.TryParse(
                       Convert.ToString(value, CultureInfo.InvariantCulture),
                       NumberStyles.Float,
                       CultureInfo.InvariantCulture,
                       out number)
                ? number
                : 0.0;
        }

        private static bool GetBool(Dictionary<string, object> source, string name)
        {
            object value;
            bool result;
            return source != null &&
                   source.TryGetValue(name, out value) &&
                   value != null &&
                   bool.TryParse(Convert.ToString(value, CultureInfo.InvariantCulture), out result) &&
                   result;
        }

        private static string Quote(string value)
        {
            return "\"" + (value ?? string.Empty).Replace("\"", "\\\"") + "\"";
        }

        private static string BuildDiagnostic(string standardError, string standardOutput)
        {
            string detail = !string.IsNullOrWhiteSpace(standardError)
                ? standardError
                : standardOutput;

            if (string.IsNullOrWhiteSpace(detail))
            {
                return "작업자 출력이 없습니다. x86 self-contained Epson OCR 작업자 경로를 확인하세요.";
            }

            return detail.Length > 1200 ? detail.Substring(0, 1200) : detail;
        }
    }
}
