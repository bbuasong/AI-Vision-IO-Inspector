namespace AI.Vision.IOInspector.Domain.Enums
{
    /// <summary>
    /// 측정부가 무엇을 재는 항목인지 구분합니다.
    ///
    /// <para>
    /// 여기의 숫자값이 그대로 AI 요청 JSON 의 <c>itemType</c> 으로 나갑니다.
    /// 사양서 VLAD_HD_Inference_Mat_요청JSON확장-2026-09-03.md 의 코드표와 같아야 하므로
    /// 값을 바꾸거나 사이에 끼워 넣지 마십시오. 새 항목은 뒤에 이어 붙입니다.
    /// </para>
    ///
    /// <para>
    /// 화면과 DB(<c>PartList_MeasurementPoints.item_type</c>)에는 한글 이름으로 남습니다.
    /// 이름과 그리는 모양은 <c>MeasurementItemTypePolicy</c> 가 함께 관리합니다.
    /// </para>
    /// </summary>
    public enum MeasurementItemType
    {
        None = 0,
        Length = 1,
        Width = 2,
        Height = 3,
        Thickness = 4,
        InnerDiameter = 5,
        OuterDiameter = 6
    }
}
