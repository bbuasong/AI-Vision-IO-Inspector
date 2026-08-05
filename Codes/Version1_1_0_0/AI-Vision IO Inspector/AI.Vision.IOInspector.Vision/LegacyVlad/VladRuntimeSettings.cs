using AI.Vision.IOInspector.Infrastructure;
using System;
using System.IO;
using System.Text;

namespace AI.Vision.IOInspector.Vision.LegacyVlad
{
    /// <summary>
    /// VLAD SDK DLL과 이미지 학습 배치 파일처럼 현장 PC마다 달라질 수 있는 런타임 경로를 읽습니다.
    /// 기존 CFG\Config.json은 VLAD 카메라/모델 포맷을 유지해야 하므로 별도 파일로 분리합니다.
    /// </summary>
    public class VladRuntimeSettings
    {
        private const string SettingsFileName = "VladRuntimeSettings.json";

        public VladRuntimeSettings()
        {
            VladSdkDllPath = @"Native\VLAD\VLAD_SDK.dll";
            CudaDllDirectoryPaths = string.Empty;
            StudyDirectoryPath = @"C:\Project\Study";
            StudyBatchFilePath = @"C:\Project\Study\Study.bat";
            UseSeparateVladRegistration = false;
            UseTestResultJson = false;
        }

        public string VladSdkDllPath { get; set; }

        public string CudaDllDirectoryPaths { get; set; }

        public string StudyDirectoryPath { get; set; }

        public string StudyBatchFilePath { get; set; }

        /// <summary>
        /// 전체 이미지와 Crop 이미지에 대해 VLAD_Custom_Registration을 실제로 두 번 호출할지 여부입니다.
        /// 현재 배포된 VLAD_SDK.dll은 같은 프로세스의 두 번째 등록에서 네이티브 힙 손상이 발생하므로 기본값은 false입니다.
        /// AI 담당자가 제공하는 DLL에서 이중 등록을 지원한다고 확인한 뒤에만 CFG에서 true로 변경합니다.
        /// </summary>
        public bool UseSeparateVladRegistration { get; set; }

        /// <summary>
        /// VLAD DLL을 호출하지 않고 약속된 HD 결과 JSON을 주입해 결과 파싱 이후의
        /// 측정값 비교, 이력 저장, UI 표시 흐름을 검증할지 여부입니다.
        /// 실제 카메라/AI 검사에서는 반드시 false로 유지합니다.
        /// </summary>
        public bool UseTestResultJson { get; set; }

        public string ResolvedVladSdkDllPath { get; private set; }

        public string ResolvedVladSdkDirectoryPath { get; private set; }

        public string[] ResolvedCudaDllDirectoryPaths { get; private set; }

        public string ResolvedStudyDirectoryPath { get; private set; }

        public string ResolvedStudyBatchFilePath { get; private set; }

        public string SettingsFilePath { get; private set; }

        public static VladRuntimeSettings Load()
        {
            return Load(AppContext.BaseDirectory);
        }

        public static VladRuntimeSettings Load(string applicationRootPath)
        {
            // DLL 및 상대 경로는 EXE 기준으로 해석해야 배포 폴더만으로 실행할 수 있습니다.
            string executableDirectoryPath = RuntimeConfigurationPathResolver.GetExecutableDirectoryPath();
            VladRuntimeSettings settings = new VladRuntimeSettings();
            settings.SettingsFilePath = RuntimeConfigurationPathResolver.GetConfigFilePath(SettingsFileName);

            if (File.Exists(settings.SettingsFilePath))
            {
                try
                {
                    string text = File.ReadAllText(settings.SettingsFilePath, Encoding.UTF8);
                    settings.VladSdkDllPath = ExtractJsonText(text, "VladSdkDllPath", settings.VladSdkDllPath);
                    settings.CudaDllDirectoryPaths = ExtractJsonText(text, "CudaDllDirectoryPaths", settings.CudaDllDirectoryPaths);
                    settings.StudyDirectoryPath = ExtractJsonText(text, "StudyDirectoryPath", settings.StudyDirectoryPath);
                    settings.StudyBatchFilePath = ExtractJsonText(text, "StudyBatchFilePath", settings.StudyBatchFilePath);
                    settings.UseSeparateVladRegistration = ExtractJsonBoolean(
                        text,
                        "UseSeparateVladRegistration",
                        settings.UseSeparateVladRegistration);
                    settings.UseTestResultJson = ExtractJsonBoolean(
                        text,
                        "UseTestResultJson",
                        settings.UseTestResultJson);
                }
                catch
                {
                    // 설정 파일을 읽지 못하면 기본값으로 계속 진행합니다.
                }
            }

            settings.ResolvedVladSdkDllPath = ResolvePath(executableDirectoryPath, settings.VladSdkDllPath);
            settings.ResolvedVladSdkDirectoryPath = Path.GetDirectoryName(settings.ResolvedVladSdkDllPath);
            settings.ResolvedCudaDllDirectoryPaths = ResolvePathList(executableDirectoryPath, settings.CudaDllDirectoryPaths);
            settings.ResolvedStudyDirectoryPath = ResolvePath(executableDirectoryPath, settings.StudyDirectoryPath);
            settings.ResolvedStudyBatchFilePath = ResolvePath(executableDirectoryPath, settings.StudyBatchFilePath);
            return settings;
        }

