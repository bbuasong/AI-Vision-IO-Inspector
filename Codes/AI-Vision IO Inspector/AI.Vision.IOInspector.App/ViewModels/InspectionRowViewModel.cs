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
            Memo = inspection.Memo;
            PartCount = inspection.PartCount < 1 ? 1 : inspection.PartCount;
            MeasuredValues = BuildMeasurementValues(inspection.Measurements, true);
            NominalValues = BuildMeasurementValues(inspection.Measurements, false);
            NgResult = BuildNgReason(inspection);
            ScoreText = BuildScoreText(inspection);
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

        public string Memo { get; set; }

        /// <summary>검사 시점의 품목개수입니다.</summary>
        public int PartCount { get; set; }

        public string MeasuredValues { get; set; }

        public string NominalValues { get; set; }

        /// <summary>
        /// 왜 떨어졌는지만 적습니다.
        ///
        /// <para>
        /// 예전에는 이 칸에 "이름 (측정 x / 기준 y)" 를 적어, 바로 옆 측정값·기준값 칸과 같은
        /// 값이 두 번 나왔습니다. 측정부 때문이 아니라 점수 때문에 떨어졌을 때는 결과 메시지
        /// 전체를 그대로 복사해 메시지 칸과도 겹쳤습니다. 이제 원인만 적습니다.
        /// </para>
        /// </summary>
        public string NgResult { get; set; }

        /// <summary>
        /// AI 점수와 통과 기준입니다. 예전에는 메시지 칸의 긴 글에서 눈으로 찾아야 했습니다.
        /// </summary>
        public string ScoreText { get; set; }

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

            // 내경·외경은 「외경」이 「내경」보다 먼저 걸리지 않도록 둘 다 정확히 봅니다.
            if (ContainsMeasurementText(name, "내경") || ContainsMeasurementText(name, "InnerDiameter"))
            {
                return "InnerDiameter";
            }

            if (ContainsMeasurementText(name, "외경") || ContainsMeasurementText(name, "OuterDiameter"))
            {
                return "OuterDiameter";
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
            else if (dimensionName == "InnerDiameter")
            {
                measurementSet.InnerDiameter = MergeCompactMeasurementValue(measurementSet.InnerDiameter, value);
            }
            else if (dimensionName == "OuterDiameter")
            {
                measurementSet.OuterDiameter = MergeCompactMeasurementValue(measurementSet.OuterDiameter, value);
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

        /// <summary>
        /// 한 측정부 벌을 한 칸에 적습니다. 앞 네 자리는 길이 / 너비 / 높이 / 두께로 늘 같습니다.
        ///
        /// <para>
        /// 내경·외경은 자리를 늘 비워 두지 않고 <b>쓰는 부품에서만</b> 이름을 붙여 뒤에 덧붙입니다.
        /// 여섯 자리로 고정하면 이 둘을 쓰지 않는 대부분의 부품에서 「- / -」만 길어집니다.
        /// 이름이 붙어 있으므로 자리를 세지 않아도 무엇인지 알 수 있습니다.
        /// </para>
        /// </summary>
        private string FormatCompactMeasurementSet(CompactMeasurementSet measurementSet)
        {
            string text = FormatCompactValue(measurementSet.Length) + " / " +
                          FormatCompactValue(measurementSet.Width) + " / " +
                          FormatCompactValue(measurementSet.Height) + " / " +
                          FormatCompactValue(measurementSet.Thickness);

            if (!string.IsNullOrWhiteSpace(measurementSet.InnerDiameter))
            {
                text += " / 내경 " + measurementSet.InnerDiameter;
            }

            if (!string.IsNullOrWhiteSpace(measurementSet.OuterDiameter))
            {
                text += " / 외경 " + measurementSet.OuterDiameter;
            }

            return text;
        }

        private string FormatCompactValue(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return "-";
            }

            return value;
        }

        /// <summary>
        /// 떨어진 원인만 짧게 만듭니다. 값은 측정값·기준값 칸에 이미 있으므로 여기서 되풀이하지
        /// 않습니다. 원인은 셋 중 하나입니다 — 측정부, 방향 점수, 검사 실패.
        /// </summary>
        private string BuildNgReason(Inspection inspection)
        {
            if (inspection.Result == InspectionResult.Pass)
            {
                return "-";
            }

            if (inspection.Result == InspectionResult.Error)
            {
                return "검사 실패";
            }

            // 기준을 벗어난 측정부가 있으면 그 이름만 적습니다.
            StringBuilder builder = new StringBuilder();
            foreach (MeasurementResult measurement in inspection.Measurements)
            {
                if (!measurement.IsPass)
                {
                    AppendListText(builder, measurement.Name);
                }
            }

            if (builder.Length > 0)
            {
                return "측정부 " + builder;
            }

            // 측정부는 다 맞았는데 떨어졌다면 방향 점수 때문입니다. 떨어진 방향만 적습니다.
            builder = new StringBuilder();
            if (inspection.ViewResults != null)
            {
                foreach (KeyValuePair<ImageViewType, AiViewInferenceResult> pair in inspection.ViewResults)
                {
                    if (pair.Value != null && !pair.Value.IsPass)
                    {
                        AppendListText(builder, pair.Key.ToString());
                    }
                }
            }

            if (builder.Length > 0)
            {
                return "Score 미달 " + builder;
            }

            return "판정 기준 미달";
        }

        /// <summary>
        /// 점수와 통과 기준을 "99.31 / 39.83" 형태로 적습니다.
        /// 점수가 없는 검사는 "-" 입니다.
        /// </summary>
        private string BuildScoreText(Inspection inspection)
        {
            if (!inspection.HasAiScore)
            {
                return "-";
            }

            return InspectionScoreFormat.Format(inspection.AiScore) +
                   " / " +
                   InspectionScoreFormat.Format(inspection.AiScoreThreshold);
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

            /// <summary>안쪽 지름입니다. 원을 감싸는 사각 범위로 지정한 항목입니다.</summary>
            public string InnerDiameter { get; set; }

            /// <summary>바깥 지름입니다.</summary>
            public string OuterDiameter { get; set; }

            public bool HasValue
            {
                get
                {
                    return !string.IsNullOrWhiteSpace(Length) ||
                           !string.IsNullOrWhiteSpace(Width) ||
                           !string.IsNullOrWhiteSpace(Height) ||
                           !string.IsNullOrWhiteSpace(Thickness) ||
                           !string.IsNullOrWhiteSpace(InnerDiameter) ||
                           !string.IsNullOrWhiteSpace(OuterDiameter);
                }
            }
        }
    }
}
