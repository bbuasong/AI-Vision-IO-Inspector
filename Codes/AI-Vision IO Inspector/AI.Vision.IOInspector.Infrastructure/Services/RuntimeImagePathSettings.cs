using System;
using System.IO;
using System.Text.Json;
using AI.Vision.IOInspector.Infrastructure;

namespace AI.Vision.IOInspector.Infrastructure.Services
{
    /// <summary>
    /// VLAD_Ops의 Config.json 경로 정책을 현재 프로그램의 이미지 저장 경로로 연결합니다.
    /// Config.json에 IMAGE_PATH/OUTPUT_PATH가 지정되어 있으면 해당 경로를 저장 위치로 사용합니다.
    /// </summary>
    public class RuntimeImagePathSettings
    {
        public const string ReferencePathPrefix = "REFERENCE:\\\\";

        private RuntimeImagePathSettings()
        {
        }

        /// <summary>
        /// 기존 호출부와의 호환을 위해 유지하는 런타임 기준 폴더입니다.
        /// 배포 환경에서도 개발 프로젝트가 아니라 실행 중인 EXE 폴더를 사용합니다.
        /// </summary>
        public string ProjectRootPath { get; private set; }

        public string ReferenceImageRootPath { get; private set; }

        public string HistoryImageRootPath { get; private set; }

        /// <summary>
        /// 부품등록 OCR이 DB 저장 전까지 이미지와 OCR JSON을 임시로 보관하는 루트 경로입니다.
        /// </summary>
        public string OcrTemporaryRootPath { get; private set; }

        public static RuntimeImagePathSettings Load(string applicationRootPath)
        {
            string executableDirectoryPath = RuntimeConfigurationPathResolver.GetExecutableDirectoryPath();
            string fallbackReferencePath = Path.Combine(executableDirectoryPath, "DB", "Image");
            string fallbackHistoryPath = Path.Combine(executableDirectoryPath, "DB", "Inspection_Data");
            string fallbackOcrTemporaryPath = Path.Combine(executableDirectoryPath, "DB", "Ocr_Temp");

            string configPath = RuntimeConfigurationPathResolver.GetConfigFilePath("Config.json");
            string configuredReferencePath = string.Empty;
            string configuredHistoryPath = string.Empty;
            string configuredOcrTemporaryPath = string.Empty;
            if (File.Exists(configPath))
            {
                string jsonText = File.ReadAllText(configPath);
                configuredReferencePath = ExtractJsonText(jsonText, "IMAGE_PATH");
                configuredHistoryPath = ExtractJsonText(jsonText, "OUTPUT_PATH");
                configuredOcrTemporaryPath = ExtractJsonText(jsonText, "OCR_PATH");
            }

            string referencePathFromEnvironment = Environment.GetEnvironmentVariable("AI_VISION_REFERENCE_IMAGE_PATH");
            if (!string.IsNullOrWhiteSpace(referencePathFromEnvironment))
            {
                configuredReferencePath = referencePathFromEnvironment;
            }

            string historyPathFromEnvironment = Environment.GetEnvironmentVariable("AI_VISION_HISTORY_IMAGE_PATH");
            if (!string.IsNullOrWhiteSpace(historyPathFromEnvironment))
            {
                configuredHistoryPath = historyPathFromEnvironment;
            }

            string ocrTemporaryPathFromEnvironment = Environment.GetEnvironmentVariable("AI_VISION_OCR_PATH");
            if (!string.IsNullOrWhiteSpace(ocrTemporaryPathFromEnvironment))
            {
                configuredOcrTemporaryPath = ocrTemporaryPathFromEnvironment;
            }

            RuntimeImagePathSettings settings = new RuntimeImagePathSettings();
            settings.ProjectRootPath = executableDirectoryPath;
            settings.ReferenceImageRootPath = ResolveConfiguredRootPath(executableDirectoryPath, configuredReferencePath, fallbackReferencePath);
            settings.HistoryImageRootPath = ResolveConfiguredRootPath(executableDirectoryPath, configuredHistoryPath, fallbackHistoryPath);
            settings.OcrTemporaryRootPath = ResolveConfiguredRootPath(executableDirectoryPath, configuredOcrTemporaryPath, fallbackOcrTemporaryPath);
            return settings;
        }

