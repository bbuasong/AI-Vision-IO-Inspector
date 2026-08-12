using System;
using System.IO;
using AI.Vision.IOInspector.Domain.Models;
using AI.Vision.IOInspector.Infrastructure.Services;

namespace AI.Vision.IOInspector.Infrastructure.Services.Camera
{
    /// <summary>
    /// 검사 후 촬영 이미지를 Config.json OUTPUT_PATH 아래에 분산 저장하기 위한 경로를 생성합니다.
    /// 한 폴더에 파일이 과도하게 쌓이지 않도록 연월일, 시간, 구분 폴더를 나누어 관리합니다.
    /// </summary>
    public static class InspectionHistoryImagePathBuilder
    {
        /// <summary>
        /// 검사 촬영 이미지의 저장 경로를 만듭니다.
        ///
        /// <paramref name="inspectionStartedAt"/>은 검사 한 번을 구분하는 시각입니다.
        /// 6방향 이미지가 한 폴더에 모이도록 호출자가 검사 시작 시점에 한 번 정해서
        /// 모든 채널에 같은 값을 넘겨야 합니다. 채널별 촬영 시각을 넘기면
        /// 초가 넘어가는 순간 이미지가 서로 다른 폴더로 갈라집니다.
        /// </summary>
        public static string BuildCaptureFilePath(
            string rootPath,
            CameraChannelConfig channel,
            Part part,
            string extension,
            DateTime inspectionStartedAt)
        {
            if (string.IsNullOrWhiteSpace(rootPath))
            {
                throw new ArgumentException("Root path is required.", "rootPath");
            }

            if (channel == null)
            {
                throw new ArgumentNullException("channel");
            }

            string targetFolderPath = BuildInspectionFolderPath(rootPath, part, inspectionStartedAt);
            string fileName = InspectionImageFileNamePolicy.BuildCaptureFileName(
                channel.ViewType,
                part == null ? string.Empty : part.PartNo,
                part == null ? string.Empty : part.PartName,
                inspectionStartedAt,
                NormalizeExtension(extension));

            return Path.Combine(targetFolderPath, fileName);
        }

        /// <summary>
        /// 한 번의 검사 이미지가 모두 들어갈 폴더 경로를 만듭니다.
        /// {OUTPUT_PATH}\yyyy\MM\dd\HH\Image\{분류코드}\{품번}\{HH-mm-ss}
        /// </summary>
        public static string BuildInspectionFolderPath(
            string rootPath,
            Part part,
            DateTime inspectionStartedAt)
        {
            RuntimeImagePathSettings pathSettings = RuntimeImagePathSettings.Load(rootPath);
            string categoryFolder = SanitizePathSegment(GetCategoryCode(part));
            string partFolder = SanitizePathSegment(GetPartNo(part));

            string hourFolderPath = Path.Combine(
                pathSettings.HistoryImageRootPath,
                inspectionStartedAt.ToString("yyyy"),
                inspectionStartedAt.ToString("MM"),
                inspectionStartedAt.ToString("dd"),
                inspectionStartedAt.ToString("HH"));
            EnsureInspectionHourFolders(hourFolderPath);

            return Path.Combine(
                hourFolderPath,
                "Image",
                categoryFolder,
                partFolder,
                InspectionImageFileNamePolicy.BuildInspectionFolderName(inspectionStartedAt));
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
