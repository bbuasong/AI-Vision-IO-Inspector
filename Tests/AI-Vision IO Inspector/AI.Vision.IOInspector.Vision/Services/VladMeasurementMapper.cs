using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;
using AI.Vision.IOInspector.Domain.Enums;
using AI.Vision.IOInspector.Domain.Models;
using AI.Vision.IOInspector.Vision.Models;

namespace AI.Vision.IOInspector.Vision.Services
{
    /// <summary>
    /// VLAD 추론 결과를 현재 프로젝트의 측정값 모델로 변환합니다.
    /// detectText에 치수값이 있으면 우선 사용하고, 없으면 bbox 픽셀값을 보정값으로 mm 단위에 맞춰 변환합니다.
    /// </summary>
    public class VladMeasurementMapper
    {
        private readonly MeasurementCalibrationService _calibrationService;

        public VladMeasurementMapper(MeasurementCalibrationService calibrationService)
        {
            _calibrationService = calibrationService;
        }

        public IList<VisionMeasurementValue> BuildMeasurements(VisionInspectionInput input, IList<VladDetection> detections, string detectText)
        {
            List<VisionMeasurementValue> measurements = new List<VisionMeasurementValue>();
            if (input == null || input.Part == null || input.Part.MeasurementRegions == null)
            {
                return measurements;
            }

            foreach (MeasurementRegion region in input.Part.MeasurementRegions)
            {
                measurements.Add(BuildMeasurement(input, region, detections, detectText));
            }

            return measurements;
        }

        private VisionMeasurementValue BuildMeasurement(VisionInspectionInput input, MeasurementRegion region, IList<VladDetection> detections, string detectText)
        {
            VisionMeasurementValue measurement = CreateBaseMeasurement(input, region);

            decimal textValue;
            string textUnit;
            if (TryParseTextMeasurement(region, detectText, out textValue, out textUnit))
            {
                measurement.Value = ConvertToTargetUnit(textValue, textUnit, region.Unit);
                measurement.CalibrationId = "VLAD-detectText";
                return measurement;
            }

            VladDetection detection = FindBestDetection(region.ViewType, detections);
            if (detection == null)
            {
                measurement.Value = 0m;
                measurement.CalibrationId = "MeasurementUnavailable";
                return measurement;
            }

            measurement.RawPixelValue = GetPixelLength(ResolveMeasurementType(region), detection);
            measurement.SourceImagePath = detection.SourceImagePath;

            decimal millimeterValue;
            string calibrationId;
            if (_calibrationService != null &&
                _calibrationService.TryConvertPixelLength(
                    region.ViewType,
                    ResolveMeasurementType(region),
                    measurement.RawPixelValue,
                    out millimeterValue,
                    out calibrationId))
            {
                measurement.Value = ConvertToTargetUnit(millimeterValue, "mm", region.Unit);
                measurement.CalibrationId = calibrationId;
                return measurement;
            }

            measurement.Value = 0m;
            measurement.CalibrationId = "CalibrationMissing";
            return measurement;
        }

        private VisionMeasurementValue CreateBaseMeasurement(VisionInspectionInput input, MeasurementRegion region)
        {
            VisionMeasurementValue measurement = new VisionMeasurementValue();
            measurement.MeasurementRegionId = region.Id;
            measurement.Name = region.Name;
            measurement.ViewType = region.ViewType;
            measurement.Unit = string.IsNullOrWhiteSpace(region.Unit) ? "mm" : region.Unit;
            measurement.RawPixelValue = 0m;
            measurement.SourceImagePath = FindSourceImagePath(input, region.ViewType);
            measurement.CalibrationId = string.Empty;
            return measurement;
        }

        private bool TryParseTextMeasurement(
            MeasurementRegion region,
            string detectText,
            out decimal value,
            out string unit)
        {
            value = 0m;
            unit = string.IsNullOrWhiteSpace(region.Unit) ? "mm" : region.Unit;

            if (string.IsNullOrWhiteSpace(detectText))
            {
                return false;
            }

            string[] keys = BuildSearchKeys(ResolveMeasurementType(region));
            for (int index = 0; index < keys.Length; index++)
            {
                string key = keys[index];
                if (string.IsNullOrWhiteSpace(key))
                {
                    continue;
                }

                string pattern = Regex.Escape(key) + @"\s*[:=]?\s*(-?\d+(?:\.\d+)?)\s*(mm|cm|m)?";
                Match match = Regex.Match(detectText, pattern, RegexOptions.IgnoreCase);
                if (!match.Success)
                {
                    continue;
                }

                if (!decimal.TryParse(match.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out value))
                {
                    continue;
                }

                if (match.Groups.Count > 2 && !string.IsNullOrWhiteSpace(match.Groups[2].Value))
                {
                    unit = match.Groups[2].Value;
                }

                return true;
            }

            return false;
        }

