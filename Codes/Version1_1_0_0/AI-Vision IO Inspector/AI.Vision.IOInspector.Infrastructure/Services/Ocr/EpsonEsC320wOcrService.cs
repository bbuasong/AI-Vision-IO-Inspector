using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using AI.Vision.IOInspector.Application.Interfaces;
using AI.Vision.IOInspector.Application.Models;

namespace AI.Vision.IOInspector.Infrastructure.Services.Ocr
{
    /// <summary>
    /// ES-C320W 전용 스캔 및 OCR 흐름입니다.
    /// WIA 스캔 후 같은 파일을 x86 Epson OCR 작업자에 전달하며, 다른 USB 스캐너는 선택하지 않습니다.
    /// </summary>
    public class EpsonEsC320wOcrService : IOcrScanService, IDisposable
    {
        private readonly string _applicationRootPath;
        private readonly EpsonScanApiClient _scanApiClient;
        private readonly OcrScanSettingsStore _settingsStore;

        public EpsonEsC320wOcrService(string applicationRootPath)
        {
            _applicationRootPath = applicationRootPath;
            _settingsStore = new OcrScanSettingsStore(applicationRootPath);
            _scanApiClient = new EpsonScanApiClient(BuildWorkerPath());
        }

        public OcrScanConfiguration LoadConfiguration()
        {
            return _settingsStore.Load();
        }

        public void SaveConfiguration(OcrScanConfiguration configuration)
        {
            _settingsStore.Save(configuration);
        }

        public IList<OcrScannerDevice> RefreshScanners()
        {
            // 호출자가 API 시작 실패와 실제 USB 미연결을 구분해 표시할 수 있도록
            // API 통신 예외는 숨기지 않고 그대로 전달합니다.
            return _scanApiClient.GetScanners();
        }

        public Task<OcrScanExecutionResult> ScanAsync(OcrScanConfiguration configuration)
        {
            return ScanAsync(configuration, OcrScanUsage.Inspection);
        }

        public async Task<OcrScanExecutionResult> ScanAsync(OcrScanConfiguration configuration, OcrScanUsage usage)
        {
            OcrScanConfiguration effectiveConfiguration = configuration ?? LoadConfiguration();
            IList<OcrScannerDevice> scanners = RefreshScanners();
            if (scanners.Count == 0)
            {
                return Failed(string.Empty, "Epson ES-C320W 스캐너가 연결되지 않았습니다. 옵션의 OCR 탭에서 장치를 새로고침하세요.");
            }

            EpsonScanApiResult apiResult = null;
            try
            {
                apiResult = await _scanApiClient.ScanAsync(scanners[0], effectiveConfiguration);
                if (!apiResult.IsSuccess)
                {
                    // API 오류도 작업 레코드를 생성한 뒤 반환될 수 있습니다.
                    // jobs.json은 즉시 정리하되, 원인 분석에 필요한 이미지와 응답 JSON은 Failed 폴더에 보관합니다.
                    PreserveFailedApiWorkingData(apiResult);
                    CleanupApiWorkingData(apiResult);
                    OcrScanExecutionResult failedResult = Failed(string.Empty, string.Empty, BuildScanFailureMessage(apiResult.ErrorMessage));
                    failedResult.ApiStatus = apiResult.Status;
                    failedResult.ApiErrorMessage = apiResult.ErrorMessage;
                    failedResult.RawText = apiResult.RawText;
                    failedResult.PartNo = apiResult.PartNo;
                    failedResult.PartNoSource = apiResult.PartNoSource;
                    return failedResult;
                }

                string outputDirectory = BuildOutputDirectory(usage);
                string imagePath = await CopyImageToApplicationStorageAsync(apiResult.ImagePath, outputDirectory);
                string resultJsonPath = WriteResultJson(outputDirectory, imagePath, apiResult.ResponseJson);
                string reason = BuildResultMessage(apiResult);
                OcrScanExecutionResult scanResult = new OcrScanExecutionResult
                {
                    // 성공/오류는 EpsonScanApi JSON의 status와 error 해석 결과를 그대로 사용합니다.
                    // 품번 유무와 품질 참고 값은 UI 표시 및 후속 DB 조회에만 사용합니다.
                    IsSuccess = apiResult.IsSuccess,
                    ApiStatus = apiResult.Status,
                    ApiErrorMessage = apiResult.ErrorMessage,
                    PartNo = apiResult.PartNo,
                    ImagePath = imagePath,
                    ResultJsonPath = resultJsonPath,
                    RawText = apiResult.RawText,
                    PartNoSource = apiResult.PartNoSource,
                    Confidence = apiResult.Confidence,
                    QualityReason = apiResult.QualityReason,
                    Message = reason
                };

                // API가 완료 상태를 반환한 다음 OCR에서만 이전 실패 분석 자료를 모두 제거합니다.
                // low_quality도 API 완료 상태이므로 part_no를 그대로 후속 처리에 전달합니다.
                if (scanResult.IsSuccess)
                {
                    DeleteFailedApiWorkingData();
                }
                else
                {
                    PreserveFailedApiWorkingData(apiResult);
                }

                CleanupApiWorkingData(apiResult);
                return scanResult;
            }
            catch (Exception exception)
            {
                // 용지 없음은 스캐너가 고장 난 경우가 아니라 ADF의 정상적인 작업 조건입니다.
                // 외부 x86 API는 HTTP 409로 반환하므로, WPF에서는 예외를 전파하지 않고
                // 다음 스캔을 바로 시도할 수 있는 실패 결과로 변환합니다.
                // API 작업 폴더와 jobs.json은 실패해도 누적하지 않습니다.
                // 최종 보관이 필요한 등록/검사 이미지와 .ocr.json은 복사가 완료된 경우에만 별도 경로에 남습니다.
                string preservedImagePath = PreserveFailedApiWorkingData(apiResult);
                CleanupApiWorkingData(apiResult);
                if (HasSuccessfulRecognition(apiResult))
                {
                    return CreateRecognitionSuccessWithStorageWarning(apiResult, preservedImagePath, exception);
                }

                OcrScanExecutionResult failedResult = Failed(
                    apiResult == null ? string.Empty : apiResult.ImagePath,
                    string.Empty,
                    BuildScanFailureMessage(exception));
                failedResult.RawText = apiResult == null ? string.Empty : apiResult.RawText;
                failedResult.ApiStatus = apiResult == null ? string.Empty : apiResult.Status;
                failedResult.ApiErrorMessage = apiResult == null ? string.Empty : apiResult.ErrorMessage;
                failedResult.PartNo = apiResult == null ? string.Empty : apiResult.PartNo;
                failedResult.PartNoSource = apiResult == null ? string.Empty : apiResult.PartNoSource;
                return failedResult;
            }
        }

