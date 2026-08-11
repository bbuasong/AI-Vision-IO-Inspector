using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using AI.Vision.IOInspector.Application.Models;

namespace AI.Vision.IOInspector.Infrastructure.Services.Ocr
{
    /// <summary>
    /// x64 WPF 프로그램에서 x86 EpsonScanApi.exe의 전체 OCR 파이프라인을 호출합니다.
    /// API 내부에서 ADF 스캔, 라벨 추출, Epson OCR, RapidOCR 보조 판독과 품번 합의를 수행합니다.
    /// </summary>
    internal class EpsonScanApiClient : IDisposable
    {
        private const string TargetScannerName = "ES-C320W";
        private const string ApiBaseAddress = "http://127.0.0.1:8000";
        private const int ApiStartTimeoutMilliseconds = 60000;
        private const int ApiRequestTimeoutMilliseconds = 180000;
        private const int StaleJobCleanupMinutes = 10;

        private readonly object _syncRoot;
        private readonly string _workerPath;
        private readonly JavaScriptSerializer _json;
        private readonly ISet<int> _existingRapidSidecarProcessIds;
        private readonly ISet<int> _ownedRapidSidecarProcessIds;
        private Process _apiProcess;
        private bool _ownsApiProcess;
        private bool _staleJobCleanupCompleted;
        private bool _disposed;

        public EpsonScanApiClient(string workerPath)
        {
            _syncRoot = new object();
            _workerPath = workerPath ?? string.Empty;
            _json = new JavaScriptSerializer();
            _existingRapidSidecarProcessIds = GetRapidSidecarProcessIds();
            _ownedRapidSidecarProcessIds = new HashSet<int>();
        }

        /// <summary>
        /// 샘플 API가 WIA로 조회한 ES-C320W 장치만 반환합니다.
        /// </summary>
        public IList<OcrScannerDevice> GetScanners()
        {
            EnsureStarted();

            Dictionary<string, object> root = Deserialize(Request("GET", "/scanners", string.Empty));
            IList<OcrScannerDevice> scanners = new List<OcrScannerDevice>();
            object scannerValues;
            IEnumerable<object> values = root.TryGetValue("scanners", out scannerValues)
                ? scannerValues as IEnumerable<object>
                : null;
            if (values == null)
            {
                return scanners;
            }

            foreach (object value in values)
            {
                Dictionary<string, object> scanner = value as Dictionary<string, object>;
                if (scanner == null)
                {
                    continue;
                }

                string deviceName = GetString(scanner, "name");
                if (deviceName.IndexOf(TargetScannerName, StringComparison.OrdinalIgnoreCase) < 0)
                {
                    continue;
                }

                scanners.Add(new OcrScannerDevice
                {
                    DeviceId = GetString(scanner, "id"),
                    DisplayName = deviceName
                });
            }

            return scanners;
        }

        /// <summary>
        /// 샘플과 동일한 scan-to-pdf(ocrOnly) API로 ADF 스캔과 하이브리드 OCR을 한 번에 수행합니다.
        /// PDF는 만들지 않아 32비트 OCR 작업자의 메모리 사용을 줄입니다.
        /// </summary>
        public Task<EpsonScanApiResult> ScanAsync(OcrScannerDevice scanner, OcrScanConfiguration configuration)
        {
            return Task.Factory.StartNew(
                delegate { return Scan(scanner, configuration); },
                CancellationToken.None,
                TaskCreationOptions.LongRunning,
                TaskScheduler.Default);
        }

        public void DeleteServerJob(string jobId)
        {
            if (string.IsNullOrWhiteSpace(jobId))
            {
                return;
            }

            try
            {
                Request("DELETE", "/jobs/" + Uri.EscapeDataString(jobId), string.Empty);
            }
            catch
            {
                // 서버 작업 목록 정리에 실패해도 OCR 결과 자체는 이미 WPF 측에 전달된 상태입니다.
            }
        }

