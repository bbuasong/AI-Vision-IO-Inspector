namespace AI.Vision.IOInspector.App.ViewModels
{
    /// <summary>
    /// 다중품목 CSV 불러오기 결과를 화면에서 확인하기 위한 표시 모델입니다.
    /// 실제 저장은 PartCatalogService를 통해 처리하고, 이 모델은 사용자 검토용으로만 사용합니다.
    /// </summary>
    public class BulkPartCsvRowViewModel : ObservableObject
    {
        public string PartNo { get; set; }

        public string PartName { get; set; }

        public string CategoryCode { get; set; }

        public string CategoryDescription { get; set; }

        public string PartType { get; set; }

        public string Measurement1Summary { get; set; }

        public string Measurement2Summary { get; set; }

        public string Measurement3Summary { get; set; }

        public string Measurement4Summary { get; set; }

        public string Measurement5Summary { get; set; }

        public string MeasurementUnit { get; set; }

        public string ResultMessage { get; set; }
    }
}
