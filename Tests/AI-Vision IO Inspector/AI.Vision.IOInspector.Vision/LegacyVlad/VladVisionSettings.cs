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

        public int GpuId { get; set; }

        public float Threshold { get; set; }

        public static VladVisionSettings Load(string applicationRootPath)
        {
            VladVisionSettings settings = new VladVisionSettings();
            settings.RootName = "CAM";
            settings.SiteName = "HD";
            settings.GpuId = 0;
            settings.Threshold = 0.5f;

            string projectRootPath = ProjectDataRootResolver.Resolve(applicationRootPath);
            string configPath = Path.Combine(projectRootPath, "CFG", "Config.json");

            if (File.Exists(configPath))
            {
                string text = File.ReadAllText(configPath);

                settings.RootName = ExtractJsonText(text, "LAST_MODE", settings.RootName);
                settings.SiteName = ExtractJsonText(text, "LAST_USER", settings.SiteName);
                settings.ModelPath = ExtractJsonText(text, "MODEL", settings.ModelPath);

                settings.RootName = ExtractJsonText(text, "ROOT_NAME", settings.RootName);
                settings.SiteName = ExtractJsonText(text, "SITE_NAME", settings.SiteName);
                settings.Threshold = ExtractJsonFloat(text, "THRESHOLD", settings.Threshold);
            }

            ApplyEnvironmentSettings(settings);

            return settings;
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
    }
}