        public void Dispose()
        {
            Process processToStop = null;
            IList<int> sidecarProcessIdsToStop = null;
            lock (_syncRoot)
            {
                if (_disposed)
                {
                    return;
                }

                _disposed = true;
                if (_ownsApiProcess)
                {
                    processToStop = _apiProcess;
                }

                _apiProcess = null;
                _ownsApiProcess = false;
                CaptureOwnedRapidSidecarProcessIds();
                sidecarProcessIdsToStop = new List<int>(_ownedRapidSidecarProcessIds);
            }

            try
            {
                if (processToStop != null && !processToStop.HasExited)
                {
                    processToStop.Kill();
                    processToStop.WaitForExit(3000);
                }
            }
            catch
            {
                // 종료 중 외부 x86 OCR 작업자가 이미 종료된 경우는 무시합니다.
            }
            finally
            {
                if (processToStop != null)
                {
                    processToStop.Dispose();
                }

                StopOwnedRapidSidecarProcesses(sidecarProcessIdsToStop);
            }
        }

        private EpsonScanApiResult Scan(OcrScannerDevice scanner, OcrScanConfiguration configuration)
        {
            if (scanner == null || string.IsNullOrWhiteSpace(scanner.DeviceId))
            {
                throw new InvalidOperationException("Epson ES-C320W 스캐너 정보를 찾지 못했습니다.");
            }

            EnsureStarted();

            OcrScanConfiguration effective = configuration ?? new OcrScanConfiguration();
            Dictionary<string, object> request = new Dictionary<string, object>();
            request["scan"] = CreateScanRequest(scanner.DeviceId, effective);
            request["card"] = CreateCardRequest(effective);
            request["pdf"] = CreateOcrRequest();

            try
            {
                // ADF 무급지는 HTTP 409로 반환되는 정상 작업 조건입니다.
                // 이 호출에서는 409 본문을 결과로 받아 예외 대신 실패 결과로 변환합니다.
                string response = Request("POST", "/scan-to-pdf?ocrOnly=true", _json.Serialize(request), ApiRequestTimeoutMilliseconds, true);
                if (IsApiFailureResponse(response))
                {
                    EpsonScanApiResult failureResult = CreateApiFailureResult(response);
                    LoadFailureWorkingFilePaths(failureResult);
                    return failureResult;
                }

                return ParseResult(response);
            }
            finally
            {
                // scan-to-pdf 호출 과정에서 샘플 API가 RapidOCR 보조 프로세스를 기동할 수 있습니다.
                // 기존 외부 프로세스와 구분하기 위해 새 PID만 앱 소유 목록에 기록합니다.
                CaptureOwnedRapidSidecarProcessIds();
            }
        }

        private Dictionary<string, object> CreateScanRequest(string deviceId, OcrScanConfiguration configuration)
        {
            Dictionary<string, object> scan = new Dictionary<string, object>();
            scan["device_id"] = deviceId;
            scan["dpi"] = NormalizeResolution(configuration.ResolutionDpi);
            scan["mode"] = NormalizeColorMode(configuration.ColorMode);
            scan["source"] = "feeder";
            scan["fmt"] = "png";
            return scan;
        }

        private static Dictionary<string, object> CreateCardRequest(OcrScanConfiguration configuration)
        {
            Dictionary<string, object> card = new Dictionary<string, object>();
            card["dpi"] = NormalizeResolution(configuration.ResolutionDpi);
            card["debug"] = false;
            return card;
        }

        private static Dictionary<string, object> CreateOcrRequest()
        {
            Dictionary<string, object> ocr = new Dictionary<string, object>();
            ocr["lang"] = "kor+eng";
            ocr["engine"] = "auto";
            return ocr;
        }

