using System;
using System.Globalization;
using System.IO;
using AI.Vision.IOInspector.Application.Interfaces;

namespace AI.Vision.IOInspector.Infrastructure.Services.Retention
{
    /// <summary>
    /// 검사 데이터 삭제 후보를 계산하고, 승인된 삭제 요청을 실행합니다.
    /// UI 팝업은 이 서비스 밖에서 처리해 삭제 동작과 사용자 확인을 분리합니다.
    /// </summary>
    public class InspectionDataRetentionService
    {
        private readonly string _applicationRootPath;
        private readonly IInspectionRepository _inspectionRepository;

        public InspectionDataRetentionService(string applicationRootPath, IInspectionRepository inspectionRepository)
        {
            _applicationRootPath = applicationRootPath;
            _inspectionRepository = inspectionRepository;
        }

        public InspectionDataCleanupCandidate BuildCleanupCandidate(InspectionDataRetentionSettings settings)
        {
            if (settings == null)
            {
                return null;
            }

            RuntimeImagePathSettings pathSettings = RuntimeImagePathSettings.Load(_applicationRootPath);
            DateTime? oldestInspectionAt = _inspectionRepository.GetOldestInspectedAt();
            DateTime? oldestOcrScanAt = FindOldestOcrScanDate(pathSettings.HistoryImageRootPath);
            DateTime? oldestDataAt = FindOldestDate(oldestInspectionAt, oldestOcrScanAt);
            if (!oldestDataAt.HasValue)
            {
                return null;
            }

            if (settings.IsFreeSpaceCleanupEnabled)
            {
                InspectionDataCleanupCandidate freeSpaceCandidate = BuildFreeSpaceCandidate(settings, pathSettings, oldestDataAt.Value);
                if (freeSpaceCandidate != null)
                {
                    return freeSpaceCandidate;
                }
            }

            if (settings.IsRetentionPeriodCleanupEnabled)
            {
                DateTime cutoff = DateTime.Now.Date.AddDays(-settings.RetentionDays);
                if (oldestDataAt.Value < cutoff)
                {
                    return new InspectionDataCleanupCandidate
                    {
                        Reason = "설정기간 " + settings.RetentionDays.ToString(CultureInfo.InvariantCulture) + "일이 지난 검사 데이터를 삭제합니다.",
                        DeleteBefore = cutoff,
                        DataRootPath = pathSettings.HistoryImageRootPath
                    };
                }
            }

            return null;
        }

        public InspectionDataCleanupResult DeleteCandidate(InspectionDataCleanupCandidate candidate)
        {
            InspectionDataCleanupResult result = new InspectionDataCleanupResult();
            if (candidate == null)
            {
                return result;
            }

            result.DeleteFrom = candidate.DeleteFrom;
            result.DeleteBefore = candidate.DeleteBefore;
            result.DataRootPath = candidate.DataRootPath;
            result.FreeSpacePercentBefore = CalculateFreeSpacePercent(candidate.DataRootPath);

            result.DeletedFolderCount = DeleteInspectionDataFoldersBefore(candidate.DataRootPath, candidate.DeleteBefore);
            result.DeletedInspectionCount = _inspectionRepository.DeleteInspectionsBefore(candidate.DeleteBefore);
            result.FreeSpacePercentAfter = CalculateFreeSpacePercent(candidate.DataRootPath);
            WriteCleanupLog(candidate, result);
            return result;
        }

