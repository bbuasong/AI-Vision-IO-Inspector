using System;
using System.Collections.Generic;
using System.Text;
using AI.Vision.IOInspector.Domain.Enums;
using AI.Vision.IOInspector.Domain.Models;

namespace AI.Vision.IOInspector.App.ViewModels
{
    /// <summary>
    /// 이력 화면 목록에 표시할 검사 결과 모델입니다.
    /// 검사 결과를 품번/품명/분류/측정 요약 단위로 풀어 화면과 CSV 저장에서 함께 사용합니다.
    /// </summary>
    public class InspectionRowViewModel : ObservableObject
    {
        public InspectionRowViewModel(Inspection inspection)
        {
            Id = inspection.Id;
            PartNo = inspection.PartNo;
            PartName = inspection.PartName;
            CategoryCode = inspection.CategoryCode;
            CategoryDescription = inspection.CategoryDescription;
            PartType = inspection.PartType;
            MeasuredValues = BuildMeasurementValues(inspection.Measurements, true);
            NominalValues = BuildMeasurementValues(inspection.Measurements, false);
            MismatchItems = BuildMismatchItems(inspection);
            NgResult = inspection.Result == InspectionResult.Fail ? MismatchItems : "-";
            Result = BuildResultText(inspection.Result);
            InspectedAtValue = inspection.InspectedAt;
            InspectedAt = inspection.InspectedAt.ToString("yyyy-MM-dd HH:mm:ss");
            Elapsed = inspection.ElapsedMilliseconds.ToString("0") + " ms";
            Message = inspection.ResultMessage;
        }

        public int Id { get; set; }

        public string PartNo { get; set; }

        public string PartName { get; set; }

        public string CategoryCode { get; set; }

        public string CategoryDescription { get; set; }

        public string PartType { get; set; }

        public string MeasuredValues { get; set; }

        public string NominalValues { get; set; }

        public string MismatchItems { get; set; }

        public string NgResult { get; set; }

        public string Result { get; set; }

        public string InspectedAt { get; set; }

        public System.DateTime InspectedAtValue { get; set; }

        public string Elapsed { get; set; }

        public string Message { get; set; }

        private static string BuildResultText(InspectionResult result)
        {
            if (result == InspectionResult.Pass)
            {
                return "PASS";
            }

            if (result == InspectionResult.Fail)
            {
                return "FAIL";
            }

            if (result == InspectionResult.Error)
            {
                return "ERROR";
            }

            return "READY";
        }

        private string BuildMeasurementValues(IList<MeasurementResult> measurements, bool useMeasuredValue)
        {
            if (measurements == null || measurements.Count == 0)
            {
                return "-";
            }

            Dictionary<int, CompactMeasurementSet> measurementSets = new Dictionary<int, CompactMeasurementSet>();
            List<string> fallbackValues = new List<string>();

            foreach (MeasurementResult measurement in measurements)
            {
                string value = useMeasuredValue
                    ? FormatDecimal(measurement.MeasuredValue)
                    : FormatDecimal(measurement.NominalValue);

                string dimensionName = GetMeasurementDimensionName(measurement.Name);
                if (string.IsNullOrWhiteSpace(dimensionName))
                {
                    fallbackValues.Add(value);
                    continue;
                }

                int setNumber = GetMeasurementSetNumber(measurement.Name);
                if (!measurementSets.ContainsKey(setNumber))
                {
                    measurementSets[setNumber] = new CompactMeasurementSet();
                }

                SetCompactMeasurementValue(measurementSets[setNumber], dimensionName, value);
            }

            StringBuilder builder = new StringBuilder();
            List<int> setNumbers = new List<int>(measurementSets.Keys);
            setNumbers.Sort();

            foreach (int setNumber in setNumbers)
            {
                CompactMeasurementSet measurementSet = measurementSets[setNumber];
                if (measurementSet.HasValue)
                {
                    AppendListText(builder, FormatCompactMeasurementSet(measurementSet));
                }
            }

            if (builder.Length > 0)
            {
                return builder.ToString();
            }

            if (fallbackValues.Count > 0)
            {
                return string.Join(" / ", fallbackValues);
            }

            return "-";
        }

        private string GetMeasurementDimensionName(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return string.Empty;
            }

            if (ContainsMeasurementText(name, "길이") || ContainsMeasurementText(name, "Length"))
            {
                return "Length";
            }

            if (ContainsMeasurementText(name, "너비") || ContainsMeasurementText(name, "폭") || ContainsMeasurementText(name, "Width"))
            {
                return "Width";
            }

