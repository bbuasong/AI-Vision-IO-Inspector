using System;
using System.ComponentModel;
using AI.Vision.IOInspector.Domain.Enums;
using AI.Vision.IOInspector.Domain.Models;

namespace AI.Vision.IOInspector.App.ViewModels
{
    /// <summary>
    /// 옵션 화면에서 카메라 6대의 설정과 실제 연결 상태를 표시/수정하기 위한 ViewModel입니다.
    /// IP, Port, 계정, RTSP 경로를 저장하면 CameraConfigurationStore의 JSON 설정으로 반영됩니다.
    /// </summary>
    public class CameraChannelStatusViewModel : INotifyPropertyChanged
    {
        private string _channelId;
        private ImageViewType _viewType;
        private string _displayName;
        private string _cameraModel;
        private CameraConnectionType _connectionType;
        private bool _isEnabled;
        private bool _isConnected;
        private string _ipAddress;
        private int _port;
        private string _userName;
        private string _password;
        private string _serialNumber;
        private string _deviceUserId;
        private string _cameraKey;
        private string _rtspUrl;
        private string _streamPath;
        private int _nvrChannel;
        private int _width;
        private int _height;
        private int _fps;
        private double _exposureTime;
        private double _gain;
        private CameraTriggerMode _triggerMode;
        private string _message;
        private string _lastFramePath;
        private DateTime _checkedAt;

        public CameraChannelStatusViewModel(CameraChannelStatus status)
        {
            ApplyStatus(status);
        }

        public event PropertyChangedEventHandler PropertyChanged;

        public string ChannelId
        {
            get { return _channelId; }
            set { SetProperty(ref _channelId, value, "ChannelId"); }
        }

        public ImageViewType ViewTypeValue
        {
            get { return _viewType; }
            set
            {
                if (SetProperty(ref _viewType, value, "ViewTypeValue"))
                {
                    OnPropertyChanged("ViewType");
                }
            }
        }

        public string ViewType
        {
            get { return _viewType.ToString(); }
        }

        public string DisplayName
        {
            get { return _displayName; }
            set { SetProperty(ref _displayName, value, "DisplayName"); }
        }

        public string CameraModel
        {
            get { return _cameraModel; }
            set { SetProperty(ref _cameraModel, value, "CameraModel"); }
        }

        public CameraConnectionType ConnectionTypeValue
        {
            get { return _connectionType; }
            set
            {
                if (SetProperty(ref _connectionType, value, "ConnectionTypeValue"))
                {
                    OnPropertyChanged("ConnectionType");
                }
            }
        }

        public string ConnectionType
        {
            get { return _connectionType.ToString(); }
            set
            {
                CameraConnectionType parsed;
                if (Enum.TryParse(value, true, out parsed))
                {
                    ConnectionTypeValue = parsed;
                }
            }
        }

        public bool IsEnabled
        {
            get { return _isEnabled; }
            set
            {
                if (SetProperty(ref _isEnabled, value, "IsEnabled"))
                {
                    OnPropertyChanged("EnabledText");
                }
            }
        }

        public string EnabledText
        {
            get { return _isEnabled ? "사용" : "미사용"; }
        }

        public bool IsConnected
        {
            get { return _isConnected; }
            set
            {
                if (SetProperty(ref _isConnected, value, "IsConnected"))
                {
                    OnPropertyChanged("ConnectedText");
                }
            }
        }

        public string ConnectedText
        {
            get { return _isConnected ? "연결됨" : "미연결"; }
        }

        public string IpAddress
        {
            get { return _ipAddress; }
            set { SetProperty(ref _ipAddress, value, "IpAddress"); }
        }

        public int Port
        {
            get { return _port; }
            set { SetProperty(ref _port, value, "Port"); }
        }

        public string UserName
        {
            get { return _userName; }
            set { SetProperty(ref _userName, value, "UserName"); }
        }

        public string Password
        {
            get { return _password; }
            set { SetProperty(ref _password, value, "Password"); }
        }

        public string SerialNumber
        {
            get { return _serialNumber; }
            set { SetProperty(ref _serialNumber, value, "SerialNumber"); }
        }

        public string DeviceUserId
        {
            get { return _deviceUserId; }
            set { SetProperty(ref _deviceUserId, value, "DeviceUserId"); }
        }

        public string CameraKey
        {
            get { return _cameraKey; }
            set { SetProperty(ref _cameraKey, value, "CameraKey"); }
        }

        public string RtspUrl
        {
            get { return _rtspUrl; }
            set { SetProperty(ref _rtspUrl, value, "RtspUrl"); }
        }

        public string StreamPath
        {
            get { return _streamPath; }
            set { SetProperty(ref _streamPath, value, "StreamPath"); }
        }

        public int NvrChannel
        {
            get { return _nvrChannel; }
            set { SetProperty(ref _nvrChannel, value, "NvrChannel"); }
        }

