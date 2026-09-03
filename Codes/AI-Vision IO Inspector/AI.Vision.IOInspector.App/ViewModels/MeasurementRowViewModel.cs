using System;
using System.Globalization;
using AI.Vision.IOInspector.Domain.Models;

namespace AI.Vision.IOInspector.App.ViewModels
{
    /// <summary>
    /// 측정부 기준값과 측정 결과를 그리드에 표시하기 위한 모델입니다.
    /// </summary>
    public class MeasurementRowViewModel : ObservableObject
    {
        public MeasurementRowViewModel()
        {
        }

        public MeasurementRowViewModel(MeasurementRegion region)
        {
            Name = region.Name;
            PointName = BuildPointName(region);
            ItemType = region.ItemType;
            ViewType = region.ViewType.ToString();
            NominalValue = region.NominalValue;
            MeasuredValue = region.NominalValue;
            Tolerance = FormatTolerance(region.ToleranceMin, region.ToleranceMax);
            Unit = region.Unit;
            X1 = region.X1;
            Y1 = region.Y1;
            X2 = region.X2;
            Y2 = region.Y2;
            LineColor = region.LineColor;
            ResultText = "기준";
        }

        public MeasurementRowViewModel(MeasurementRegion region, MeasurementResult result)
            : this(region)
        {
            if (result == null)
            {
                return;
            }

            MeasuredValue = result.MeasuredValue;
            if (!string.IsNullOrWhiteSpace(result.Unit))
            {
                Unit = result.Unit;
            }

            ResultText = result.IsJudged ? (result.IsPass ? "PASS" : "FAIL") : "-";
        }

        public MeasurementRowViewModel(MeasurementResult result)
        {
            Name = result.Name;
            PointName = ExtractPointName(result.Name);
            ItemType = ExtractItemType(result.Name);
            ViewType = "-";
            NominalValue = result.NominalValue;
            MeasuredValue = result.MeasuredValue;
            Tolerance = FormatTolerance(result.ToleranceMin, result.ToleranceMax);
            Unit = result.Unit;
            ResultText = result.IsJudged ? (result.IsPass ? "PASS" : "FAIL") : "-";
        }

        public string Name { get; set; }

        public string PointName { get; set; }

        public string ViewType { get; set; }

        public string ItemType { get; set; }

        public decimal NominalValue { get; set; }

        public decimal MeasuredValue { get; set; }

        public string Tolerance { get; set; }

        public string Unit { get; set; }

        public double? X1 { get; set; }

        public double? Y1 { get; set; }

        public double? X2 { get; set; }

        public double? Y2 { get; set; }

        public string LineColor { get; set; }

        public string ResultText { get; set; }

        private static string BuildPointName(MeasurementRegion region)
        {
            if (region != null && region.IndexNo > 0)
            {
                // 검사 화면의 열 폭(70) 안에서 카메라와 번호를 함께 구분합니다.
                // Thickness는 정책의 약어인 Thk를 써서 Thk1처럼 표시합니다.
                return MeasurementPointPolicy.GetViewShortName(region.ViewType) +
                       region.IndexNo.ToString(CultureInfo.InvariantCulture);
            }

            return ExtractPointName(region == null ? string.Empty : region.Name);
        }

        private static string ExtractPointName(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return "-";
            }

            int separatorIndex = name.IndexOf(" - ", StringComparison.Ordinal);
            if (separatorIndex <= 0)
            {
                return name.Trim();
            }

            return name.Substring(0, separatorIndex).Trim();
        }

        private static string ExtractItemType(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return "-";
            }

            int separatorIndex = name.IndexOf(" - ", StringComparison.Ordinal);
            if (separatorIndex < 0 || separatorIndex + 3 >= name.Length)
            {
                return "-";
            }

            return name.Substring(separatorIndex + 3).Trim();
        }

        private static string FormatTolerance(decimal toleranceMin, decimal toleranceMax)
        {
            return "-" + Math.Abs(toleranceMin).ToString("0.###", CultureInfo.InvariantCulture) +
                   " ~ +" +
                   Math.Abs(toleranceMax).ToString("0.###", CultureInfo.InvariantCulture);
        }
    }
}
