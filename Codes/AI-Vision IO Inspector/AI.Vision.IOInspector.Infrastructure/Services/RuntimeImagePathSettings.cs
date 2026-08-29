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

        /// <summary>
        /// 기준 이미지의 절대 경로를 DB 에 담을 형태로 바꿉니다.
        ///
        /// <para>
        /// 절대 경로를 그대로 담았더니 컴퓨터나 드라이브가 바뀔 때마다 전부 깨졌습니다.
        /// 사무실에서 저장한 행은 C: 를, 현장에서 저장한 행은 E: 를 가리켜 서로의 DB 를
        /// 읽을 수 없었습니다. 루트는 Config 의 IMAGE_PATH 가 이미 알고 있으므로 DB 에는
        /// 루트 아래 상대 위치만 REFERENCE:\ 머리를 붙여 담고, 읽을 때 그 컴퓨터의
        /// 루트를 붙여 되살립니다(ResolveImageFilePath 가 그 일을 합니다).
        /// </para>
        ///
        /// <para>
        /// 루트 밖의 경로는 상대로 만들 수 없으므로 그대로 돌려줍니다. 예전 형식의 행도
        /// 같은 까닭으로 읽는 쪽에서 그대로 통과합니다.
        /// </para>
        /// </summary>
        /// <summary>
        /// DB 에 담긴 경로를 이 컴퓨터의 절대 경로로 되살립니다.
        ///
        /// <para>
        /// ResolveImageFilePath 와 달리 파일이 실제로 있는지 보지 않습니다. DB 를 읽는 경계에서는
        /// 파일이 아직 안 옮겨졌더라도 "이 컴퓨터라면 여기 있어야 한다"는 절대 경로로 풀어 두어야,
        /// 메모리 안에서는 언제나 절대 경로 하나로 통합니다. 없으면 없는 대로 화면이 빈 칸을
        /// 보여 주면 됩니다.
        /// </para>
        /// </summary>
        public string FromStorableImagePath(string storedFilePath)
        {
            if (string.IsNullOrWhiteSpace(storedFilePath))
            {
                return storedFilePath;
            }

            string trimmedPath = storedFilePath.Trim();
            if (!trimmedPath.StartsWith(ReferencePathPrefix, StringComparison.OrdinalIgnoreCase))
            {
                return trimmedPath;
            }

            string relativePath = trimmedPath.Substring(ReferencePathPrefix.Length)
                .TrimStart('\\', '/')
                .Replace('/', Path.DirectorySeparatorChar);
            if (string.IsNullOrWhiteSpace(relativePath))
            {
                return trimmedPath;
            }

            return Path.Combine(ReferenceImageRootPath, relativePath);
        }

        public string ToStorableImagePath(string absoluteFilePath)
        {
            if (string.IsNullOrWhiteSpace(absoluteFilePath))
            {
                return absoluteFilePath;
            }

            string trimmedPath = absoluteFilePath.Trim();
            if (trimmedPath.StartsWith(ReferencePathPrefix, StringComparison.OrdinalIgnoreCase))
            {
                return trimmedPath;
            }

            try
            {
                string rootPath = Path.GetFullPath(ReferenceImageRootPath)
                    .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                string fullPath = Path.GetFullPath(trimmedPath);

                if (fullPath.StartsWith(rootPath + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                {
                    string relativePath = fullPath.Substring(rootPath.Length)
                        .TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                    return ReferencePathPrefix +
                           relativePath.Replace(Path.AltDirectorySeparatorChar, '\\')
                                       .Replace(Path.DirectorySeparatorChar, '\\');
                }
            }
            catch (ArgumentException)
            {
            }
            catch (NotSupportedException)
            {
            }
            catch (PathTooLongException)
            {
            }

            return trimmedPath;
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
