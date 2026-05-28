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
            ViewType = region.ViewType.ToString();
            NominalValue = region.NominalValue;
            MeasuredValue = region.NominalValue;
            Tolerance = region.ToleranceMin + " ~ +" + region.ToleranceMax;
            Unit = region.Unit;
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

        public decimal NominalValue { get; set; }

        public decimal MeasuredValue { get; set; }

        public string Tolerance { get; set; }

        public string Unit { get; set; }

        public string ResultText { get; set; }
    }
}
