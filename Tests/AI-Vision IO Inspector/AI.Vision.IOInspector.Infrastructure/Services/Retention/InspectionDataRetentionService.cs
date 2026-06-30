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
            if (!oldestInspectionAt.HasValue)
            {
                return null;
            }

            if (settings.IsFreeSpaceCleanupEnabled)
            {
                InspectionDataCleanupCandidate freeSpaceCandidate = BuildFreeSpaceCandidate(settings, pathSettings, oldestInspectionAt.Value);
                if (freeSpaceCandidate != null)
                {
                    return freeSpaceCandidate;
                }
            }

            if (settings.IsRetentionPeriodCleanupEnabled)
            {
                DateTime cutoff = DateTime.Now.Date.AddDays(-settings.RetentionDays);
                if (oldestInspectionAt.Value < cutoff)
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

            result.DeletedInspectionCount = _inspectionRepository.DeleteInspectionsBefore(candidate.DeleteBefore);
            result.DeletedFolderCount = DeleteInspectionDataFoldersBefore(candidate.DataRootPath, candidate.DeleteBefore);
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

            DateTime monthStart = new DateTime(oldestInspectionAt.Year, oldestInspectionAt.Month, 1);
            DateTime deleteBefore = monthStart.AddMonths(1);
            return new InspectionDataCleanupCandidate
            {
                Reason = "저장 디스크 여유공간이 " +
                         freeSpacePercent.ToString("0.0", CultureInfo.InvariantCulture) +
                         "%로 기준 " +
                         settings.MinimumFreeSpacePercent.ToString("0.0", CultureInfo.InvariantCulture) +
                         "% 이하입니다. 가장 오래된 1개월 단위 검사 데이터를 삭제합니다.",
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

        private int DeleteInspectionDataFoldersBefore(string rootPath, DateTime cutoffExclusive)
        {
            if (string.IsNullOrWhiteSpace(rootPath) || !Directory.Exists(rootPath))
            {
                return 0;
            }

            int deletedCount = 0;
            DirectoryInfo rootDirectory = new DirectoryInfo(rootPath);
            deletedCount += DeleteNestedHourFoldersBefore(rootDirectory, cutoffExclusive);
            deletedCount += DeleteLegacyDayHourFoldersBefore(rootDirectory, cutoffExclusive);
            DeleteEmptyDirectories(rootDirectory, rootDirectory.FullName);
            return deletedCount;
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

        private bool TryParseFixedNumber(string value, int length, out int number)
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
