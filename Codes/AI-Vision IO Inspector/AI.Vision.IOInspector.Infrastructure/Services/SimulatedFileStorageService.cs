using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using AI.Vision.IOInspector.Application.Interfaces;
using AI.Vision.IOInspector.Domain.Models;
using AI.Vision.IOInspector.Infrastructure.Repositories;
using AI.Vision.IOInspector.Infrastructure;

namespace AI.Vision.IOInspector.Infrastructure.Services
{
    /// <summary>
    /// 검사 결과를 텍스트 파일로 남기는 개발용 저장 서비스입니다.
    /// 실제 운영에서는 이미지 원본, NG 이미지, 로그 저장 정책을 이 클래스 계열에서 구현합니다.
    /// </summary>
    public class SimulatedFileStorageService : IFileStorageService
    {
        private readonly string _rootPath;
        private readonly InspectionHistoryRetentionOptions _retentionOptions;

        public SimulatedFileStorageService(string rootPath)
            : this(rootPath, new InspectionHistoryRetentionOptions())
        {
        }

        public SimulatedFileStorageService(string rootPath, InspectionHistoryRetentionOptions retentionOptions)
        {
            _rootPath = ProjectDataRootResolver.Resolve(rootPath);
            _retentionOptions = retentionOptions;
        }

        public void StoreInspection(Inspection inspection)
        {
            RuntimeImagePathSettings pathSettings = RuntimeImagePathSettings.Load(_rootPath);
            DateTime inspectedAt = inspection.InspectedAt == DateTime.MinValue ? DateTime.Now : inspection.InspectedAt;
            string hourPath = BuildInspectionHourPath(pathSettings.HistoryImageRootPath, inspectedAt);
            string categoryFolder = SanitizePathSegment(inspection.CategoryCode);
            string partFolder = SanitizePathSegment(inspection.PartNo);
            string fileName = "Inspection_" + inspection.Id.ToString("0000") + "_" + inspectedAt.ToString("HHmmssfff") + ".txt";

            string historyFolderPath = Path.Combine(hourPath, "History", categoryFolder, partFolder);
            string logFolderPath = Path.Combine(hourPath, "Log", categoryFolder, partFolder);
            Directory.CreateDirectory(historyFolderPath);
            Directory.CreateDirectory(logFolderPath);

            WriteHistoryFile(Path.Combine(historyFolderPath, fileName), inspection);
            WriteLogFile(Path.Combine(logFolderPath, fileName), inspection);
        }

        private void WriteHistoryFile(string filePath, Inspection inspection)
        {
            using (StreamWriter writer = new StreamWriter(filePath, false))
            {
                writer.WriteLine("InspectionId=" + inspection.Id);
                writer.WriteLine("PartNo=" + inspection.PartNo);
                writer.WriteLine("PartName=" + inspection.PartName);
                writer.WriteLine("CategoryCode=" + inspection.CategoryCode);
                writer.WriteLine("CategoryDescription=" + inspection.CategoryDescription);
                writer.WriteLine("PartType=" + inspection.PartType);
                writer.WriteLine("Result=" + inspection.Result);
                writer.WriteLine("Message=" + inspection.ResultMessage);
                writer.WriteLine("ElapsedMs=" + inspection.ElapsedMilliseconds);

                foreach (MeasurementResult measurement in inspection.Measurements)
                {
                    writer.WriteLine(measurement.Name + ": " + measurement.MeasuredValue + measurement.Unit + " / " + measurement.Message);
                }
            }
        }

        private void WriteLogFile(string filePath, Inspection inspection)
        {
            using (StreamWriter writer = new StreamWriter(filePath, false))
            {
                writer.WriteLine("InspectionId=" + inspection.Id);
                writer.WriteLine("EventCount=" + inspection.Events.Count);
                foreach (EventLogEntry entry in inspection.Events)
                {
                    writer.WriteLine(
                        entry.CreatedAt.ToString("o", CultureInfo.InvariantCulture) +
                        " [" + entry.Severity + "] " +
                        entry.Source + " - " +
                        entry.Message);
                }
            }
        }

        private string BuildInspectionHourPath(string rootPath, DateTime inspectedAt)
        {
            string hourPath = Path.Combine(
                rootPath,
                inspectedAt.ToString("yyyy"),
                inspectedAt.ToString("MM"),
                inspectedAt.ToString("dd"),
                inspectedAt.ToString("HH"));
            Directory.CreateDirectory(Path.Combine(hourPath, "History"));
            Directory.CreateDirectory(Path.Combine(hourPath, "Image"));
            Directory.CreateDirectory(Path.Combine(hourPath, "Log"));
            return hourPath;
        }

        private string SanitizePathSegment(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return "UNKNOWN";
            }

            string sanitized = value.Trim();
            foreach (char invalidCharacter in Path.GetInvalidFileNameChars())
            {
                sanitized = sanitized.Replace(invalidCharacter, '_');
            }

            return sanitized.Length > 80 ? sanitized.Substring(0, 80).Trim() : sanitized;
        }

        private void ApplyRetentionPolicy(string logRootPath)
        {
            DeleteExpiredDayFolders(logRootPath);
            DeleteOldestDayFoldersUntilFreeSpaceIsEnough(logRootPath);
        }

        private void DeleteExpiredDayFolders(string logRootPath)
        {
            if (_retentionOptions.RetentionDays <= 0)
            {
                return;
            }

            DateTime cutoffDate = DateTime.Now.Date.AddDays(-_retentionOptions.RetentionDays);
            foreach (DirectoryInfo dayFolder in GetDayFolders(logRootPath))
            {
                DateTime folderDate;
                if (DateTime.TryParseExact(dayFolder.Name, "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out folderDate))
                {
                    if (folderDate < cutoffDate)
                    {
                        DeleteDirectory(dayFolder);
                    }
                }
            }
        }

        private void DeleteOldestDayFoldersUntilFreeSpaceIsEnough(string logRootPath)
        {
            if (_retentionOptions.MinimumFreeSpaceBytes <= 0)
            {
                return;
            }

            DriveInfo drive = new DriveInfo(Path.GetPathRoot(logRootPath));
            while (drive.AvailableFreeSpace < _retentionOptions.MinimumFreeSpaceBytes)
            {
                DirectoryInfo oldestFolder = GetOldestDayFolder(logRootPath);
                if (oldestFolder == null)
                {
                    return;
                }

                DeleteDirectory(oldestFolder);
                drive = new DriveInfo(Path.GetPathRoot(logRootPath));
            }
        }

        private IEnumerable<DirectoryInfo> GetDayFolders(string rootPath)
        {
            DirectoryInfo root = new DirectoryInfo(rootPath);
            if (!root.Exists)
            {
                return new List<DirectoryInfo>();
            }

            return root.GetDirectories();
        }

        private DirectoryInfo GetOldestDayFolder(string rootPath)
        {
            DirectoryInfo oldestFolder = null;
            foreach (DirectoryInfo dayFolder in GetDayFolders(rootPath))
            {
                if (oldestFolder == null || string.Compare(dayFolder.Name, oldestFolder.Name, StringComparison.Ordinal) < 0)
                {
                    oldestFolder = dayFolder;
                }
            }

            return oldestFolder;
        }

        private void DeleteDirectory(DirectoryInfo directory)
        {
            try
            {
                directory.Delete(true);
            }
            catch
            {
                // 로그 삭제 실패가 검사 흐름을 막으면 안 되므로 운영 로그 정책 확정 전까지 무시합니다.
            }
        }
    }
}
