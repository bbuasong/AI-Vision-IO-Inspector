namespace AI.Vision.IOInspector.Domain.Enums
{
    /// <summary>
    /// 메인 검사 화면에서 사용자에게 보여줄 검사 진행 상태입니다.
    /// </summary>
    public enum InspectionStatus
    {
        Idle = 0,
        PartLookup = 1,
        Ready = 2,
        Capturing = 3,
        Inferencing = 4,
        Measuring = 5,
        Judging = 6,
        Saving = 7,
        Completed = 8,
        Error = 9
    }
}
