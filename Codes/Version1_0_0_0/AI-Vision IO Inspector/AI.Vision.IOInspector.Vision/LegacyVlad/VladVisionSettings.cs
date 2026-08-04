using AI.Vision.IOInspector.Infrastructure;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AI.Vision.IOInspector.Vision.LegacyVlad
{
    public class VladVisionSettings
    {
        public string RootName { get; set; }

        public string SiteName { get; set; }

        public string ModelPath { get; set; }

        /// <summary>
        /// Crop 이미지 학습/추론용 모델 경로입니다. Config.json에 CROP_MODEL이 없으면 MODEL 경로를 함께 사용합니다.
        /// </summary>
        public string CroppedModelPath { get; set; }

        /// <summary>
        /// 전체 이미지 학습/추론용 모델 경로입니다. 기존 Config.json의 MODEL 값을 그대로 사용합니다.
        /// </summary>
        public string FullImageModelPath
        {
            get { return ModelPath; }
        }

        public int GpuId { get; set; }

        public float Threshold { get; set; }

        /// <summary>
        /// 현재 VLAD 설정을 읽은 Config.json 파일 경로입니다.
        /// 실행 파일과 같은 위치의 CFG\\Config.json만 사용합니다.
        /// </summary>
        public string ConfigFilePath { get; private set; }

        public static VladVisionSettings Load(string applicationRootPath)
        {
            VladVisionSettings settings = new VladVisionSettings();
            settings.RootName = "CAM";
            settings.SiteName = "HD";
            settings.GpuId = 0;
            settings.Threshold = 0.5f;

            // Config.json은 항상 현재 EXE와 같은 CFG 폴더에서만 읽습니다.
            string executableDirectoryPath = RuntimeConfigurationPathResolver.GetExecutableDirectoryPath();
            string configPath = RuntimeConfigurationPathResolver.GetConfigFilePath("Config.json");
            settings.ConfigFilePath = configPath;

            if (File.Exists(configPath))
            {
                string text = File.ReadAllText(configPath);

                settings.RootName = ExtractJsonText(text, "LAST_MODE", settings.RootName);
                settings.SiteName = ExtractJsonText(text, "LAST_USER", settings.SiteName);
                settings.ModelPath = ExtractJsonText(text, "MODEL", settings.ModelPath);
                settings.CroppedModelPath = ExtractJsonText(text, "CROP_MODEL", settings.CroppedModelPath);

                settings.RootName = ExtractJsonText(text, "ROOT_NAME", settings.RootName);
                settings.SiteName = ExtractJsonText(text, "SITE_NAME", settings.SiteName);
                settings.GpuId = ExtractJsonInt(text, "GPU_ID", settings.GpuId);
                settings.Threshold = ExtractJsonFloat(text, "THRESHOLD", settings.Threshold);
            }

            ApplyEnvironmentSettings(settings);
            settings.ModelPath = ResolveModelPath(executableDirectoryPath, settings.ModelPath);
            if (string.IsNullOrWhiteSpace(settings.CroppedModelPath))
            {
                settings.CroppedModelPath = settings.ModelPath;
            }
            else
            {
                settings.CroppedModelPath = ResolveModelPath(executableDirectoryPath, settings.CroppedModelPath);
            }

            return settings;
        }

        public string BuildModelPathMissingMessage()
        {
            if (string.IsNullOrWhiteSpace(ModelPath))
            {
                return "VLAD 모델 경로가 설정되어 있지 않습니다. CFG\\Config.json의 MODEL 또는 AI_VISION_VLAD_MODEL_PATH 환경변수를 실제 모델 폴더로 지정하세요.";
            }

            return "VLAD 모델 경로를 찾을 수 없습니다. 현재 설정: " + ModelPath + "\r\n" +
                   "CFG\\Config.json의 MODEL 값은 기존 VLAD_Ops 설정에서 가져온 값입니다. 현재 PC에 해당 드라이브/폴더가 없으면 AI_VISION_VLAD_MODEL_PATH 환경변수 또는 Config.json MODEL 값을 실제 모델 폴더로 변경하세요.";
        }

        private static void ApplyEnvironmentSettings(VladVisionSettings settings)
        {
            string modelPathFromEnvironment = Environment.GetEnvironmentVariable("AI_VISION_VLAD_MODEL_PATH");
            if (!string.IsNullOrWhiteSpace(modelPathFromEnvironment))
            {
                settings.ModelPath = modelPathFromEnvironment;
            }

            string siteNameFromEnvironment = Environment.GetEnvironmentVariable("AI_VISION_VLAD_SITE");
            if (!string.IsNullOrWhiteSpace(siteNameFromEnvironment))
            {
                settings.SiteName = siteNameFromEnvironment;
            }

            string rootNameFromEnvironment = Environment.GetEnvironmentVariable("AI_VISION_VLAD_ROOT");
            if (!string.IsNullOrWhiteSpace(rootNameFromEnvironment))
            {
                settings.RootName = rootNameFromEnvironment;
            }

            int gpuId;
            string gpuIdText = Environment.GetEnvironmentVariable("AI_VISION_VLAD_GPU");
            if (!string.IsNullOrWhiteSpace(gpuIdText) && int.TryParse(gpuIdText, out gpuId))
            {
                settings.GpuId = gpuId;
            }

            float threshold;
            string thresholdText = Environment.GetEnvironmentVariable("AI_VISION_VLAD_THRESHOLD");
            if (!string.IsNullOrWhiteSpace(thresholdText) &&
                float.TryParse(thresholdText, NumberStyles.Float, CultureInfo.InvariantCulture, out threshold))
            {
                settings.Threshold = threshold;
            }
        }

        private static string ResolveModelPath(string executableDirectoryPath, string modelPath)
        {
            if (string.IsNullOrWhiteSpace(modelPath))
            {
                return string.Empty;
            }

            string normalizedPath = Environment.ExpandEnvironmentVariables(modelPath.Trim())
                .Replace('/', Path.DirectorySeparatorChar);

            if (!Path.IsPathRooted(normalizedPath))
            {
                normalizedPath = Path.Combine(executableDirectoryPath, normalizedPath);
            }

            normalizedPath = Path.GetFullPath(normalizedPath);
            if (Directory.Exists(normalizedPath))
            {
                return normalizedPath;
            }

            string[] fallbackPaths = new string[]
            {
                Path.Combine(executableDirectoryPath, "Models", "VLAD"),
                Path.Combine(executableDirectoryPath, "RuntimeData", "Models", "VLAD"),
                Path.Combine(executableDirectoryPath, "Native", "VLAD", "Model"),
                Path.Combine(executableDirectoryPath, "Native", "VLAD", "Models")
            };

            foreach (string fallbackPath in fallbackPaths)
            {
                string fullFallbackPath = Path.GetFullPath(fallbackPath);
                if (Directory.Exists(fullFallbackPath))
                {
                    return fullFallbackPath;
                }
            }

            return normalizedPath;
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

            int secondQuoteIndex = text.IndexOf('"', firstQuoteIndex + 1);
            if (secondQuoteIndex < 0)
            {
                return defaultValue;
            }

            return text.Substring(firstQuoteIndex + 1, secondQuoteIndex - firstQuoteIndex - 1);
        }

        private static float ExtractJsonFloat(string text, string key, float defaultValue)
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
            while (valueEnd < text.Length &&
                   (char.IsDigit(text[valueEnd]) || text[valueEnd] == '.' || text[valueEnd] == '-'))
            {
                valueEnd++;
            }

            if (valueEnd <= valueStart)
            {
                return defaultValue;
            }

            float value;
            string valueText = text.Substring(valueStart, valueEnd - valueStart);
            if (!float.TryParse(valueText, NumberStyles.Float, CultureInfo.InvariantCulture, out value))
            {
                return defaultValue;
            }

            return value;
        }

        private static int ExtractJsonInt(string text, string key, int defaultValue)
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

            if (valueEnd <= valueStart)
            {
                return defaultValue;
            }

            int value;
            string valueText = text.Substring(valueStart, valueEnd - valueStart);
            if (!int.TryParse(valueText, NumberStyles.Integer, CultureInfo.InvariantCulture, out value))
            {
                return defaultValue;
            }

            return value;
        }
    }
}
