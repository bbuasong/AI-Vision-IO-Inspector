namespace AI.Vision.IOInspector.Domain.Enums
{
    /// <summary>
    /// 카메라 영상을 가져오는 연결 방식을 구분합니다.
    /// 실제 장비 투입 전에는 Simulated를 사용하고, 현장에서는 DirectSdk 또는 NvrRtsp를 선택합니다.
    /// </summary>
    public enum CameraConnectionType
    {
        Simulated = 0,
        DirectSdk = 1,
        Rtsp = 2,
        NvrRtsp = 3,
        File = 4
    }
}