        private InspectionDataCleanupCandidate BuildFreeSpaceCandidate(
            InspectionDataRetentionSettings settings,
            RuntimeImagePathSettings pathSettings,
            DateTime oldestInspectionAt)
        {
            DriveInfo drive = ResolveDrive(pathSettings.HistoryImageRootPath);
            if (drive == null || drive.TotalSize <= 0)
            {
                return null;
            }

            decimal freeSpacePercent = (decimal)drive.AvailableFreeSpace * 100m / (decimal)drive.TotalSize;
            if (freeSpacePercent > settings.MinimumFreeSpacePercent)
            {
                return null;
            }

            DateTime dayStart = oldestInspectionAt.Date;
            DateTime deleteBefore = dayStart.AddDays(1);
            return new InspectionDataCleanupCandidate
            {
                Reason = "저장 디스크 여유공간이 " +
                         freeSpacePercent.ToString("0.0", CultureInfo.InvariantCulture) +
                         "%로 기준 " +
                         settings.MinimumFreeSpacePercent.ToString("0.0", CultureInfo.InvariantCulture) +
                         "% 이하입니다. 가장 오래된 1일 단위 검사 데이터를 삭제합니다.",
                DeleteFrom = dayStart,
                DeleteBefore = deleteBefore,
                DataRootPath = pathSettings.HistoryImageRootPath,
                CurrentFreeSpacePercent = freeSpacePercent,
                MinimumFreeSpacePercent = settings.MinimumFreeSpacePercent
            };
        }

        private DriveInfo ResolveDrive(string path)
        {
            try
            {
                string targetPath = string.IsNullOrWhiteSpace(path) ? ProjectDataRootResolver.Resolve(_applicationRootPath) : path;
                string rootPath = Path.GetPathRoot(Path.GetFullPath(targetPath));
                if (string.IsNullOrWhiteSpace(rootPath))
                {
                    return null;
                }

                DriveInfo drive = new DriveInfo(rootPath);
                return drive.IsReady ? drive : null;
            }
            catch
            {
                return null;
            }
        }

        private decimal? CalculateFreeSpacePercent(string path)
        {
            DriveInfo drive = ResolveDrive(path);
            if (drive == null || drive.TotalSize <= 0)
            {
                return null;
            }

            return (decimal)drive.AvailableFreeSpace * 100m / (decimal)drive.TotalSize;
        }

        private void WriteCleanupLog(InspectionDataCleanupCandidate candidate, InspectionDataCleanupResult result)
        {
            try
            {
                string rootPath = string.IsNullOrWhiteSpace(candidate.DataRootPath)
                    ? ProjectDataRootResolver.Resolve(_applicationRootPath)
                    : candidate.DataRootPath;
                string logFolderPath = Path.Combine(rootPath, "RetentionLog");
                Directory.CreateDirectory(logFolderPath);

                string logFilePath = Path.Combine(
                    logFolderPath,
                    DateTime.Now.ToString("yyyyMMdd", CultureInfo.InvariantCulture) + ".log");
                using (StreamWriter writer = new StreamWriter(logFilePath, true))
                {
                    writer.WriteLine("[" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture) + "]");
                    writer.WriteLine("Reason=" + candidate.Reason);
                    writer.WriteLine("DataRootPath=" + candidate.DataRootPath);
                    writer.WriteLine("DeleteFrom=" + FormatDateTime(result.DeleteFrom));
                    writer.WriteLine("DeleteBefore=" + FormatDateTime(result.DeleteBefore));
                    writer.WriteLine("DeletedFolderCount=" + result.DeletedFolderCount.ToString(CultureInfo.InvariantCulture));
                    writer.WriteLine("DeletedInspectionCount=" + result.DeletedInspectionCount.ToString(CultureInfo.InvariantCulture));
                    writer.WriteLine("FreeSpacePercentBefore=" + FormatPercent(result.FreeSpacePercentBefore));
                    writer.WriteLine("FreeSpacePercentAfter=" + FormatPercent(result.FreeSpacePercentAfter));
                    writer.WriteLine();
                }
            }
            catch
            {
                // 삭제 이력 로그 저장 실패가 검사 데이터 삭제 흐름을 막으면 안 됩니다.
            }
        }

        private string FormatDateTime(DateTime value)
        {
            return value == DateTime.MinValue
                ? string.Empty
                : value.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
        }

        private string FormatPercent(decimal? value)
        {
            return value.HasValue
                ? value.Value.ToString("0.0", CultureInfo.InvariantCulture)
                : string.Empty;
        }

