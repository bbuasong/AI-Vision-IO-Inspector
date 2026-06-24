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
            ItemType = region.ItemType;
            ViewType = region.ViewType.ToString();
            NominalValue = region.NominalValue;
            MeasuredValue = region.NominalValue;
            Tolerance = region.ToleranceMin + " ~ +" + region.ToleranceMax;
            Unit = region.Unit;
            X1 = region.X1;
            Y1 = region.Y1;
            X2 = region.X2;
            Y2 = region.Y2;
            LineColor = region.LineColor;
            ResultText = "기준";
        }

        public MeasurementRowViewModel(MeasurementResult result)
        {
            Name = result.Name;
            ViewType = "-";
            NominalValue = result.NominalValue;
            MeasuredValue = result.MeasuredValue;
            Tolerance = result.ToleranceMin + " ~ +" + result.ToleranceMax;
            Unit = result.Unit;
            ResultText = result.IsOk ? "OK" : "NG";
        }

        public string Name { get; set; }

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
    }
}