        private EpsonScanApiResult ParseResult(string response)
        {
            Dictionary<string, object> root = Deserialize(response);
            EpsonScanApiResult result = new EpsonScanApiResult();
            result.JobId = GetString(root, "id");
            // EpsonScanApi는 "scans\\..." 형태의 API 실행 폴더 기준 상대 경로를 반환합니다.
            // WPF의 현재 작업 폴더와 다를 수 있으므로 Native\EpsonOCR 기준 절대 경로로 바꿉니다.
            result.ImagePath = ResolveApiFilePath(GetFirstNonEmptyString(root, "image_path", "scan_path"));
            result.CardImagePath = ResolveApiFilePath(GetString(root, "card_path"));
            result.OcrSourceImagePath = ResolveApiFilePath(GetString(root, "ocr_src_path"));
            if (string.IsNullOrWhiteSpace(result.ImagePath))
            {
                // 구버전 API가 원본 경로 대신 OCR 입력 경로만 반환하는 경우를 보완합니다.
                result.ImagePath = result.OcrSourceImagePath;
            }
            result.Status = GetString(root, "status");
            result.ErrorMessage = GetString(root, "error");

            Dictionary<string, object> ocr = GetObject(root, "ocr");
            if (ocr == null)
            {
                throw new InvalidOperationException(
                    string.IsNullOrWhiteSpace(result.ErrorMessage)
                        ? "EpsonScanApi 응답에 OCR 결과가 없습니다."
                        : result.ErrorMessage);
            }

            Dictionary<string, object> quality = GetObject(ocr, "quality");
            result.PartNo = GetString(ocr, "part_no");
            // API 버전에 따라 text 또는 engine_raw_text 중 하나만 전달될 수 있습니다.
            // 옵션 UI에는 실제 OCR 원문을 우선 표시해야 하므로 비어 있지 않은 값을 선택합니다.
            result.RawText = GetFirstNonEmptyString(ocr, "text", "engine_raw_text", "raw_text");
            result.PartNoSource = GetString(ocr, "part_no_source");
            result.PartCropImagePath = ResolveApiFilePath(GetString(ocr, "part_crop_path"));
            result.NeedsConfirmation = GetBoolean(ocr, "needs_rescan") || string.IsNullOrWhiteSpace(result.PartNo);
            result.QualityOk = quality == null || GetBoolean(quality, "ok");
            result.Confidence = quality == null ? 0.0 : GetDouble(quality, "confidence");
            result.QualityReason = quality == null ? string.Empty : GetString(quality, "reason");
            result.ResponseJson = response;

            // OCR 성공/오류 판단은 API JSON의 status와 error만 사용합니다.
            // low_quality는 API가 재스캔을 권고하는 완료 상태이며, part_no가 반환되었다면
            // 검색/등록 화면으로 전달해야 합니다. quality와 needs_rescan은 참고 정보로만 보관합니다.
            result.IsSuccess = IsCompletedOcrStatus(result.Status) &&
                               string.IsNullOrWhiteSpace(result.ErrorMessage);

            return result;
        }