        private int DeleteInspectionDataFoldersBefore(string rootPath, DateTime cutoffExclusive)
        {
            if (string.IsNullOrWhiteSpace(rootPath) || !Directory.Exists(rootPath))
            {
                return 0;
            }

            int deletedCount = 0;
            DirectoryInfo rootDirectory = new DirectoryInfo(rootPath);
            deletedCount += DeleteNestedHourFoldersBefore(rootDirectory, cutoffExclusive);
            deletedCount += DeleteNestedOcrScanFoldersBefore(rootDirectory, cutoffExclusive);
            deletedCount += DeleteLegacyDayHourFoldersBefore(rootDirectory, cutoffExclusive);
            DeleteEmptyDirectories(rootDirectory, rootDirectory.FullName);
            return deletedCount;
        }

        /// <summary>
        /// OUTPUT_PATHyyyyMMddOCR_Scan에 저장한 스캔 이미지를 같은 일 단위 보존 규칙으로 삭제합니다.
        /// 검사 이력 DB와 별도로 저장된 OCR 파일도 HDD 여유 공간 관리에서 누락되지 않게 합니다.
        /// </summary>
        private int DeleteNestedOcrScanFoldersBefore(DirectoryInfo rootDirectory, DateTime cutoffExclusive)
        {
            int deletedCount = 0;
            foreach (DirectoryInfo yearDirectory in rootDirectory.GetDirectories())
            {
                int year;
                if (!TryParseFixedNumber(yearDirectory.Name, 4, out year))
                {
                    continue;
                }

                foreach (DirectoryInfo monthDirectory in yearDirectory.GetDirectories())
                {
                    int month;
                    if (!TryParseFixedNumber(monthDirectory.Name, 2, out month))
                    {
                        continue;
                    }

                    foreach (DirectoryInfo dayDirectory in monthDirectory.GetDirectories())
                    {
                        int day;
                        if (!TryParseFixedNumber(dayDirectory.Name, 2, out day))
                        {
                            continue;
                        }

                        DateTime dayStart;
                        try
                        {
                            dayStart = new DateTime(year, month, day);
                        }
                        catch
                        {
                            continue;
                        }

                        DirectoryInfo ocrDirectory = new DirectoryInfo(Path.Combine(dayDirectory.FullName, "OCR_Scan"));
                        if (dayStart < cutoffExclusive && ocrDirectory.Exists && DeleteDirectory(ocrDirectory))
                        {
                            deletedCount++;
                        }
                    }
                }
            }

            return deletedCount;
        }

        private static DateTime? FindOldestDate(DateTime? first, DateTime? second)
        {
            if (!first.HasValue)
            {
                return second;
            }

            if (!second.HasValue)
            {
                return first;
            }

            return first.Value <= second.Value ? first : second;
        }

        /// <summary>
        /// 검사 이력이 없고 OCR 스캔 파일만 남아 있는 경우에도 보존 정책이 동작하도록 가장 오래된 OCR 일자를 찾습니다.
        /// </summary>
        private static DateTime? FindOldestOcrScanDate(string rootPath)
        {
            if (string.IsNullOrWhiteSpace(rootPath) || !Directory.Exists(rootPath))
            {
                return null;
            }

            DateTime? oldest = null;
            DirectoryInfo rootDirectory = new DirectoryInfo(rootPath);
            foreach (DirectoryInfo yearDirectory in rootDirectory.GetDirectories())
            {
                int year;
                if (!TryParseFixedNumber(yearDirectory.Name, 4, out year))
                {
                    continue;
                }

                foreach (DirectoryInfo monthDirectory in yearDirectory.GetDirectories())
                {
                    int month;
                    if (!TryParseFixedNumber(monthDirectory.Name, 2, out month))
                    {
                        continue;
                    }

                    foreach (DirectoryInfo dayDirectory in monthDirectory.GetDirectories())
                    {
                        int day;
                        if (!TryParseFixedNumber(dayDirectory.Name, 2, out day) ||
                            !HasOcrScanDirectory(dayDirectory))
                        {
                            continue;
                        }

                        try
                        {
                            DateTime date = new DateTime(year, month, day);
                            if (!oldest.HasValue || date < oldest.Value)
                            {
                                oldest = date;
                            }
                        }
                        catch
                        {
                        }
                    }
                }
            }

            return oldest;
        }

