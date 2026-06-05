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
            MeasurementKeys = new List<string>();
            MeasuredValueByKey = new Dictionary<string, string>();
            NominalValueByKey = new Dictionary<string, string>();
            MeasurementResultByKey = new Dictionary<string, string>();

            Id = inspection.Id;
            PartNo = inspection.PartNo;
            PartName = inspection.PartName;
            CategoryCode = inspection.CategoryCode;
            CategoryDescription = inspection.CategoryDescription;
            PartType = inspection.PartType;
            BuildMeasurementCsvValues(inspection.Measurements);
            MeasuredValues = BuildMeasurementValues(inspection.Measurements, true);
            NominalValues = BuildMeasurementValues(inspection.Measurements, false);
            MismatchItems = BuildMismatchItems(inspection);
            NgResult = inspection.Result == InspectionResult.Ng ? MismatchItems : "-";
            Result = inspection.Result.ToString();
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

        public IList<string> MeasurementKeys { get; private set; }

        public IDictionary<string, string> MeasuredValueByKey { get; private set; }

        public IDictionary<string, string> NominalValueByKey { get; private set; }

        public IDictionary<string, string> MeasurementResultByKey { get; private set; }

        public string Result { get; set; }

        public string InspectedAt { get; set; }

        public System.DateTime InspectedAtValue { get; set; }

        public string Elapsed { get; set; }

        public string Message { get; set; }

        public string GetMeasuredValue(string measurementKey)
        {
            return GetDictionaryValue(MeasuredValueByKey, measurementKey);
        }

        public string GetNominalValue(string measurementKey)
        {
            return GetDictionaryValue(NominalValueByKey, measurementKey);
        }

        public string GetMeasurementResult(string measurementKey)
        {
            return GetDictionaryValue(MeasurementResultByKey, measurementKey);
        }

        private void BuildMeasurementCsvValues(IList<MeasurementResult> measurements)
        {
            if (measurements == null)
            {
                return;
            }

            foreach (MeasurementResult measurement in measurements)
            {
                string measurementKey = BuildUniqueMeasurementKey(measurement);
                MeasurementKeys.Add(measurementKey);
                MeasuredValueByKey[measurementKey] = FormatDecimal(measurement.MeasuredValue);
                NominalValueByKey[measurementKey] = FormatDecimal(measurement.NominalValue);
                MeasurementResultByKey[measurementKey] = measurement.IsOk ? "OK" : "NG";
            }
        }

        private string BuildUniqueMeasurementKey(MeasurementResult measurement)
        {
            string baseKey = NormalizeMeasurementName(measurement.Name);
            string key = baseKey;
            if (MeasuredValueByKey.ContainsKey(key))
            {
                key = baseKey + "_" + measurement.MeasurementRegionId.ToString();
            }

            return key;
        }

        private string NormalizeMeasurementName(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return "측정부";
            }

            StringBuilder builder = new StringBuilder();
            bool lastCharacterWasSeparator = false;
            foreach (char character in name.Trim())
            {
                if (char.IsWhiteSpace(character))
                {
                    continue;
                }

                if (IsMeasurementNameSeparator(character))
                {
                    if (builder.Length > 0 && !lastCharacterWasSeparator)
                    {
                        builder.Append("_");
                        lastCharacterWasSeparator = true;
                    }
                }
                else
                {
                    builder.Append(character);
                    lastCharacterWasSeparator = false;
                }
            }

            string normalizedName = builder.ToString().Trim('_');
            if (string.IsNullOrWhiteSpace(normalizedName))
            {
                return "측정부";
            }

            return normalizedName;
        }

        private bool IsMeasurementNameSeparator(char character)
        {
            return character == '-' ||
                   character == '/' ||
                   character == '\\' ||
                   character == ':' ||
                   character == '(' ||
                   character == ')' ||
                   character == '[' ||
                   character == ']';
        }

        private string GetDictionaryValue(IDictionary<string, string> dictionary, string key)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                return string.Empty;
            }

            if (dictionary.ContainsKey(key))
            {
                return dictionary[key];
            }

            return string.Empty;
        }

        private string BuildMeasurementValues(IList<MeasurementResult> measurements, bool useMeasuredValue)
        {
            if (measurements == null || measurements.Count == 0)
            {
                return "-";
            }

            StringBuilder builder = new StringBuilder();
            foreach (MeasurementResult measurement in measurements)
            {
                string value = useMeasuredValue
                    ? FormatDecimal(measurement.MeasuredValue)
                    : FormatDecimal(measurement.NominalValue);
                AppendListText(builder, measurement.Name + ": " + value + " " + measurement.Unit);
            }

            return builder.ToString();
        }

        private string BuildMismatchItems(Inspection inspection)
        {
            if (inspection.Result != InspectionResult.Ng)
            {
                return "-";
            }

            StringBuilder builder = new StringBuilder();
            foreach (MeasurementResult measurement in inspection.Measurements)
            {
                if (!measurement.IsOk)
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
    }
}