        public void ApplyVladSdkDllDirectory()
        {
            if (!string.IsNullOrWhiteSpace(ResolvedVladSdkDirectoryPath))
            {
                VladNativeMethods.SetVladSdkDllDirectory(ResolvedVladSdkDirectoryPath);
            }

            ApplyCudaDllDirectoriesToProcessPath();
        }

        private void ApplyCudaDllDirectoriesToProcessPath()
        {
            if (ResolvedCudaDllDirectoryPaths == null || ResolvedCudaDllDirectoryPaths.Length == 0)
            {
                return;
            }

            string currentPath = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
            string updatedPath = currentPath;

            foreach (string directoryPath in ResolvedCudaDllDirectoryPaths)
            {
                if (string.IsNullOrWhiteSpace(directoryPath) || !Directory.Exists(directoryPath))
                {
                    continue;
                }

                if (ContainsPath(updatedPath, directoryPath))
                {
                    continue;
                }

                updatedPath = directoryPath + Path.PathSeparator + updatedPath;
            }

            if (!string.Equals(currentPath, updatedPath, StringComparison.Ordinal))
            {
                Environment.SetEnvironmentVariable("PATH", updatedPath);
            }
        }

        private static string ResolvePath(string executableDirectoryPath, string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return string.Empty;
            }

            string normalizedPath = Environment.ExpandEnvironmentVariables(path.Trim())
                .Replace('/', Path.DirectorySeparatorChar);
            if (!Path.IsPathRooted(normalizedPath))
            {
                normalizedPath = Path.Combine(executableDirectoryPath, normalizedPath);
            }

            return Path.GetFullPath(normalizedPath);
        }

        private static string[] ResolvePathList(string executableDirectoryPath, string paths)
        {
            if (string.IsNullOrWhiteSpace(paths))
            {
                return new string[0];
            }

            string[] values = paths.Split(new char[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
            for (int index = 0; index < values.Length; index++)
            {
                values[index] = ResolvePath(executableDirectoryPath, values[index]);
            }

            return values;
        }

        private static bool ContainsPath(string pathList, string directoryPath)
        {
            if (string.IsNullOrWhiteSpace(pathList) || string.IsNullOrWhiteSpace(directoryPath))
            {
                return false;
            }

            string normalizedDirectoryPath = Path.GetFullPath(directoryPath)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            string[] values = pathList.Split(new char[] { Path.PathSeparator }, StringSplitOptions.RemoveEmptyEntries);
            foreach (string value in values)
            {
                string normalizedValue;
                try
                {
                    normalizedValue = Path.GetFullPath(Environment.ExpandEnvironmentVariables(value.Trim()))
                        .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                }
                catch
                {
                    continue;
                }

                if (string.Equals(normalizedValue, normalizedDirectoryPath, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private static string ExtractJsonText(string text, string key, string defaultValue)
        {
            if (string.IsNullOrWhiteSpace(text) || string.IsNullOrWhiteSpace(key))
            {
                return defaultValue;
            }

            string pattern = "\"" + key + "\"";
            int keyIndex = text.IndexOf(pattern, StringComparison.OrdinalIgnoreCase);
            if (keyIndex < 0)
            {
                return defaultValue;
            }

            int colonIndex = text.IndexOf(':', keyIndex);
            if (colonIndex < 0)
            {
                return defaultValue;
            }

            int firstQuoteIndex = text.IndexOf('"', colonIndex + 1);
            if (firstQuoteIndex < 0)
            {
                return defaultValue;
            }

            int currentIndex = firstQuoteIndex + 1;
            bool isEscaped = false;
            while (currentIndex < text.Length)
            {
                char current = text[currentIndex];
                if (current == '"' && !isEscaped)
                {
                    string rawValue = text.Substring(firstQuoteIndex + 1, currentIndex - firstQuoteIndex - 1);
                    return rawValue.Replace("\\\\", "\\");
                }

                isEscaped = current == '\\' && !isEscaped;
                if (current != '\\')
                {
                    isEscaped = false;
                }

                currentIndex++;
            }

            return defaultValue;
        }

        /// <summary>
        /// 단순 런타임 설정 파일의 true/false 값을 읽습니다.
        /// JSON 전체를 다시 직렬화하지 않아 사용자가 관리하는 다른 설정 항목을 변경하지 않습니다.
        /// </summary>
        private static bool ExtractJsonBoolean(string text, string key, bool defaultValue)
        {
            if (string.IsNullOrWhiteSpace(text) || string.IsNullOrWhiteSpace(key))
            {
                return defaultValue;
            }

            string pattern = "\"" + key + "\"";
            int keyIndex = text.IndexOf(pattern, StringComparison.OrdinalIgnoreCase);
            if (keyIndex < 0)
            {
                return defaultValue;
            }

            int colonIndex = text.IndexOf(':', keyIndex);
            if (colonIndex < 0)
            {
                return defaultValue;
            }

            int valueStart = colonIndex + 1;
            while (valueStart < text.Length && char.IsWhiteSpace(text[valueStart]))
            {
                valueStart++;
            }

            if (text.IndexOf("true", valueStart, StringComparison.OrdinalIgnoreCase) == valueStart)
            {
                return true;
            }

            if (text.IndexOf("false", valueStart, StringComparison.OrdinalIgnoreCase) == valueStart)
            {
                return false;
            }

            return defaultValue;
        }
    }
}
