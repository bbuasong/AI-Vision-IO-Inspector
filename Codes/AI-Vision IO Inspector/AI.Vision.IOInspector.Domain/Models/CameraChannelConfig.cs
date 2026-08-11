using AI.Vision.IOInspector.Domain.Enums;

namespace AI.Vision.IOInspector.Domain.Models
{
    /// <summary>
    /// 검사 화면의 한 방향과 실제 카메라 한 대를 연결하는 설정입니다.
    /// Top/Front/Back/Left/Right/Thickness 6개 채널을 이 모델로 동일하게 관리합니다.
    /// </summary>
    public class CameraChannelConfig
    {
        public CameraChannelConfig()
        {
            IsEnabled = true;
        }

        public string ChannelId { get; set; }

        public ImageViewType ViewType { get; set; }

        public string DisplayName { get; set; }

        public string CameraModel { get; set; }

        public CameraConnectionType ConnectionType { get; set; }

        public bool IsEnabled { get; set; }

        public string IpAddress { get; set; }

        public int Port { get; set; }

        public string UserName { get; set; }

        public string Password { get; set; }

        public string SerialNumber { get; set; }

        public string DeviceUserId { get; set; }

        public string CameraKey { get; set; }

        public int CamX { get; set; }

        public int CamY { get; set; }

        /// <summary>
        /// 검사 촬영에 사용하는 RTSP 주소입니다. 원본 해상도가 필요하므로 메인 스트림을 지정합니다.
        /// </summary>
        public string RtspUrl { get; set; }

        /// <summary>
        /// 검사 화면 미리보기에만 사용하는 RTSP 주소입니다.
        /// 6개 채널을 모두 메인 스트림으로 계속 열어두면 NVR 동시 전송 한계에 걸려
        /// 일부 채널이 끊기므로, 미리보기는 저화질 서브 스트림으로 분리할 수 있게 합니다.
        /// 비어 있으면 RtspUrl을 그대로 사용합니다(기존 설정 호환).
        /// </summary>
        public string PreviewRtspUrl { get; set; }

        public string StreamPath { get; set; }

        public int NvrChannel { get; set; }

        public int Width { get; set; }

        public int Height { get; set; }

        public int Fps { get; set; }

        public double ExposureTime { get; set; }

        public double Gain { get; set; }

        public CameraTriggerMode TriggerMode { get; set; }

        public string SdkDllPath { get; set; }

        public string NativeDllDirectory { get; set; }

        public string SnapshotFilePath { get; set; }
    }
}