        /// <summary>
        /// EpsonScanApi의 작업 오류를 작업자가 이해할 수 있는 메시지로 정리합니다.
        /// 용지 없음은 API/스캐너를 재시작하지 않아도 용지를 올린 뒤 다시 스캔할 수 있습니다.
        /// </summary>
        private static string BuildScanFailureMessage(Exception exception)
        {
            return BuildScanFailureMessage(exception == null ? string.Empty : exception.Message);
        }

        private static string BuildScanFailureMessage(string message)
        {
            message = message ?? string.Empty;
            if (message.IndexOf("ADF empty", StringComparison.OrdinalIgnoreCase) >= 0
                || message.IndexOf("용지가 없습니다", StringComparison.OrdinalIgnoreCase) >= 0
                || message.IndexOf("급지구", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return "스캐너 ADF에 용지가 없습니다. 용지를 올린 뒤 OCR 스캔을 다시 시도하세요.";
            }

            if (string.IsNullOrWhiteSpace(message))
            {
                return "OCR 스캔 중 알 수 없는 오류가 발생했습니다. 스캐너 연결과 용지 상태를 확인하세요.";
            }

            return message;
        }

        public void Dispose()
        {
            _scanApiClient.Dispose();
        }

        /// <summary>
        /// 등록 OCR은 DB 저장 전까지 OCR_PATH 아래에만 파일을 생성합니다.
        /// 검사 OCR은 기존처럼 OUTPUT_PATH의 일자/시간별 OCR_Scan 이력 폴더를 사용합니다.
        /// </summary>
        private string BuildOutputDirectory(OcrScanUsage usage)
        {
            RuntimeImagePathSettings pathSettings = RuntimeImagePathSettings.Load(_applicationRootPath);
            DateTime now = DateTime.Now;
            if (usage == OcrScanUsage.Registration)
            {
                return Path.Combine(
                    pathSettings.OcrTemporaryRootPath,
                    now.ToString("yyyy"),
                    now.ToString("MM"),
                    now.ToString("dd"),
                    now.ToString("HH"),
                    "Registration");
            }

            return Path.Combine(
                pathSettings.HistoryImageRootPath,
                now.ToString("yyyy"),
                now.ToString("MM"),
                now.ToString("dd"),
                now.ToString("HH"),
                "OCR_Scan");
        }

        /// <summary>
        /// WPF가 보관할 OCR 결과 JSON 경로를 계산합니다.
        /// </summary>
        private static string BuildResultJsonPath(string imagePath)
        {
            if (string.IsNullOrWhiteSpace(imagePath))
            {
                return string.Empty;
            }

            return Path.Combine(
                Path.GetDirectoryName(imagePath) ?? string.Empty,
                Path.GetFileNameWithoutExtension(imagePath) + ".ocr.json");
        }

        /// <summary>
        /// x86 API 작업 폴더의 원본 스캔 이미지를 현장 이력/등록 임시 폴더로 복사합니다.
        /// API의 내부 작업 경로가 배포 폴더에 계속 쌓이지 않도록 이후 원본은 정리합니다.
        /// </summary>
        /// <summary>
        /// EpsonScanApi 완료 응답 직후 PNG 파일 핸들이 잠시 유지되는 경우를 처리합니다.
        /// 파일 잠금이 풀릴 때까지 짧게 재시도해 OCR 판독 성공 결과가 저장 단계에서 오류로 바뀌지 않게 합니다.
        /// </summary>
        private static async Task<string> CopyImageToApplicationStorageAsync(string sourceImagePath, string outputDirectory)
        {
            Directory.CreateDirectory(outputDirectory);
            string destinationPath = Path.Combine(
                outputDirectory,
                "scan_" + DateTime.Now.ToString("yyyyMMdd_HHmmss_fff") + ".png");
            IOException lastIOException = null;

            for (int attempt = 0; attempt < 20; attempt++)
            {
                if (string.IsNullOrWhiteSpace(sourceImagePath) || !File.Exists(sourceImagePath))
                {
                    if (attempt == 19)
                    {
                        throw new FileNotFoundException("EpsonScanApi가 반환한 스캔 이미지를 찾을 수 없습니다.", sourceImagePath);
                    }

                    await Task.Delay(250).ConfigureAwait(false);
                    continue;
                }

                try
                {
                    File.Copy(sourceImagePath, destinationPath, false);
                    return destinationPath;
                }
                catch (IOException exception)
                {
                    lastIOException = exception;
                    if (attempt < 19)
                    {
                        await Task.Delay(250).ConfigureAwait(false);
                    }
                }
            }

            throw new IOException(
                "EpsonScanApi 스캔 이미지 파일이 사용 중이어서 최종 저장 위치로 복사하지 못했습니다.",
                lastIOException);
        }

        private static string CopyImageToApplicationStorage(string sourceImagePath, string outputDirectory)
        {
            if (string.IsNullOrWhiteSpace(sourceImagePath) || !File.Exists(sourceImagePath))
            {
                throw new FileNotFoundException("EpsonScanApi가 저장한 스캔 이미지를 찾을 수 없습니다.", sourceImagePath);
            }

            Directory.CreateDirectory(outputDirectory);
            string destinationPath = Path.Combine(
                outputDirectory,
                "scan_" + DateTime.Now.ToString("yyyyMMdd_HHmmss_fff") + ".png");
            File.Copy(sourceImagePath, destinationPath, false);
            return destinationPath;
        }

        private static string WriteResultJson(string outputDirectory, string imagePath, string responseJson)
        {
            string resultJsonPath = BuildResultJsonPath(imagePath);
            Directory.CreateDirectory(outputDirectory);
            File.WriteAllText(resultJsonPath, responseJson ?? string.Empty);
            return resultJsonPath;
        }

        private static string BuildResultMessage(EpsonScanApiResult result)
        {
            if (result == null)
            {
                return "OCR 결과를 확인할 수 없습니다.";
            }

            if (!string.IsNullOrWhiteSpace(result.ErrorMessage))
            {
                return result.ErrorMessage;
            }

            return "EpsonScanApi status=" + result.Status + ", Source=" + result.PartNoSource;
        }

        private static void DeleteApiWorkingFiles(EpsonScanApiResult result)
        {
            if (result == null)
            {
                return;
            }

            DeleteFile(result.ImagePath);
            DeleteFile(result.CardImagePath);
            DeleteFile(result.PartCropImagePath);
            if (!string.Equals(result.OcrSourceImagePath, result.ImagePath, StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(result.OcrSourceImagePath, result.CardImagePath, StringComparison.OrdinalIgnoreCase))
            {
                DeleteFile(result.OcrSourceImagePath);
            }
        }

        /// <summary>
        /// x86 EpsonScanApi가 만든 작업 레코드와 작업 파일을 함께 정리합니다.
        /// 서버 삭제가 실패한 경우에도 로컬 파일 정리를 시도해 스캔 임시 폴더가 누적되지 않게 합니다.
        /// </summary>
        private void CleanupApiWorkingData(EpsonScanApiResult result)
        {
            if (result == null)
            {
                return;
            }

            _scanApiClient.DeleteServerJob(result.JobId);
            DeleteApiWorkingFiles(result);
        }

        /// <summary>
        /// API 작업 폴더에 있던 실패 증빙을 다음 정상 OCR 전까지 보관합니다.
        /// jobs.json은 작업 상태 파일이므로 보관하지 않고, 이미지와 응답 JSON만 Failed 폴더에 복사합니다.
        /// </summary>
        private string PreserveFailedApiWorkingData(EpsonScanApiResult result)
        {
            if (result == null)
            {
                return string.Empty;
            }

            try
            {
                string failureDirectory = BuildApiFailureDirectory();
                Directory.CreateDirectory(failureDirectory);

                string filePrefix = "scan_" + DateTime.Now.ToString("yyyyMMdd_HHmmss_fff");
                string preservedImagePath = CopyFailureImage(result.ImagePath, failureDirectory, filePrefix + ".png");
                CopyFailureImage(result.CardImagePath, failureDirectory, filePrefix + "_card.png");

                if (!string.Equals(result.OcrSourceImagePath, result.ImagePath, StringComparison.OrdinalIgnoreCase) &&
                    !string.Equals(result.OcrSourceImagePath, result.CardImagePath, StringComparison.OrdinalIgnoreCase))
                {
                    CopyFailureImage(result.OcrSourceImagePath, failureDirectory, filePrefix + "_ocr-source.png");
                }

                File.WriteAllText(
                    Path.Combine(failureDirectory, filePrefix + ".ocr.json"),
                    string.IsNullOrWhiteSpace(result.ResponseJson)
                        ? "{\"error\":\"OCR 처리 실패 응답이 비어 있습니다.\"}"
                        : result.ResponseJson);
                return preservedImagePath;
            }
            catch
            {
                // 실패 보관 자체가 다시 OCR 기능을 막으면 안 되므로, 원래 오류 흐름은 그대로 반환합니다.
                return string.Empty;
            }
        }

        /// <summary>
        /// 품번까지 정상 판정한 OCR이 완료되면 이전 실패 분석 자료를 한 번에 삭제합니다.
        /// 실패 보관함은 API 임시 폴더 하위의 고정 경로만 대상으로 하므로 최종 이력 폴더에는 영향을 주지 않습니다.
        /// </summary>
        private void DeleteFailedApiWorkingData()
        {
            try
            {
                string failureDirectory = BuildApiFailureDirectory();
                if (Directory.Exists(failureDirectory))
                {
                    Directory.Delete(failureDirectory, true);
                }
            }
            catch
            {
                // 파일이 열려 있으면 다음 정상 OCR에서 다시 정리를 시도합니다.
            }
        }

        private string BuildApiFailureDirectory()
        {
            return Path.Combine(_applicationRootPath, "Native", "EpsonOCR", "scans", "Failed");
        }

        private static string CopyFailureImage(string sourceImagePath, string failureDirectory, string destinationFileName)
        {
            if (string.IsNullOrWhiteSpace(sourceImagePath) || !File.Exists(sourceImagePath))
            {
                return string.Empty;
            }

            string destinationPath = Path.Combine(failureDirectory, destinationFileName);
            File.Copy(sourceImagePath, destinationPath, true);
            return destinationPath;
        }

        private static void DeleteFile(string filePath)
        {
            try
            {
                if (!string.IsNullOrWhiteSpace(filePath) && File.Exists(filePath))
                {
                    File.Delete(filePath);
                }
            }
            catch
            {
                // API 작업 파일을 정리하지 못해도 WPF가 저장한 검사 이력 파일은 유지합니다.
            }
        }

        /// <summary>
        /// 등록 OCR의 임시 파일만 삭제합니다. 경로가 OCR_PATH 밖이면 삭제하지 않습니다.
        /// </summary>
        public void DeleteTemporaryFiles(OcrScanExecutionResult result)
        {
            if (result == null)
            {
                return;
            }

            RuntimeImagePathSettings pathSettings = RuntimeImagePathSettings.Load(_applicationRootPath);
            string rootPath = Path.GetFullPath(pathSettings.OcrTemporaryRootPath)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

            DeleteFileIfUnderRoot(result.ImagePath, rootPath);
            DeleteFileIfUnderRoot(result.ResultJsonPath, rootPath);
            DeleteEmptyDirectories(result.ImagePath, rootPath);
        }

        private static void DeleteFileIfUnderRoot(string filePath, string rootPath)
        {
            if (string.IsNullOrWhiteSpace(filePath) || string.IsNullOrWhiteSpace(rootPath))
            {
                return;
            }

            string fullPath = Path.GetFullPath(filePath);
            string rootWithSeparator = rootPath + Path.DirectorySeparatorChar;
            if (!fullPath.StartsWith(rootWithSeparator, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            if (File.Exists(fullPath))
            {
                File.Delete(fullPath);
            }
        }

        private static void DeleteEmptyDirectories(string filePath, string rootPath)
        {
            if (string.IsNullOrWhiteSpace(filePath) || string.IsNullOrWhiteSpace(rootPath))
            {
                return;
            }

            string directoryPath = Path.GetDirectoryName(filePath);
            while (!string.IsNullOrWhiteSpace(directoryPath))
            {
                string fullDirectoryPath = Path.GetFullPath(directoryPath)
                    .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                if (string.Equals(fullDirectoryPath, rootPath, StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }

                if (!fullDirectoryPath.StartsWith(rootPath + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) ||
                    !Directory.Exists(fullDirectoryPath) ||
                    Directory.EnumerateFileSystemEntries(fullDirectoryPath).GetEnumerator().MoveNext())
                {
                    return;
                }

                Directory.Delete(fullDirectoryPath, false);
                directoryPath = Path.GetDirectoryName(fullDirectoryPath);
            }
        }

        private string BuildWorkerPath()
        {
            // OCR API는 배포 폴더만으로 실행되어야 합니다.
            // 프로젝트 루트를 다시 탐색하면 개발 PC의 Native 폴더를 잘못 실행하여
            // 임시 스캔 파일과 실행 설정이 배포 폴더 밖에 생성될 수 있습니다.
            return Path.Combine(_applicationRootPath, "Native", "EpsonOCR", "EpsonScanApi.exe");
        }

        private static OcrScanExecutionResult Failed(string imagePath, string message)
        {
            return Failed(imagePath, string.Empty, message);
        }

        /// <summary>
        /// API가 스캔과 품번 판독을 정상 완료한 경우만 판독 성공으로 인정합니다.
        /// 이 판정은 DB 등록 여부나 최종 이력 파일 복사 성공 여부와 무관합니다.
        /// </summary>
        private static bool HasSuccessfulRecognition(EpsonScanApiResult result)
        {
            return result != null && result.IsSuccess;
        }

        /// <summary>
        /// OCR 판독은 완료됐지만 최종 이력 보관 중 문제가 생긴 경우의 결과를 만듭니다.
        /// 판독 품번과 원문은 검사 검색에 계속 사용하고, 보관 경고는 UI와 실패 증빙에 남깁니다.
        /// </summary>
        private static OcrScanExecutionResult CreateRecognitionSuccessWithStorageWarning(
            EpsonScanApiResult apiResult,
            string preservedImagePath,
            Exception exception)
        {
            OcrScanExecutionResult result = new OcrScanExecutionResult();
            result.IsSuccess = true;
            result.ApiStatus = apiResult.Status;
            result.ApiErrorMessage = apiResult.ErrorMessage;
            result.PartNo = apiResult.PartNo;
            result.ImagePath = preservedImagePath ?? string.Empty;
            result.ResultJsonPath = BuildResultJsonPath(preservedImagePath);
            result.RawText = apiResult.RawText;
            result.PartNoSource = apiResult.PartNoSource;
            result.Confidence = apiResult.Confidence;
            result.QualityReason = apiResult.QualityReason;
            result.Message = "OCR 판독은 정상 완료됐지만 이미지/결과 JSON 보관 중 경고가 발생했습니다. " +
                             BuildScanFailureMessage(exception);
            return result;
        }

        private static OcrScanExecutionResult Failed(string imagePath, string resultJsonPath, string message)
        {
            return new OcrScanExecutionResult
            {
                IsSuccess = false,
                ImagePath = imagePath ?? string.Empty,
                ResultJsonPath = resultJsonPath ?? string.Empty,
                Message = string.IsNullOrWhiteSpace(message) ? "OCR 처리에 실패했습니다." : message
            };
        }
    }
}