        /// <summary>
        /// EpsonScanApi가 OCR 결과를 반환한 완료 상태인지 확인합니다.
        /// low_quality는 품질 경고이지 통신 또는 스캔 실패가 아닙니다.
        /// </summary>
        private static bool IsCompletedOcrStatus(string status)
        {
            return string.Equals(status, "done", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(status, "low_quality", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// 여러 API 버전에서 같은 의미로 사용되는 문자열 필드 중 첫 번째 유효 값을 반환합니다.
        /// </summary>
        private static string GetFirstNonEmptyString(Dictionary<string, object> source, params string[] keys)
        {
            if (source == null || keys == null)
            {
                return string.Empty;
            }

            foreach (string key in keys)
            {
                string value = GetString(source, key);
                if (!string.IsNullOrWhiteSpace(value))
                {
                    return value;
                }
            }

            return string.Empty;
        }

        /// <summary>
        /// EpsonScanApi 응답 파일 경로를 API 실행 위치 기준의 절대 경로로 변환합니다.
        /// 배포 위치와 WPF 작업 디렉터리가 달라도 스캔 결과 이미지를 찾도록 합니다.
        /// </summary>
        private string ResolveApiFilePath(string apiFilePath)
        {
            if (string.IsNullOrWhiteSpace(apiFilePath))
            {
                return string.Empty;
            }

            if (Path.IsPathRooted(apiFilePath))
            {
                return apiFilePath;
            }

            string apiDirectoryPath = Path.GetDirectoryName(_workerPath);
            if (string.IsNullOrWhiteSpace(apiDirectoryPath))
            {
                return apiFilePath;
            }

            return Path.GetFullPath(Path.Combine(apiDirectoryPath, apiFilePath));
        }

        /// <summary>
        /// HTTP 409 등 API가 반환한 작업 실패 본문을 예외 없이 OCR 실패 결과로 만듭니다.
        /// </summary>
        private bool IsApiFailureResponse(string response)
        {
            Dictionary<string, object> root = Deserialize(response);
            return !string.IsNullOrWhiteSpace(GetString(root, "detail")) ||
                   !string.IsNullOrWhiteSpace(GetString(root, "error"));
        }

        private EpsonScanApiResult CreateApiFailureResult(string response)
        {
            Dictionary<string, object> root = Deserialize(response);
            EpsonScanApiResult result = new EpsonScanApiResult();
            result.IsSuccess = false;
            // 실패 응답에도 작업 ID를 포함하면 WPF가 ADF 무급지나 OCR 오류 뒤에도
            // API 임시 이미지와 jobs.json 작업 레코드를 즉시 정리할 수 있습니다.
            result.JobId = GetString(root, "id");
            result.ErrorMessage = GetString(root, "detail");
            if (string.IsNullOrWhiteSpace(result.ErrorMessage))
            {
                result.ErrorMessage = GetString(root, "error");
            }

            result.ResponseJson = response;
            return result;
        }

        /// <summary>
        /// scan-to-pdf 오류 응답에는 작업 ID와 오류 메시지만 포함될 수 있습니다.
        /// 실패 원인 분석용 원본/카드/방향 보정 이미지를 보관하기 전에 JobRegistry에서 실제 파일 경로를 다시 읽습니다.
        /// </summary>
        private void LoadFailureWorkingFilePaths(EpsonScanApiResult result)
        {
            if (result == null || string.IsNullOrWhiteSpace(result.JobId))
            {
                return;
            }

            try
            {
                Dictionary<string, object> job = Deserialize(
                    Request("GET", "/jobs/" + Uri.EscapeDataString(result.JobId), string.Empty, 10000));

                result.ImagePath = ResolveApiFilePath(GetString(job, "image_path"));
                result.CardImagePath = ResolveApiFilePath(GetString(job, "card_path"));
                result.OcrSourceImagePath = ResolveApiFilePath(GetString(job, "ocr_src_path"));

                string jobError = GetString(job, "error");
                if (!string.IsNullOrWhiteSpace(jobError))
                {
                    result.ErrorMessage = jobError;
                }
            }
            catch
            {
                // 오류 응답 이후 API가 이미 종료된 경우에도 최초 오류 메시지와 작업 ID는 유지합니다.
                // 이 경우 보관 가능한 파일 경로가 없으므로 .ocr.json만 실패 보관함에 남습니다.
            }
        }

        private void EnsureStarted()
        {
            ThrowIfDisposed();
            if (IsHealthy())
            {
                EnsureConnectedApiUsesConfiguredWorker();
                CleanupStaleServerJobsOnce();
                return;
            }

            lock (_syncRoot)
            {
                ThrowIfDisposed();
                if (IsHealthy())
                {
                    EnsureConnectedApiUsesConfiguredWorker();
                    CleanupStaleServerJobsOnce();
                    return;
                }

                if (string.IsNullOrWhiteSpace(_workerPath) || !File.Exists(_workerPath))
                {
                    throw new FileNotFoundException("Native\\EpsonOCR\\EpsonScanApi.exe 파일을 찾을 수 없습니다.", _workerPath);
                }

                ProcessStartInfo startInfo = new ProcessStartInfo();
                startInfo.FileName = _workerPath;
                startInfo.WorkingDirectory = Path.GetDirectoryName(_workerPath);
                startInfo.UseShellExecute = false;
                startInfo.CreateNoWindow = true;
                startInfo.WindowStyle = ProcessWindowStyle.Hidden;

                _apiProcess = Process.Start(startInfo);
                if (_apiProcess == null)
                {
                    throw new InvalidOperationException("EpsonScanApi.exe를 시작하지 못했습니다.");
                }

                _ownsApiProcess = true;
            }

            DateTime limit = DateTime.Now.AddMilliseconds(ApiStartTimeoutMilliseconds);
            while (DateTime.Now < limit)
            {
                if (IsHealthy())
                {
                    EnsureConnectedApiUsesConfiguredWorker();
                    CleanupStaleServerJobsOnce();
                    return;
                }

                Thread.Sleep(500);
            }

            throw new TimeoutException("EpsonScanApi.exe가 60초 안에 준비되지 않았습니다. Native\\EpsonOCR의 appsettings.json 및 rapid_sidecar.exe를 확인하세요.");
        }

        /// <summary>
        /// 포트 8000에 이미 떠 있는 API가 현재 실행 폴더의 OCR API인지 확인합니다.
        /// 개발 프로젝트의 Native 폴더나 별도 샘플 API에 연결하면 이미지가 배포 폴더 밖에 생성되므로,
        /// 다른 경로의 API는 재사용하지 않고 작업자에게 종료를 안내합니다.
        /// </summary>
        private void EnsureConnectedApiUsesConfiguredWorker()
        {
            string expectedPath = Path.GetFullPath(_workerPath);
            Process[] processes = Process.GetProcessesByName("EpsonScanApi");
            try
            {
                foreach (Process process in processes)
                {
                    string actualPath = GetProcessExecutablePath(process);
                    if (string.IsNullOrWhiteSpace(actualPath))
                    {
                        continue;
                    }

                    if (string.Equals(
                        Path.GetFullPath(actualPath),
                        expectedPath,
                        StringComparison.OrdinalIgnoreCase))
                    {
                        return;
                    }

                    throw new InvalidOperationException(
                        "다른 EpsonScanApi.exe가 포트 8000을 사용 중입니다. " +
                        "현재 경로: " + actualPath + Environment.NewLine +
                        "필요 경로: " + expectedPath + Environment.NewLine +
                        "기존 샘플 또는 프로젝트 루트 OCR API를 종료한 후 다시 실행하세요.");
                }
            }
            finally
            {
                foreach (Process process in processes)
                {
                    process.Dispose();
                }
            }

            throw new InvalidOperationException(
                "포트 8000의 OCR API 프로세스 경로를 확인하지 못했습니다. " +
                "현재 실행 폴더의 Native\\EpsonOCR\\EpsonScanApi.exe만 실행되도록 확인하세요.");
        }

        /// <summary>
        /// 실행 중인 EpsonScanApi 프로세스의 실제 EXE 경로를 읽습니다.
        /// 다른 계정 또는 보호된 프로세스는 경로 조회가 제한될 수 있습니다.
        /// </summary>
        private static string GetProcessExecutablePath(Process process)
        {
            try
            {
                return process.MainModule == null ? string.Empty : process.MainModule.FileName;
            }
            catch
            {
                return string.Empty;
            }
        }

        /// <summary>
        /// 이전 비정상 종료로 남은 API 작업 파일만 한 번 정리합니다.
        /// Native\\EpsonOCR\\scans는 임시 작업 폴더이므로 최종 검사/등록 이력은 삭제하지 않습니다.
        /// </summary>
        private void CleanupStaleServerJobsOnce()
        {
            lock (_syncRoot)
            {
                if (_staleJobCleanupCompleted)
                {
                    return;
                }
            }

            try
            {
                Request("DELETE", "/jobs?olderThanMinutes=" + StaleJobCleanupMinutes.ToString(CultureInfo.InvariantCulture), string.Empty, 10000);
                lock (_syncRoot)
                {
                    _staleJobCleanupCompleted = true;
                }
            }
            catch
            {
                // 구버전 API가 정리 경로를 아직 제공하지 않아도 스캔 기능은 계속 동작해야 합니다.
            }
        }

        private bool IsHealthy()
        {
            try
            {
                // 샘플의 /health는 RapidOCR 진단을 위해 보조 모델 프로세스를 기동합니다.
                // 단순 준비 확인에서는 OCR 엔진을 실행하지 않는 /jobs를 사용합니다.
                string response = Request("GET", "/jobs", string.Empty, 1500);
                Dictionary<string, object> root = Deserialize(response);
                return root.ContainsKey("count");
            }
            catch
            {
                return false;
            }
        }

        private string Request(string method, string path, string body)
        {
            return Request(method, path, body, ApiRequestTimeoutMilliseconds);
        }

        private static string Request(string method, string path, string body, int timeoutMilliseconds)
        {
            return Request(method, path, body, timeoutMilliseconds, false);
        }

        /// <summary>
        /// HttpClient는 HTTP 상태 코드를 예외로 바꾸지 않습니다.
        /// 따라서 ADF 무급지의 HTTP 409를 정상 실패 결과로 처리할 수 있습니다.
        /// </summary>
        private static string Request(string method, string path, string body, int timeoutMilliseconds, bool allowConflictResponse)
        {
            using (HttpClient client = new HttpClient())
            using (HttpRequestMessage request = new HttpRequestMessage(new HttpMethod(method), ApiBaseAddress + path))
            {
                client.Timeout = TimeSpan.FromMilliseconds(timeoutMilliseconds);
                request.Headers.Accept.ParseAdd("application/json");
                if (!string.IsNullOrWhiteSpace(body))
                {
                    request.Content = new StringContent(body, Encoding.UTF8, "application/json");
                }

                try
                {
                    using (HttpResponseMessage response = client.SendAsync(request).GetAwaiter().GetResult())
                    {
                        string responseBody = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
                        if (response.IsSuccessStatusCode || allowConflictResponse)
                        {
                            return responseBody;
                        }

                        throw new InvalidOperationException(
                            string.IsNullOrWhiteSpace(responseBody)
                                ? "EpsonScanApi 처리 실패: HTTP " + (int)response.StatusCode
                                : "EpsonScanApi 처리 실패: " + responseBody);
                    }
                }
                catch (HttpRequestException exception)
                {
                    throw new InvalidOperationException("EpsonScanApi 통신에 실패했습니다. " + exception.Message, exception);
                }
                catch (TaskCanceledException exception)
                {
                    throw new InvalidOperationException("EpsonScanApi 응답 시간이 초과되었습니다.", exception);
                }
            }
        }

        private Dictionary<string, object> Deserialize(string json)
        {
            Dictionary<string, object> root = _json.DeserializeObject(json) as Dictionary<string, object>;
            if (root == null)
            {
                throw new InvalidOperationException("EpsonScanApi 응답 JSON 형식이 올바르지 않습니다.");
            }

            return root;
        }

        private static Dictionary<string, object> GetObject(Dictionary<string, object> source, string name)
        {
            object value;
            return source != null && source.TryGetValue(name, out value) ? value as Dictionary<string, object> : null;
        }

        private static string GetString(Dictionary<string, object> source, string name)
        {
            object value;
            return source != null && source.TryGetValue(name, out value) && value != null
                ? Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty
                : string.Empty;
        }

        private static bool GetBoolean(Dictionary<string, object> source, string name)
        {
            object value;
            bool result;
            return source != null && source.TryGetValue(name, out value) && value != null &&
                   bool.TryParse(Convert.ToString(value, CultureInfo.InvariantCulture), out result) && result;
        }

        private static double GetDouble(Dictionary<string, object> source, string name)
        {
            object value;
            double result;
            return source != null && source.TryGetValue(name, out value) && value != null &&
                   double.TryParse(Convert.ToString(value, CultureInfo.InvariantCulture), NumberStyles.Float, CultureInfo.InvariantCulture, out result)
                ? result
                : 0.0;
        }

        private static int NormalizeResolution(int dpi)
        {
            return dpi == 300 || dpi == 400 || dpi == 600 ? dpi : 400;
        }

        private static string NormalizeColorMode(string colorMode)
        {
            if (string.Equals(colorMode, "bw", StringComparison.OrdinalIgnoreCase))
            {
                return "bw";
            }

            if (string.Equals(colorMode, "color", StringComparison.OrdinalIgnoreCase))
            {
                return "color";
            }

            return "gray";
        }

        private void ThrowIfDisposed()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException("EpsonScanApiClient");
            }
        }

        /// <summary>
        /// 현재 실행 중인 RapidOCR 보조 프로세스 PID를 읽습니다.
        /// </summary>
        private static ISet<int> GetRapidSidecarProcessIds()
        {
            ISet<int> processIds = new HashSet<int>();
            Process[] processes = Process.GetProcessesByName("rapid_sidecar");
            foreach (Process process in processes)
            {
                try
                {
                    processIds.Add(process.Id);
                }
                finally
                {
                    process.Dispose();
                }
            }

            return processIds;
        }

        /// <summary>
        /// 앱 시작 이후 새로 생긴 RapidOCR 보조 프로세스만 종료 대상으로 표시합니다.
        /// </summary>
        private void CaptureOwnedRapidSidecarProcessIds()
        {
            ISet<int> currentProcessIds = GetRapidSidecarProcessIds();
            foreach (int processId in currentProcessIds)
            {
                if (!_existingRapidSidecarProcessIds.Contains(processId))
                {
                    _ownedRapidSidecarProcessIds.Add(processId);
                }
            }
        }

        /// <summary>
        /// EpsonScanApi가 이번 앱 실행 중 자동 기동한 RapidOCR 프로세스만 종료합니다.
        /// </summary>
        private static void StopOwnedRapidSidecarProcesses(IList<int> processIds)
        {
            if (processIds == null)
            {
                return;
            }

            foreach (int processId in processIds)
            {
                try
                {
                    using (Process process = Process.GetProcessById(processId))
                    {
                        if (!process.HasExited)
                        {
                            process.Kill();
                            process.WaitForExit(3000);
                        }
                    }
                }
                catch
                {
                    // 이미 종료됐거나 다른 이유로 접근할 수 없는 경우에는 무시합니다.
                }
            }
        }
    }

