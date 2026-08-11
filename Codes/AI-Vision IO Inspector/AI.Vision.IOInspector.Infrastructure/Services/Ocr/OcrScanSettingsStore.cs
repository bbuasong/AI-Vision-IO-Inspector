using System;
using System.IO;
using System.Text.Json;
using AI.Vision.IOInspector.Application.Models;

namespace AI.Vision.IOInspector.Infrastructure.Services.Ocr
{
    /// <summary>
    /// 실행 파일과 같은 루트의 CFG 폴더에 OCR 설정을 보관합니다.
    /// 배포 PC에서도 개발 경로가 아닌 실행 폴더의 설정만 사용합니다.
    /// </summary>
    internal class OcrScanSettingsStore
    {
        private readonly string _settingsPath;
        private readonly JsonSerializerOptions _jsonOptions;

        public OcrScanSettingsStore(string applicationRootPath)
        {
            string projectRootPath = ProjectDataRootResolver.Resolve(applicationRootPath);
            _settingsPath = Path.Combine(projectRootPath, "CFG", "OcrScannerSettings.json");
            _jsonOptions = new JsonSerializerOptions();
            _jsonOptions.WriteIndented = true;
        }

        public OcrScanConfiguration Load()
        {
            if (!File.Exists(_settingsPath))
            {
                return new OcrScanConfiguration();
            }

            try
            {
                OcrScanConfiguration configuration = JsonSerializer.Deserialize<OcrScanConfiguration>(File.ReadAllText(_settingsPath), _jsonOptions);
                return Normalize(configuration);
            }
            catch
            {
                return new OcrScanConfiguration();
            }
        }

        public void Save(OcrScanConfiguration configuration)
        {
            string folderPath = Path.GetDirectoryName(_settingsPath);
            if (!string.IsNullOrWhiteSpace(folderPath))
            {
                Directory.CreateDirectory(folderPath);
            }

            File.WriteAllText(_settingsPath, JsonSerializer.Serialize(Normalize(configuration), _jsonOptions));
        }

        private static OcrScanConfiguration Normalize(OcrScanConfiguration configuration)
        {
            if (configuration == null)
            {
                configuration = new OcrScanConfiguration();
            }

            if (configuration.ResolutionDpi != 300 && configuration.ResolutionDpi != 400 && configuration.ResolutionDpi != 600)
            {
                configuration.ResolutionDpi = 400;
            }

            if (!string.Equals(configuration.ColorMode, "bw", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(configuration.ColorMode, "color", StringComparison.OrdinalIgnoreCase))
            {
                configuration.ColorMode = "gray";
            }

            return configuration;
        }
    }
}
