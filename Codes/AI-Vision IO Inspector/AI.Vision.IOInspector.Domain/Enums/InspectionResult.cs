namespace AI.Vision.IOInspector.Domain.Enums
{
    /// <summary>
    /// 검사 최종 판정입니다. 시스템 오류는 Fail과 분리해서 관리해야 합니다.
    ///
    /// 화면 표기와 용어를 맞추기 위해 2026-08-12에 Ok/Ng에서 Pass/Fail로 이름을 변경했습니다.
    /// 저장된 값은 int 그대로이므로 기존 검사 이력의 의미는 바뀌지 않습니다.
    /// </summary>
    public enum InspectionResult
    {
        NotInspected = 0,
        Pass = 1,
        Fail = 2,
        Error = 3
    }
}