        /// <summary>
        /// 신규 OCR 저장 구조(YYYY/MM/DD/HH/OCR_Scan)와 이전 구조(YYYY/MM/DD/OCR_Scan)를 모두 인식합니다.
        /// 이전에 저장된 파일도 보존 기간 또는 HDD 여유 공간 정리 대상에서 누락되지 않게 합니다.
        /// </summary>
        private static bool HasOcrScanDirectory(DirectoryInfo dayDirectory)
        {
            if (dayDirectory == null)
            {
                return false;
            }

            if (Directory.Exists(Path.Combine(dayDirectory.FullName, "OCR_Scan")))
            {
                return true;
            }

            foreach (DirectoryInfo hourDirectory in dayDirectory.GetDirectories())
            {
                int hour;
                if (!TryParseFixedNumber(hourDirectory.Name, 2, out hour))
                {
                    continue;
                }

                if (Directory.Exists(Path.Combine(hourDirectory.FullName, "OCR_Scan")))
                {
                    return true;
                }
            }

            return false;
        }

        private int DeleteNestedHourFoldersBefore(DirectoryInfo rootDirectory, DateTime cutoffExclusive)
        {
            int deletedCount = 0;
            foreach (DirectoryInfo yearDirectory in rootDirectory.GetDirectories())
            {
                int year;
                if (!TryParseFixedNumber(yearDirectory.Name, 4, out year))
                {
                    continue;
                }

                foreach (DirectoryInfo monthDirectory in yearDirectory.GetDirectories())
                {
                    int month;
                    if (!TryParseFixedNumber(monthDirectory.Name, 2, out month))
                    {
                        continue;
                    }

                    foreach (DirectoryInfo dayDirectory in monthDirectory.GetDirectories())
                    {
                        int day;
                        if (!TryParseFixedNumber(dayDirectory.Name, 2, out day))
                        {
                            continue;
                        }

                        foreach (DirectoryInfo hourDirectory in dayDirectory.GetDirectories())
                        {
                            int hour;
                            if (!TryParseFixedNumber(hourDirectory.Name, 2, out hour))
                            {
                                continue;
                            }

                            DateTime hourStart;
                            try
                            {
                                hourStart = new DateTime(year, month, day, hour, 0, 0);
                            }
                            catch
                            {
                                continue;
                            }

                            if (hourStart < cutoffExclusive && DeleteDirectory(hourDirectory))
                            {
                                deletedCount++;
                            }
                        }
                    }
                }
            }

            return deletedCount;
        }

        private int DeleteLegacyDayHourFoldersBefore(DirectoryInfo rootDirectory, DateTime cutoffExclusive)
        {
            int deletedCount = 0;
            foreach (DirectoryInfo dayDirectory in rootDirectory.GetDirectories())
            {
                DateTime day;
                if (!DateTime.TryParseExact(dayDirectory.Name, "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out day))
                {
                    continue;
                }

                foreach (DirectoryInfo hourDirectory in dayDirectory.GetDirectories())
                {
                    int hour;
                    if (!TryParseFixedNumber(hourDirectory.Name, 2, out hour))
                    {
                        continue;
                    }

                    DateTime hourStart = day.AddHours(hour);
                    if (hourStart < cutoffExclusive && DeleteDirectory(hourDirectory))
                    {
                        deletedCount++;
                    }
                }
            }

            return deletedCount;
        }

        private static bool TryParseFixedNumber(string value, int length, out int number)
        {
            number = 0;
            return !string.IsNullOrWhiteSpace(value) &&
                   value.Length == length &&
                   int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out number);
        }

        private bool DeleteDirectory(DirectoryInfo directory)
        {
            try
            {
                directory.Delete(true);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private void DeleteEmptyDirectories(DirectoryInfo directory, string rootPath)
        {
            if (directory == null || !directory.Exists)
            {
                return;
            }

            foreach (DirectoryInfo child in directory.GetDirectories())
            {
                DeleteEmptyDirectories(child, rootPath);
            }

            if (string.Equals(directory.FullName, rootPath, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            try
            {
                if (directory.GetFiles().Length == 0 && directory.GetDirectories().Length == 0)
                {
                    directory.Delete();
                }
            }
            catch
            {
            }
        }
    }
}