        public string Resolution
        {
            get { return _width.ToString() + " x " + _height.ToString(); }
        }

        public int Width
        {
            get { return _width; }
            set
            {
                if (SetProperty(ref _width, value, "Width"))
                {
                    OnPropertyChanged("Resolution");
                }
            }
        }

        public int Height
        {
            get { return _height; }
            set
            {
                if (SetProperty(ref _height, value, "Height"))
                {
                    OnPropertyChanged("Resolution");
                }
            }
        }

        public int Fps
        {
            get { return _fps; }
            set { SetProperty(ref _fps, value, "Fps"); }
        }

        public double ExposureTime
        {
            get { return _exposureTime; }
            set { SetProperty(ref _exposureTime, value, "ExposureTime"); }
        }

        public double Gain
        {
            get { return _gain; }
            set { SetProperty(ref _gain, value, "Gain"); }
        }

        public CameraTriggerMode TriggerModeValue
        {
            get { return _triggerMode; }
            set
            {
                if (SetProperty(ref _triggerMode, value, "TriggerModeValue"))
                {
                    OnPropertyChanged("TriggerMode");
                }
            }
        }

        public string TriggerMode
        {
            get { return _triggerMode.ToString(); }
            set
            {
                CameraTriggerMode parsed;
                if (Enum.TryParse(value, true, out parsed))
                {
                    TriggerModeValue = parsed;
                }
            }
        }

        public string Message
        {
            get { return _message; }
            set { SetProperty(ref _message, value, "Message"); }
        }

        public string LastFramePath
        {
            get { return _lastFramePath; }
            set { SetProperty(ref _lastFramePath, value, "LastFramePath"); }
        }

        public string CheckedAt
        {
            get { return _checkedAt.ToString("yyyy-MM-dd HH:mm:ss"); }
        }

        public CameraChannelConfig ToConfig()
        {
            CameraChannelConfig config = new CameraChannelConfig();
            config.ChannelId = ChannelId;
            config.ViewType = ViewTypeValue;
            config.DisplayName = DisplayName;
            config.CameraModel = CameraModel;
            config.ConnectionType = ConnectionTypeValue;
            config.IsEnabled = IsEnabled;
            config.IpAddress = IpAddress;
            config.Port = Port;
            config.UserName = UserName;
            config.Password = Password;
            config.SerialNumber = SerialNumber;
            config.DeviceUserId = DeviceUserId;
            config.CameraKey = CameraKey;
            // RTSP URL은 IP/Port/User/Password/StreamPath에서 매번 다시 생성합니다.
            // 화면에 표시된 생성 URL을 그대로 저장하면 비밀번호를 바꿔도 예전 URL이 우선 적용될 수 있습니다.
            config.RtspUrl = string.Empty;
            config.StreamPath = StreamPath;
            config.NvrChannel = NvrChannel;
            config.Width = Width;
            config.Height = Height;
            config.Fps = Fps;
            config.ExposureTime = ExposureTime;
            config.Gain = Gain;
            config.TriggerMode = TriggerModeValue;
            return config;
        }

        public void ApplyStatus(CameraChannelStatus status)
        {
            if (status == null)
            {
                return;
            }

            ChannelId = status.ChannelId;
            ViewTypeValue = status.ViewType;
            DisplayName = status.DisplayName;
            CameraModel = status.CameraModel;
            ConnectionTypeValue = status.ConnectionType;
            IsEnabled = status.IsEnabled;
            IsConnected = status.IsConnected;
            IpAddress = status.IpAddress;
            Port = status.Port <= 0 ? 554 : status.Port;
            UserName = status.UserName;
            Password = status.Password;
            SerialNumber = status.SerialNumber;
            DeviceUserId = status.DeviceUserId;
            CameraKey = status.CameraKey;
            RtspUrl = status.RtspUrl;
            StreamPath = string.IsNullOrWhiteSpace(status.StreamPath) ? "trackID=1" : status.StreamPath;
            NvrChannel = status.NvrChannel;
            Width = status.Width;
            Height = status.Height;
            Fps = status.Fps;
            ExposureTime = status.ExposureTime;
            Gain = status.Gain;
            TriggerModeValue = status.TriggerMode;
            Message = status.Message;
            LastFramePath = status.LastFramePath;
            _checkedAt = status.CheckedAt;
            OnPropertyChanged("CheckedAt");
        }

        private bool SetProperty<T>(ref T storage, T value, string propertyName)
        {
            if (object.Equals(storage, value))
            {
                return false;
            }

            storage = value;
            OnPropertyChanged(propertyName);
            return true;
        }

        private void OnPropertyChanged(string propertyName)
        {
            PropertyChangedEventHandler handler = PropertyChanged;
            if (handler != null)
            {
                handler(this, new PropertyChangedEventArgs(propertyName));
            }
        }
    }
}
