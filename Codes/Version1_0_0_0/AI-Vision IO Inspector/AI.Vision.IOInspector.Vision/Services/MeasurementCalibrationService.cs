using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using AI.Vision.IOInspector.Domain.Enums;
using AI.Vision.IOInspector.Infrastructure;

namespace AI.Vision.IOInspector.Vision.Services
{
    /// <summary>
    /// 카메라 시점별 픽셀-mm 보정값을 관리합니다.
    /// 실제 현장 캘리브레이션 값이 없으면 픽셀값을 mm로 확정 변환하지 않고 호출자에게 실패를 알려줍니다.
    /// </summary>
    public class MeasurementCalibrationService
    {
        private readonly Dictionary<ImageViewType, MeasurementCalibration> _calibrations;

        public MeasurementCalibrationService(string applicationRootPath)
        {
            _calibrations = new Dictionary<ImageViewType, MeasurementCalibration>();
            Load();
        }

        public bool TryConvertPixelLength(
            ImageViewType viewType,
            string measurementName,
            decimal pixelValue,
            out decimal millimeterValue,
            out string calibrationId)
        {
            millimeterValue = 0m;
            calibrationId = string.Empty;

            MeasurementCalibration calibration;
            if (!_calibrations.TryGetValue(viewType, out calibration))
            {
                return false;
            }

            decimal ratio = GetAxisRatio(calibration, measurementName);
            millimeterValue = decimal.Round(pixelValue * ratio, 3);
            calibrationId = calibration.CalibrationId;
            return true;
        }

        private void Load()
        {
            string calibrationPath = RuntimeConfigurationPathResolver.GetConfigFilePath("Calibration.json");
            if (!File.Exists(calibrationPath))
            {
                return;
            }

            try
            {
                string json = File.ReadAllText(calibrationPath);
                JsonSerializerOptions options = new JsonSerializerOptions();
                options.PropertyNameCaseInsensitive = true;

                CalibrationDocument document = JsonSerializer.Deserialize<CalibrationDocument>(json, options);
                if (document == null || document.Calibrations == null)
                {
                    return;
                }

                foreach (CalibrationItem item in document.Calibrations)
                {
                    AddCalibration(item);
                }
            }
            catch
            {
                // 보정 파일은 현장별 선택 파일입니다. 파일 오류가 있어도 프로그램 실행은 유지하고,
                // 실제 측정값 매핑 단계에서 CalibrationMissing 상태로 남깁니다.
            }
        }

        private void AddCalibration(CalibrationItem item)
        {
            if (item == null || !item.IsActive)
            {
                return;
            }

            ImageViewType viewType;
            if (!Enum.TryParse<ImageViewType>(item.ViewType, true, out viewType))
            {
                return;
            }

            if (item.MmPerPixelX <= 0m || item.MmPerPixelY <= 0m)
            {
                return;
            }

            MeasurementCalibration calibration = new MeasurementCalibration();
            calibration.ViewType = viewType;
            calibration.MmPerPixelX = item.MmPerPixelX;
            calibration.MmPerPixelY = item.MmPerPixelY;
            calibration.CalibrationId = item.CalibrationId;
            if (string.IsNullOrWhiteSpace(calibration.CalibrationId))
            {
                calibration.CalibrationId = viewType.ToString() + "-Calibration";
            }

            _calibrations[viewType] = calibration;
        }

        private decimal GetAxisRatio(MeasurementCalibration calibration, string measurementName)
        {
            string normalizedName = measurementName ?? string.Empty;
            normalizedName = normalizedName.ToLowerInvariant();

            if (normalizedName.Contains("높이") || normalizedName.Contains("두께") ||
                normalizedName.Contains("height") || normalizedName.Contains("thickness"))
            {
                return calibration.MmPerPixelY;
            }

            if (normalizedName.Contains("길이") || normalizedName.Contains("너비") || normalizedName.Contains("폭") ||
                normalizedName.Contains("length") || normalizedName.Contains("width"))
            {
                return calibration.MmPerPixelX;
            }

            return (calibration.MmPerPixelX + calibration.MmPerPixelY) / 2m;
        }

        private class CalibrationDocument
        {
            public IList<CalibrationItem> Calibrations { get; set; }
        }

        private class CalibrationItem
        {
            public string ViewType { get; set; }

            public decimal MmPerPixelX { get; set; }

            public decimal MmPerPixelY { get; set; }

            public string CalibrationId { get; set; }

            public bool IsActive { get; set; }
        }

        private class MeasurementCalibration
        {
            public ImageViewType ViewType { get; set; }

            public decimal MmPerPixelX { get; set; }

            public decimal MmPerPixelY { get; set; }

            public string CalibrationId { get; set; }
        }
    }
}
