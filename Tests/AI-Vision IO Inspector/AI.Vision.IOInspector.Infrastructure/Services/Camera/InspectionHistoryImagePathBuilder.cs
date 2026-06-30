using System;
using System.IO;
using AI.Vision.IOInspector.Domain.Models;
using AI.Vision.IOInspector.Infrastructure.Services;

namespace AI.Vision.IOInspector.Infrastructure.Services.Camera
{
    /// <summary>
    /// 검사 후 촬영 이미지를 DB\History 아래에 분산 저장하기 위한 경로를 생성합니다.
    /// 한 폴더에 파일이 과도하게 쌓이지 않도록 연월일, 시간, 구분 폴더를 나누어 관리합니다.
    /// </summary>
    public static class InspectionHistoryImagePathBuilder
    {
        public static string BuildCaptureFilePath(
            string rootPath,
            CameraChannelConfig channel,
            Part part,
            string extension,
            DateTime capturedAt)
        {
            if (string.IsNullOrWhiteSpace(rootPath))
            {
                throw new ArgumentException("Root path is required.", "rootPath");
            }

            if (channel == null)
            {
                throw new ArgumentNullException("channel");
            }

            RuntimeImagePathSettings pathSettings = RuntimeImagePathSettings.Load(rootPath);
            string normalizedExtension = NormalizeExtension(extension);
            string yearFolder = capturedAt.ToString("yyyy");
            string monthFolder = capturedAt.ToString("MM");
            string dayFolder = capturedAt.ToString("dd");
            string hourFolder = capturedAt.ToString("HH");
            string categoryFolder = SanitizePathSegment(GetCategoryCode(part));
            string partFolder = SanitizePathSegment(GetPartNo(part));

            string hourFolderPath = Path.Combine(
                pathSettings.HistoryImageRootPath,
                yearFolder,
                monthFolder,
                dayFolder,
                hourFolder);
            EnsureInspectionHourFolders(hourFolderPath);

            string targetFolderPath = Path.Combine(
                hourFolderPath,
                "Image",
                categoryFolder,
                partFolder);
            string fileName = BuildFileName(part, channel, capturedAt, normalizedExtension);
            return Path.Combine(targetFolderPath, fileName);
        }

        private static string BuildFileName(Part part, CameraChannelConfig channel, DateTime capturedAt, string extension)
        {
            string partNo = SanitizePathSegment(part == null ? string.Empty : part.PartNo);
            string partName = SanitizePathSegment(part == null ? string.Empty : part.PartName);

            if (string.IsNullOrWhiteSpace(partNo))
            {
                partNo = "UNKNOWN_PARTNO";
            }

            if (string.IsNullOrWhiteSpace(partName))
            {
                partName = "UNKNOWN_PARTNAME";
            }

            string viewType = SanitizePathSegment(channel.ViewType.ToString());
            string testTime = capturedAt.ToString("HHmmssfff");
            return partNo + "_" + partName + "_" + viewType + "_" + testTime + extension;
        }

        private static string GetCategoryCode(Part part)
        {
            if (part != null && !string.IsNullOrWhiteSpace(part.CategoryCode))
            {
                return part.CategoryCode;
            }

            return "NO_CATEGORY";
        }

        private static string GetPartNo(Part part)
        {
            if (part != null && !string.IsNullOrWhiteSpace(part.PartNo))
            {
                return part.PartNo;
            }

            return "NO_PARTNO";
        }

        private static void EnsureInspectionHourFolders(string hourFolderPath)
        {
            if (string.IsNullOrWhiteSpace(hourFolderPath))
            {
                return;
            }

            Directory.CreateDirectory(Path.Combine(hourFolderPath, "History"));
            Directory.CreateDirectory(Path.Combine(hourFolderPath, "Image"));
            Directory.CreateDirectory(Path.Combine(hourFolderPath, "Log"));
        }

        private static string NormalizeExtension(string extension)
        {
            if (string.IsNullOrWhiteSpace(extension))
            {
                return ".bmp";
            }

            string normalizedExtension = extension.Trim();
            if (!normalizedExtension.StartsWith(".", StringComparison.Ordinal))
            {
                normalizedExtension = "." + normalizedExtension;
            }

            return normalizedExtension;
        }

        private static string SanitizePathSegment(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            string sanitized = value.Trim();
            foreach (char invalidCharacter in Path.GetInvalidFileNameChars())
            {
                sanitized = sanitized.Replace(invalidCharacter, '_');
            }

            while (sanitized.Contains("  "))
            {
                sanitized = sanitized.Replace("  ", " ");
            }

            if (sanitized.Length > 80)
            {
                sanitized = sanitized.Substring(0, 80).Trim();
            }

            return sanitized;
        }
    }
}
