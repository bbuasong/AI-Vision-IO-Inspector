using AI.Vision.IOInspector.Domain.Models;

namespace AI.Vision.IOInspector.App.ViewModels
{
    /// <summary>
    /// 옵션 화면에서 카메라 6대의 설정과 현재 상태를 표시하기 위한 ViewModel입니다.
    /// 상태 원본은 ICameraService에서 가져오며, 화면 표시용 문자열만 이곳에서 정리합니다.
    /// </summary>
    public class CameraChannelStatusViewModel
    {
        private readonly CameraChannelStatus _status;

        public CameraChannelStatusViewModel(CameraChannelStatus status)
        {
            _status = status;
        }

        public string ViewType
        {
            get { return _status.ViewType.ToString(); }
        }

        public string DisplayName
        {
            get { return _status.DisplayName; }
        }

        public string CameraModel
        {
            get { return _status.CameraModel; }
        }

        public string ConnectionType
        {
            get { return _status.ConnectionType.ToString(); }
        }

        public string EnabledText
        {
            get { return _status.IsEnabled ? "사용" : "미사용"; }
        }

        public string ConnectedText
        {
            get { return _status.IsConnected ? "연결됨" : "미연결"; }
        }

        public string IpAddress
        {
            get { return _status.IpAddress; }
        }

        public string SerialNumber
        {
            get { return _status.SerialNumber; }
        }

        public string DeviceUserId
        {
            get { return _status.DeviceUserId; }
        }

        public string CameraKey
        {
            get { return _status.CameraKey; }
        }

        public string RtspUrl
        {
            get { return _status.RtspUrl; }
        }

        public int NvrChannel
        {
            get { return _status.NvrChannel; }
        }

        public string Resolution
        {
            get { return _status.Width.ToString() + " x " + _status.Height.ToString(); }
        }

        public int Fps
        {
            get { return _status.Fps; }
        }

        public double ExposureTime
        {
            get { return _status.ExposureTime; }
        }

        public double Gain
        {
            get { return _status.Gain; }
        }

        public string TriggerMode
        {
            get { return _status.TriggerMode.ToString(); }
        }

        public string Message
        {
            get { return _status.Message; }
        }

        public string LastFramePath
        {
            get { return _status.LastFramePath; }
        }

        public string CheckedAt
        {
            get { return _status.CheckedAt.ToString("yyyy-MM-dd HH:mm:ss"); }
        }
    }
}
