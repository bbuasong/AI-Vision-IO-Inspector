using AI.Vision.IOInspector.Domain.Enums;

namespace AI.Vision.IOInspector.Domain.Models
{
    /// <summary>
    /// 검사 화면의 한 방향과 실제 카메라 한 대를 연결하는 설정입니다.
    /// Top/Front/Back/Left/Right/Thickness 6개 채널을 이 모델로 동일하게 관리합니다.
    /// </summary>
    public class CameraChannelConfig
    {
        public string ChannelId { get; set; }

        public ImageViewType ViewType { get; set; }

        public string DisplayName { get; set; }

        public string CameraModel { get; set; }

        public CameraConnectionType ConnectionType { get; set; }

        public bool IsEnabled { get; set; }

        public string IpAddress { get; set; }

        public string SerialNumber { get; set; }

        public string DeviceUserId { get; set; }

        public string CameraKey { get; set; }

        public string RtspUrl { get; set; }

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
