namespace AI.Vision.IOInspector.App.Stores
{
    /// <summary>
    /// 부품 목록 검색에 사용하는 화면 입력 조건입니다.
    /// 단일 검색어는 품번, 품명, 분류코드, 분류설명, 구분 전체를 대상으로 찾습니다.
    /// </summary>
    public class PartSearchCriteria
    {
        public string GlobalKeyword { get; set; }

        public string PartNo { get; set; }

        public string PartName { get; set; }

        public string CategoryCode { get; set; }

        public string CategoryDescription { get; set; }

        public string PartType { get; set; }
    }
}
