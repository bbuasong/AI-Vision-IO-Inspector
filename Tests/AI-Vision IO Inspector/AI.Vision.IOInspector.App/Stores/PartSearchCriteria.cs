namespace AI.Vision.IOInspector.App.Stores
{
    /// <summary>
    /// 부품 목록 검색에 사용하는 화면 입력 조건입니다.
    /// ViewModel은 화면 값을 이 객체에 담고, DataStore는 이 조건으로 캐시를 필터링합니다.
    /// </summary>
    public class PartSearchCriteria
    {
        public string PartNo { get; set; }

        public string PartName { get; set; }

        public string CategoryCode { get; set; }

        public string CategoryDescription { get; set; }
    }
}