    /// <summary>
    /// EpsonScanApi의 scan-to-pdf(ocrOnly) 응답에서 WPF에 필요한 값만 정리한 결과입니다.
    /// </summary>
    internal class EpsonScanApiResult
    {
        public EpsonScanApiResult()
        {
            IsSuccess = false;
            JobId = string.Empty;
            ImagePath = string.Empty;
            CardImagePath = string.Empty;
            OcrSourceImagePath = string.Empty;
            PartCropImagePath = string.Empty;
            Status = string.Empty;
            ErrorMessage = string.Empty;
            PartNo = string.Empty;
            RawText = string.Empty;
            PartNoSource = string.Empty;
            QualityReason = string.Empty;
            ResponseJson = string.Empty;
        }

        public string JobId { get; set; }
        public bool IsSuccess { get; set; }
        public string ImagePath { get; set; }
        public string CardImagePath { get; set; }
        public string OcrSourceImagePath { get; set; }
        public string PartCropImagePath { get; set; }
        public string Status { get; set; }
        public string ErrorMessage { get; set; }
        public string PartNo { get; set; }
        public string RawText { get; set; }
        public string PartNoSource { get; set; }
        public bool NeedsConfirmation { get; set; }
        public bool QualityOk { get; set; }
        public double Confidence { get; set; }
        public string QualityReason { get; set; }
        public string ResponseJson { get; set; }
    }
}
