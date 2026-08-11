using System;
using AI.Vision.IOInspector.Domain.Enums;

namespace AI.Vision.IOInspector.Domain.Models
{
    /// <summary>
    /// 카메라 채널의 현재 설정과 연결/촬영 상태입니다.
    /// 옵션 화면에서 6대 카메라의 매핑과 연결 상태를 그대로 표시하기 위한 모델입니다.
    /// </summary>
    public class CameraChannelStatus
    {
        public string ChannelId { get; set; }

        public ImageViewType ViewType { get; set; }

        public string DisplayName { get; set; }

        public string CameraModel { get; set; }

        public CameraConnectionType ConnectionType { get; set; }

        public bool IsEnabled { get; set; }

        public bool IsConnected { get; set; }

        public string IpAddress { get; set; }

        public int Port { get; set; }

        public string UserName { get; set; }

        public string Password { get; set; }

        public string SerialNumber { get; set; }

        public string DeviceUserId { get; set; }

        public string CameraKey { get; set; }

        public int CamX { get; set; }

        public int CamY { get; set; }

        public string RtspUrl { get; set; }

        /// <summary>
        /// 검사 화면 미리보기 전용 RTSP 주소입니다. 비어 있으면 RtspUrl을 사용합니다.
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

        public string Message { get; set; }

        public string LastFramePath { get; set; }

        public DateTime CheckedAt { get; set; }
    }
}
