using System;
using System.IO;
using System.Text.Json;

namespace AI.Vision.IOInspector.Infrastructure.Services.Retention
{
    /// <summary>
    /// 검사 데이터 자동삭제 옵션을 CFG\InspectionDataRetention.json 파일로 저장합니다.
    /// VLAD Config.json 포맷은 유지해야 하므로 별도 설정 파일로 분리합니다.
    /// </summary>
    public class InspectionDataRetentionSettingsStore
    {
        private readonly string _settingsFilePath;
        private readonly JsonSerializerOptions _jsonOptions;

        public InspectionDataRetentionSettingsStore(string applicationRootPath)
        {
            string projectRootPath = ProjectDataRootResolver.Resolve(applicationRootPath);
            _settingsFilePath = Path.Combine(projectRootPath, "CFG", "InspectionDataRetention.json");
            _jsonOptions = new JsonSerializerOptions();
            _jsonOptions.WriteIndented = true;
        }

        public string SettingsFilePath
        {
            get { return _settingsFilePath; }
        }

        public InspectionDataRetentionSettings Load()
        {
            if (!File.Exists(_settingsFilePath))
            {
                return new InspectionDataRetentionSettings();
            }

            try
            {
                string json = File.ReadAllText(_settingsFilePath);
                InspectionDataRetentionSettings settings = JsonSerializer.Deserialize<InspectionDataRetentionSettings>(json, _jsonOptions);
                return Normalize(settings);
            }
            catch
            {
                return new InspectionDataRetentionSettings();
            }
        }

        public void Save(InspectionDataRetentionSettings settings)
        {
            string directoryPath = Path.GetDirectoryName(_settingsFilePath);
            if (!string.IsNullOrWhiteSpace(directoryPath))
            {
                Directory.CreateDirectory(directoryPath);
            }

            string json = JsonSerializer.Serialize(Normalize(settings), _jsonOptions);
            File.WriteAllText(_settingsFilePath, json);
        }

        private InspectionDataRetentionSettings Normalize(InspectionDataRetentionSettings settings)
        {
            if (settings == null)
            {
                settings = new InspectionDataRetentionSettings();
            }

            if (settings.MinimumFreeSpacePercent < 1m)
            {
                settings.MinimumFreeSpacePercent = 1m;
            }

            if (settings.MinimumFreeSpacePercent > 99m)
            {
                settings.MinimumFreeSpacePercent = 99m;
            }

            if (settings.RetentionDays <= 0)
            {
                settings.RetentionDays = 365;
            }

            return settings;
        }
    }
}
