namespace AI.Vision.IOInspector.App.ViewModels
{
    /// <summary>
    /// 다중품목 CSV 불러오기 결과를 화면에서 확인하기 위한 표시 모델입니다.
    /// 실제 저장은 PartCatalogService를 통해 처리하고, 이 모델은 사용자 검토용으로만 사용합니다.
    ///
    /// <para>
    /// 측정부 칸은 카메라마다 다섯 개씩 따로 둡니다.
    /// 예전에는 칸이 다섯 개뿐이라 앞에서부터 채웠는데, Top에 다섯 개가 있으면
    /// Thickness 측정부가 한 칸도 보이지 않았습니다. CSV 열도 카메라별로 나뉘어 있어
    /// 화면과 파일이 서로 어긋나기도 했습니다.
    /// </para>
    /// </summary>
    public class BulkPartCsvRowViewModel : ObservableObject
    {
        public string PartNo { get; set; }

        public string PartName { get; set; }

        public string CategoryCode { get; set; }

        public string CategoryDescription { get; set; }

        public string Memo { get; set; }

        public string Top1Summary { get; set; }

        public string Top2Summary { get; set; }

        public string Top3Summary { get; set; }

        public string Top4Summary { get; set; }

        public string Top5Summary { get; set; }

        public string Thk1Summary { get; set; }

        public string Thk2Summary { get; set; }

        public string Thk3Summary { get; set; }

        public string Thk4Summary { get; set; }

        public string Thk5Summary { get; set; }

        public string MeasurementUnit { get; set; }

        public string ResultMessage { get; set; }
    }
}