        public string ResolveImageFilePath(string imagePath)
        {
            if (string.IsNullOrWhiteSpace(imagePath))
            {
                return string.Empty;
            }

            string trimmedPath = imagePath.Trim();
            if (File.Exists(trimmedPath))
            {
                return trimmedPath;
            }

            string referencePath = ResolveReferencePath(trimmedPath);
            if (!string.IsNullOrWhiteSpace(referencePath) && File.Exists(referencePath))
            {
                return referencePath;
            }

            if (!Path.IsPathRooted(trimmedPath))
            {
                string projectRelativePath = Path.Combine(ProjectRootPath, trimmedPath);
                if (File.Exists(projectRelativePath))
                {
                    return projectRelativePath;
                }
            }

            return trimmedPath;
        }

        public bool ImageFileExists(string imagePath)
        {
            string resolvedPath = ResolveImageFilePath(imagePath);
            return !string.IsNullOrWhiteSpace(resolvedPath) && File.Exists(resolvedPath);
        }

        public string BuildReferenceDisplayPath(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
            {
                return "-";
            }

            string resolvedPath = ResolveImageFilePath(filePath);
            string folderPath = Path.GetDirectoryName(resolvedPath);
            if (string.IsNullOrWhiteSpace(folderPath))
            {
                return filePath;
            }

            string rootPath = Path.GetFullPath(ReferenceImageRootPath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            string fullFolderPath = Path.GetFullPath(folderPath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            if (fullFolderPath.StartsWith(rootPath, StringComparison.OrdinalIgnoreCase))
            {
                string relativePath = fullFolderPath.Substring(rootPath.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                return ReferencePathPrefix + relativePath.Replace(Path.AltDirectorySeparatorChar, '\\').Replace(Path.DirectorySeparatorChar, '\\');
            }

            return folderPath;
        }

        private static string ResolveConfiguredRootPath(string executableDirectoryPath, string configuredPath, string fallbackPath)
        {
            string candidatePath = configuredPath;
            if (string.IsNullOrWhiteSpace(candidatePath))
            {
                return Path.GetFullPath(fallbackPath);
            }

            candidatePath = candidatePath.Trim().Replace('/', Path.DirectorySeparatorChar);
            if (!Path.IsPathRooted(candidatePath))
            {
                candidatePath = Path.Combine(executableDirectoryPath, candidatePath);
            }

            return Path.GetFullPath(candidatePath);
        }

        private string ResolveReferencePath(string imagePath)
        {
            if (!imagePath.StartsWith(ReferencePathPrefix, StringComparison.OrdinalIgnoreCase))
            {
                return string.Empty;
            }

            string relativePath = imagePath.Substring(ReferencePathPrefix.Length)
                .TrimStart('\\', '/')
                .Replace('/', Path.DirectorySeparatorChar)
                .Replace('\\', Path.DirectorySeparatorChar);

            if (string.IsNullOrWhiteSpace(relativePath))
            {
                return string.Empty;
            }

            return Path.Combine(ReferenceImageRootPath, relativePath);
        }

        private static string ExtractJsonText(string jsonText, string key)
        {
            if (string.IsNullOrWhiteSpace(jsonText) || string.IsNullOrWhiteSpace(key))
            {
                return string.Empty;
            }

            try
            {
                using (JsonDocument document = JsonDocument.Parse(jsonText))
                {
                    return FindStringProperty(document.RootElement, key);
                }
            }
            catch (JsonException)
            {
                return string.Empty;
            }
        }

        private static string FindStringProperty(JsonElement element, string key)
        {
            if (element.ValueKind == JsonValueKind.Object)
            {
                foreach (JsonProperty property in element.EnumerateObject())
                {
                    if (string.Equals(property.Name, key, StringComparison.OrdinalIgnoreCase) &&
                        property.Value.ValueKind == JsonValueKind.String)
                    {
                        return property.Value.GetString();
                    }

                    string nestedValue = FindStringProperty(property.Value, key);
                    if (!string.IsNullOrWhiteSpace(nestedValue))
                    {
                        return nestedValue;
                    }
                }
            }

            if (element.ValueKind == JsonValueKind.Array)
            {
                foreach (JsonElement item in element.EnumerateArray())
                {
                    string nestedValue = FindStringProperty(item, key);
                    if (!string.IsNullOrWhiteSpace(nestedValue))
                    {
                        return nestedValue;
                    }
                }
            }

            return string.Empty;
        }
    }
}
