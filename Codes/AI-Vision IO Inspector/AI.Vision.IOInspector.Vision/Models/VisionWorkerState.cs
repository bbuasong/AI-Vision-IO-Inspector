namespace AI.Vision.IOInspector.Vision.Models
{
    /// <summary>
    /// 카메라 수신 작업자와 AI 추론 작업자가 공통으로 사용하는 실행 상태입니다.
    /// </summary>
    public enum VisionWorkerState
    {
        Stopped = 0,
        Starting = 1,
        Running = 2,
        Stopping = 3,
        Faulted = 4
    }
}
