namespace AI.Vision.IOInspector.Infrastructure.Services.Camera
{
    /// <summary>
    /// 옵션 화면에서 실제 카메라 연결 상태를 표시하기 위한 테스트 결과입니다.
    /// </summary>
    internal class CameraConnectionTestResult
    {
        public bool IsConnected { get; set; }

        public string Message { get; set; }
    }
}
