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
    /// AI 결과 문자열은 true,score,measurement1,... 형식이며 측정값 단위는 mm로 고정합니다.
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

            List<MeasurementRegion> regions = BuildSortedMeasurementRegions(input.Part.MeasurementRegions);
            VladStandardAiResult standardResult;
            if (!TryParseStandardAiResult(detectText, out standardResult))
            {
                return measurements;
            }

            int measurementCount = Math.Min(regions.Count, standardResult.MeasurementValues.Count);
            for (int index = 0; index < measurementCount; index++)
            {
                MeasurementRegion region = regions[index];
                VisionMeasurementValue measurement = CreateBaseMeasurement(input, region);
                measurement.Value = standardResult.MeasurementValues[index];
                measurement.Unit = "mm";
                measurement.CalibrationId = "VLAD-standard-result-mm";
                measurements.Add(measurement);
            }

            return measurements;
        }

        public bool TryParseStandardAiResult(string detectText, out VladStandardAiResult result)
        {
            result = null;

            if (string.IsNullOrWhiteSpace(detectText))
            {
                return false;
            }

            VladStandardAiResult fallbackResult = null;
            string[] lines = detectText.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
            for (int index = 0; index < lines.Length; index++)
            {
                VladStandardAiResult candidate;
                if (TryParseStandardAiResultCandidate(lines[index], out candidate))
                {
                    if (candidate.MeasurementValues.Count > 0)
                    {
                        result = candidate;
                        return true;
                    }

                    if (fallbackResult == null)
                    {
                        fallbackResult = candidate;
                    }
                }
            }

            VladStandardAiResult wholeTextCandidate;
            if (TryParseStandardAiResultCandidate(detectText, out wholeTextCandidate))
            {
                if (wholeTextCandidate.MeasurementValues.Count > 0)
                {
                    result = wholeTextCandidate;
                    return true;
                }

                if (fallbackResult == null)
                {
                    fallbackResult = wholeTextCandidate;
                }
            }

            if (fallbackResult != null)
            {
                result = fallbackResult;
                return true;
            }

            return false;
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

        private bool TryParseStandardAiResultCandidate(string text, out VladStandardAiResult result)
        {
            result = null;

            string candidate = RemoveViewPrefix(text);
            if (string.IsNullOrWhiteSpace(candidate))
            {
                return false;
            }

            string[] tokens = candidate.Split(',');
            if (tokens.Length < 2)
            {
                return false;
            }

            bool isMatched;
            if (!TryParseMatchedToken(tokens[0], out isMatched))
            {
                return false;
            }

            decimal score;
            if (!TryParseDecimalToken(tokens[1], out score))
            {
                return false;
            }

            VladStandardAiResult parsed = new VladStandardAiResult();
            parsed.IsMatched = isMatched;
            parsed.Confidence = NormalizeConfidence(score);

            for (int index = 2; index < tokens.Length; index++)
            {
                decimal measuredValue;
                if (TryParseDecimalToken(tokens[index], out measuredValue))
                {
                    parsed.MeasurementValues.Add(measuredValue);
                }
            }

            result = parsed;
            return true;
        }

        private string RemoveViewPrefix(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return string.Empty;
            }

            string candidate = text.Trim();
            if (candidate.StartsWith("[", StringComparison.Ordinal) && candidate.Contains("]"))
            {
                int closeIndex = candidate.IndexOf(']');
                candidate = candidate.Substring(closeIndex + 1).Trim();
            }

            return candidate;
        }

        private bool TryParseMatchedToken(string token, out bool isMatched)
        {
            isMatched = false;
            string value = string.IsNullOrWhiteSpace(token) ? string.Empty : token.Trim();
            if (string.Equals(value, "true", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(value, "ok", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(value, "pass", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(value, "1", StringComparison.OrdinalIgnoreCase))
            {
                isMatched = true;
                return true;
            }

            if (string.Equals(value, "false", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(value, "ng", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(value, "fail", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(value, "0", StringComparison.OrdinalIgnoreCase))
            {
                isMatched = false;
                return true;
            }

            return false;
        }

        private bool TryParseDecimalToken(string token, out decimal value)
        {
            value = 0m;
            string text = string.IsNullOrWhiteSpace(token) ? string.Empty : token.Trim();
            Match match = Regex.Match(text, @"-?\d+(?:\.\d+)?");
            return match.Success &&
                decimal.TryParse(match.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out value);
        }

        private decimal NormalizeConfidence(decimal score)
        {
            if (score > 1m && score <= 100m)
            {
                return decimal.Round(score / 100m, 4);
            }

            return score;
        }

        private List<MeasurementRegion> BuildSortedMeasurementRegions(IList<MeasurementRegion> source)
        {
            List<MeasurementRegion> regions = new List<MeasurementRegion>();
            if (source == null)
            {
                return regions;
            }

            for (int index = 0; index < source.Count; index++)
            {
                if (source[index] != null)
                {
                    regions.Add(source[index]);
                }
            }

            regions.Sort(delegate(MeasurementRegion left, MeasurementRegion right)
            {
                return left.IndexNo.CompareTo(right.IndexNo);
            });

            return regions;
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

    public class VladStandardAiResult
    {
        public VladStandardAiResult()
        {
            MeasurementValues = new List<decimal>();
        }

        public bool IsMatched { get; set; }

        public decimal Confidence { get; set; }

        public IList<decimal> MeasurementValues { get; private set; }
    }
}
