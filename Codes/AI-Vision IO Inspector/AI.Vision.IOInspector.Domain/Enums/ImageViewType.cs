namespace AI.Vision.IOInspector.Domain.Enums
{
    /// <summary>
    /// 검사 및 기준 이미지가 어느 방향의 뷰인지 구분합니다.
    /// 요구사항 명세서의 Top/Front/Back/Left/Right/Thickness 구성과 미분류 상태를 반영합니다.
    /// </summary>
    public enum ImageViewType
    {
        Top = 0,
        Front = 1,
        Back = 2,
        Left = 3,
        Right = 4,
        Thickness = 5,
        Unclassified = 6
    }
}
