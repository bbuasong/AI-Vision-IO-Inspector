namespace AI.Vision.IOInspector.Application.Interfaces
{
    /// <summary>
    /// 옵션에서 변경한 검사 Score 기준을 AI 입력 Context에 전달하기 위한 선택 인터페이스입니다.
    /// AI 구현체가 이 인터페이스를 지원하지 않아도 최종 애플리케이션 판정은 동일한 Config 기준을 사용합니다.
    /// </summary>
    public interface IInspectionScoreSettings
    {
        void SetInspectionPassScoreThreshold(decimal scoreThreshold);
    }
}