            if (ContainsMeasurementText(name, "높이") || ContainsMeasurementText(name, "Height"))
            {
                return "Height";
            }

            if (ContainsMeasurementText(name, "두께") || ContainsMeasurementText(name, "Thickness"))
            {
                return "Thickness";
            }

            return string.Empty;
        }

        private bool ContainsMeasurementText(string text, string keyword)
        {
            if (string.IsNullOrWhiteSpace(text) || string.IsNullOrWhiteSpace(keyword))
            {
                return false;
            }

            return text.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private int GetMeasurementSetNumber(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return 1;
            }

            string normalizedName = name.Replace(" ", string.Empty);
            int markerIndex = normalizedName.IndexOf("측정부", StringComparison.OrdinalIgnoreCase);
            string markerText = "측정부";
            if (markerIndex < 0)
            {
                markerText = "Measurement";
                markerIndex = normalizedName.IndexOf(markerText, StringComparison.OrdinalIgnoreCase);
            }

            if (markerIndex < 0)
            {
                return 1;
            }

            StringBuilder numberBuilder = new StringBuilder();
            int startIndex = markerIndex + markerText.Length;
            for (int index = startIndex; index < normalizedName.Length; index++)
            {
                char character = normalizedName[index];
                if (char.IsDigit(character))
                {
                    numberBuilder.Append(character);
                    continue;
                }

                if (numberBuilder.Length > 0)
                {
                    break;
                }
            }

            if (numberBuilder.Length == 0)
            {
                return 1;
            }

            int setNumber;
            if (int.TryParse(numberBuilder.ToString(), out setNumber) && setNumber > 0)
            {
                return setNumber;
            }

            return 1;
        }

        private void SetCompactMeasurementValue(CompactMeasurementSet measurementSet, string dimensionName, string value)
        {
            if (dimensionName == "Length")
            {
                measurementSet.Length = MergeCompactMeasurementValue(measurementSet.Length, value);
            }
            else if (dimensionName == "Width")
            {
                measurementSet.Width = MergeCompactMeasurementValue(measurementSet.Width, value);
            }
            else if (dimensionName == "Height")
            {
                measurementSet.Height = MergeCompactMeasurementValue(measurementSet.Height, value);
            }
            else if (dimensionName == "Thickness")
            {
                measurementSet.Thickness = MergeCompactMeasurementValue(measurementSet.Thickness, value);
            }
        }

        private string MergeCompactMeasurementValue(string currentValue, string newValue)
        {
            if (string.IsNullOrWhiteSpace(currentValue))
            {
                return newValue;
            }

            return currentValue + ", " + newValue;
        }

        private string FormatCompactMeasurementSet(CompactMeasurementSet measurementSet)
        {
            return FormatCompactValue(measurementSet.Length) + " / " +
                   FormatCompactValue(measurementSet.Width) + " / " +
                   FormatCompactValue(measurementSet.Height) + " / " +
                   FormatCompactValue(measurementSet.Thickness);
        }

        private string FormatCompactValue(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return "-";
            }

            return value;
        }

        private string BuildMismatchItems(Inspection inspection)
        {
            if (inspection.Result != InspectionResult.Fail)
            {
                return "-";
            }

            StringBuilder builder = new StringBuilder();
            foreach (MeasurementResult measurement in inspection.Measurements)
            {
                if (!measurement.IsPass)
                {
                    AppendListText(
                        builder,
                        measurement.Name + " (측정 " + FormatDecimal(measurement.MeasuredValue) + " / 기준 " + FormatDecimal(measurement.NominalValue) + ")");
                }
            }

            if (builder.Length > 0)
            {
                return builder.ToString();
            }

            if (!string.IsNullOrWhiteSpace(inspection.ResultMessage))
            {
                return inspection.ResultMessage;
            }

            return "불일치 항목 확인 필요";
        }

        private void AppendListText(StringBuilder builder, string text)
        {
            if (builder.Length > 0)
            {
                builder.Append("; ");
            }

            builder.Append(text);
        }

        private string FormatDecimal(decimal value)
        {
            return value.ToString("0.###");
        }

        private class CompactMeasurementSet
        {
            public string Length { get; set; }

            public string Width { get; set; }

            public string Height { get; set; }

            public string Thickness { get; set; }

            public bool HasValue
            {
                get
                {
                    return !string.IsNullOrWhiteSpace(Length) ||
                           !string.IsNullOrWhiteSpace(Width) ||
                           !string.IsNullOrWhiteSpace(Height) ||
                           !string.IsNullOrWhiteSpace(Thickness);
                }
            }
        }
    }
}