        private string[] BuildSearchKeys(string regionName)
        {
            List<string> keys = new List<string>();
            AddKey(keys, regionName);

            string lowerName = (regionName ?? string.Empty).ToLowerInvariant();
            if (lowerName.Contains("길이") || lowerName.Contains("length") || lowerName.Contains("len"))
            {
                AddKey(keys, "길이");
                AddKey(keys, "Length");
                AddKey(keys, "length");
                AddKey(keys, "len");
            }

            if (lowerName.Contains("너비") || lowerName.Contains("폭") || lowerName.Contains("width"))
            {
                AddKey(keys, "너비");
                AddKey(keys, "폭");
                AddKey(keys, "Width");
                AddKey(keys, "width");
            }

            if (lowerName.Contains("높이") || lowerName.Contains("height"))
            {
                AddKey(keys, "높이");
                AddKey(keys, "Height");
                AddKey(keys, "height");
            }

            if (lowerName.Contains("두께") || lowerName.Contains("thickness"))
            {
                AddKey(keys, "두께");
                AddKey(keys, "Thickness");
                AddKey(keys, "thickness");
            }

            return keys.ToArray();
        }

        private void AddKey(IList<string> keys, string key)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                return;
            }

            for (int index = 0; index < keys.Count; index++)
            {
                if (string.Equals(keys[index], key, StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }
            }

            keys.Add(key);
        }

        private VladDetection FindBestDetection(ImageViewType viewType, IList<VladDetection> detections)
        {
            if (detections == null || detections.Count == 0)
            {
                return null;
            }

            VladDetection best = null;
            decimal bestArea = -1m;
            for (int index = 0; index < detections.Count; index++)
            {
                VladDetection detection = detections[index];
                if (detection == null || detection.ViewType != viewType)
                {
                    continue;
                }

                decimal area = Math.Abs(detection.Width * detection.Height);
                if (area > bestArea)
                {
                    best = detection;
                    bestArea = area;
                }
            }

            if (best != null)
            {
                return best;
            }

            for (int index = 0; index < detections.Count; index++)
            {
                VladDetection detection = detections[index];
                if (detection == null)
                {
                    continue;
                }

                decimal area = Math.Abs(detection.Width * detection.Height);
                if (area > bestArea)
                {
                    best = detection;
                    bestArea = area;
                }
            }

            return best;
        }

        private decimal GetPixelLength(string measurementName, VladDetection detection)
        {
            string normalizedName = (measurementName ?? string.Empty).ToLowerInvariant();

            if (normalizedName.Contains("높이") || normalizedName.Contains("두께") ||
                normalizedName.Contains("height") || normalizedName.Contains("thickness"))
            {
                return Math.Abs(detection.Height);
            }

            return Math.Abs(detection.Width);
        }

        private string ResolveMeasurementType(MeasurementRegion region)
        {
            if (region != null && !string.IsNullOrWhiteSpace(region.ItemType))
            {
                return region.ItemType;
            }

            return region == null ? string.Empty : region.Name;
        }

        private decimal ConvertToTargetUnit(decimal value, string sourceUnit, string targetUnit)
        {
            decimal millimeterValue = value;
            string source = NormalizeUnit(sourceUnit);
            string target = NormalizeUnit(targetUnit);

            if (source == "cm")
            {
                millimeterValue = value * 10m;
            }
            else if (source == "m")
            {
                millimeterValue = value * 1000m;
            }

            if (target == "cm")
            {
                return decimal.Round(millimeterValue / 10m, 3);
            }

            if (target == "m")
            {
                return decimal.Round(millimeterValue / 1000m, 3);
            }

            return decimal.Round(millimeterValue, 3);
        }

        private string NormalizeUnit(string unit)
        {
            if (string.IsNullOrWhiteSpace(unit))
            {
                return "mm";
            }

            return unit.Trim().ToLowerInvariant();
        }

        private string FindSourceImagePath(VisionInspectionInput input, ImageViewType viewType)
        {
            if (input == null || input.CapturedImages == null)
            {
                return string.Empty;
            }

            foreach (CapturedImage image in input.CapturedImages)
            {
                if (image.ViewType == viewType)
                {
                    return image.FilePath;
                }
            }

            return string.Empty;
        }
    }
}
