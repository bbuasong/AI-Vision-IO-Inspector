namespace AI.Vision.IOInspector.Domain.Enums
{
    /// <summary>
    /// 검사 최종 판정입니다. 시스템 오류는 NG와 분리해서 관리해야 합니다.
    /// </summary>
    public enum InspectionResult
    {
        NotInspected = 0,
        Ok = 1,
        Ng = 2,
        Error = 3
    }
}
