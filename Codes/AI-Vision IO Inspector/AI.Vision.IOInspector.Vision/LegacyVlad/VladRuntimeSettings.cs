using AI.Vision.IOInspector.Infrastructure;
using System;
using System.Globalization;
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
            CudaCacheDirectoryPath = @"%LOCALAPPDATA%\AI-Vision IO Inspector\CudaCache";
            StudyDirectoryPath = @"C:\Project\Study";
            StudyBatchFilePath = @"C:\Project\Study\Study.bat";
            UseSeparateVladRegistration = false;
            UseTestResultJson = false;
            EnableRtspCallbackRegistration = true;
            PersistentCaptureChannels = string.Empty;
            CustomRegistrationTimeoutMilliseconds = 150000;
            UnregistrationTimeoutMilliseconds = 30000;
            LogRetentionDays = ApplicationLogFileResolver.DefaultRetentionDays;
        }

        public string VladSdkDllPath { get; set; }

        public string CudaDllDirectoryPaths { get; set; }

        /// <summary>
        /// VLAD_SDK.dll 내부 TensorFlow/CUDA가 컴파일한 PTX 커널을 캐시하는 폴더입니다.
        /// 이 값이 없으면 GPU 커널을 처음 쓸 때마다 ptxas.exe가 매번 새로 컴파일해 호출이 느려집니다.
        /// (cuda-ptxas-runtime-2026-07-20.md 참고, 코드 이관 과정에서 유실되어 2026-08-07에 복원)
        /// </summary>
        public string CudaCacheDirectoryPath { get; set; }

        public string StudyDirectoryPath { get; set; }

        public string StudyBatchFilePath { get; set; }

        /// <summary>
        /// 프로그램 시작 시 RTSP 채널을 VLAD SDK에 등록해 프레임 callback을 계속 받을지 여부입니다.
        ///
        /// 이 등록은 채널마다 메인 스트림 연결을 하나씩 상시로 붙듭니다.
        /// 그런데 검사 캡처는 채널 해상도가 callback 버퍼(1920x1080)를 넘으면
        /// callback을 쓰지 않고 RTSP 원본을 직접 캡처하므로, 현장처럼 6채널이 모두
        /// 1920x1080을 초과하는 구성에서는 이 연결이 <b>복구 경로로만</b> 쓰입니다.
        ///
        /// NVR의 클라이언트 출력 대역폭이 제한적인 현장(예: 50Mbps)에서는
        /// 이 상시 연결이 화면 스트리밍과 검사 캡처가 쓸 대역폭을 함께 잠식합니다.
        /// 대역폭이 원인인지 확인하거나 회수하려면 false로 두고 비교합니다.
        ///
        /// false로 두면 원본 캡처 실패 시 복구할 callback 캐시가 없어
        /// 해당 채널은 검정 이미지로 저장됩니다. 검사 자체는 계속 진행됩니다.
        /// </summary>
        public bool EnableRtspCallbackRegistration { get; set; }

        /// <summary>
        /// 검사 캡처를 상시 연결로 처리할 채널 목록입니다.
        ///
        /// 기존 방식은 검사할 때마다 RTSP 연결을 새로 열고 첫 키프레임을 기다립니다.
        /// 그 대기가 길어지면 실패하는데, 현장 로그에서 Top 24% / Thickness 17.7%로 나왔습니다.
        /// 여기에 지정한 채널은 프로그램 시작 시 ffmpeg를 띄워 최신 프레임을 계속 갱신하고,
        /// 검사 때는 그 파일을 복사만 하므로 연결 수립 대기가 사라집니다.
        ///
        ///   ""               사용하지 않습니다. 기존 방식 그대로입니다. (기본값)
        ///   "Top"            Top 채널만 상시 연결합니다. 나머지는 기존 방식이라 효과를 비교할 수 있습니다.
        ///   "Top,Thickness"  쉼표로 여러 채널을 지정합니다.
        ///   "ALL"            6채널 전부 상시 연결합니다.
        /// </summary>
        public string PersistentCaptureChannels { get; set; }

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

        /// <summary>
        /// VLAD_Custom_Registration 네이티브 호출의 최대 대기 시간입니다.
        /// 이 시간을 넘기면 네이티브 호출이 멈춘 것으로 보고 시간 초과 예외를 던져 시작 스레드가
        /// 무한 대기하지 않게 합니다(2026-08-07, 종료 후 즉시 재실행 시 무한 대기하던 문제 대응).
        /// </summary>
        public int CustomRegistrationTimeoutMilliseconds { get; set; }

        /// <summary>
        /// VLAD_Unregistration 네이티브 호출의 최대 대기 시간입니다.
        /// 이 시간을 넘기면 프로그램 종료(OnExit)가 무한 대기하지 않고 시간 초과로 처리를 이어갑니다.
        /// </summary>
        public int UnregistrationTimeoutMilliseconds { get; set; }

        /// <summary>
        /// DB\Logs의 일자별 로그 폴더와 RetentionLog의 일자별 파일을 보관하는 기간(일)입니다.
        /// 이 기간이 지난 로그는 프로그램 시작 시 삭제합니다. 0 이하이면 삭제하지 않습니다.
        /// </summary>
        public int LogRetentionDays { get; set; }

        public string ResolvedVladSdkDllPath { get; private set; }

        public string ResolvedVladSdkDirectoryPath { get; private set; }

        public string[] ResolvedCudaDllDirectoryPaths { get; private set; }

        public string ResolvedCudaCacheDirectoryPath { get; private set; }

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
                    settings.CudaCacheDirectoryPath = ExtractJsonText(text, "CudaCacheDirectoryPath", settings.CudaCacheDirectoryPath);
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
                    settings.EnableRtspCallbackRegistration = ExtractJsonBoolean(
                        text,
                        "EnableRtspCallbackRegistration",
                        settings.EnableRtspCallbackRegistration);
                    settings.PersistentCaptureChannels = ExtractJsonText(
                        text,
                        "PersistentCaptureChannels",
                        settings.PersistentCaptureChannels);
                    settings.CustomRegistrationTimeoutMilliseconds = ExtractJsonInt32(
                        text,
                        "CustomRegistrationTimeoutMilliseconds",
                        settings.CustomRegistrationTimeoutMilliseconds);
                    settings.UnregistrationTimeoutMilliseconds = ExtractJsonInt32(
                        text,
                        "UnregistrationTimeoutMilliseconds",
                        settings.UnregistrationTimeoutMilliseconds);
                    settings.LogRetentionDays = ExtractJsonInt32(
                        text,
                        "LogRetentionDays",
                        settings.LogRetentionDays);
                }
                catch
                {
                    // 설정 파일을 읽지 못하면 기본값으로 계속 진행합니다.
                }
            }

            settings.ResolvedVladSdkDllPath = ResolvePath(executableDirectoryPath, settings.VladSdkDllPath);
            settings.ResolvedVladSdkDirectoryPath = Path.GetDirectoryName(settings.ResolvedVladSdkDllPath);
            settings.ResolvedCudaDllDirectoryPaths = ResolvePathList(executableDirectoryPath, settings.CudaDllDirectoryPaths);
            settings.ResolvedCudaCacheDirectoryPath = ResolvePath(executableDirectoryPath, settings.CudaCacheDirectoryPath);
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
            ApplyCudaCacheDirectory();
        }

        /// <summary>
        /// VLAD_Custom_Registration 이전에 PTX 커널 캐시 폴더를 만들고 CUDA_CACHE_PATH로 지정합니다.
        /// 이 값이 없으면 TensorFlow/CUDA가 GPU 커널을 처음 쓸 때마다 ptxas.exe로 새로 컴파일해 호출이 느려집니다.
        /// </summary>
        private void ApplyCudaCacheDirectory()
        {
            if (string.IsNullOrWhiteSpace(ResolvedCudaCacheDirectoryPath))
            {
                return;
            }

            try
            {
                Directory.CreateDirectory(ResolvedCudaCacheDirectoryPath);
                Environment.SetEnvironmentVariable("CUDA_CACHE_PATH", ResolvedCudaCacheDirectoryPath);
            }
            catch
            {
                // 캐시 폴더 생성 실패가 VLAD 등록 흐름을 막으면 안 됩니다. 이 경우 CUDA 기본 캐시 경로를 사용합니다.
            }
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
        /// 단순 런타임 설정 파일의 정수 값을 읽습니다. 값이 없거나 정수로 변환할 수 없으면 기본값을 사용합니다.
        /// </summary>
        private static int ExtractJsonInt32(string text, string key, int defaultValue)
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

            int valueEnd = valueStart;
            while (valueEnd < text.Length && (char.IsDigit(text[valueEnd]) || text[valueEnd] == '-'))
            {
                valueEnd++;
            }

            int parsedValue;
            if (valueEnd > valueStart &&
                int.TryParse(text.Substring(valueStart, valueEnd - valueStart), NumberStyles.Integer, CultureInfo.InvariantCulture, out parsedValue))
            {
                return parsedValue;
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
