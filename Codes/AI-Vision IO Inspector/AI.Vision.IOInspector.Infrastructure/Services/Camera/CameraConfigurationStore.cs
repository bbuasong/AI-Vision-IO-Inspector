using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Web.Script.Serialization;
using AI.Vision.IOInspector.Infrastructure;
using AI.Vision.IOInspector.Domain.Enums;
using AI.Vision.IOInspector.Domain.Models;

namespace AI.Vision.IOInspector.Infrastructure.Services.Camera
{
    /// <summary>
    /// CFG\Config.json의 실제 CAMS 항목만 읽고 저장합니다.
    /// VLAD와 공유하는 기존 JSON 키는 유지하고 옵션 화면에서 편집하는 값만 갱신합니다.
    /// </summary>
    public class CameraConfigurationStore
    {
        private const string DefaultSiteName = "HD";
        private const string LastSectionName = "LAST";
        private const string CustomSectionName = "CUSTOM";
        private const string CamerasSectionName = "CAMS";
        private const string InspectionPassScoreThresholdKey = "INSPECTION_PASS_SCORE_THRESHOLD";
        private const string SinglePartSimilarityThresholdKey = "SINGLE_PART_SIMILARITY_THRESHOLD";
        private const string HideInspectionScoreKey = "HIDE_INSPECTION_SCORE";

        private readonly string _configFilePath;
        private readonly JavaScriptSerializer _serializer;

        public CameraConfigurationStore(string applicationBasePath)
        {
            // applicationBasePath와 무관하게 현재 EXE의 CFG만 사용합니다.
            // 배포 환경에서 개발 프로젝트의 CFG를 탐색하면 안 됩니다.
            _configFilePath = RuntimeConfigurationPathResolver.GetConfigFilePath("Config.json");
            _serializer = new JavaScriptSerializer();
            _serializer.MaxJsonLength = int.MaxValue;
            _serializer.RecursionLimit = 100;
        }

        public IList<CameraChannelConfig> Load()
        {
            if (!File.Exists(_configFilePath))
            {
                return new List<CameraChannelConfig>();
            }

            try
            {
                IDictionary<string, object> rootObject = DeserializeObject(File.ReadAllText(_configFilePath));
                return LoadChannels(rootObject);
            }
            catch
            {
                // 공유 설정 파일을 임의의 기본값으로 대체하지 않습니다.
                return new List<CameraChannelConfig>();
            }
        }

        public void Save(IList<CameraChannelConfig> channels)
        {
            string directoryPath = Path.GetDirectoryName(_configFilePath);
            if (!string.IsNullOrWhiteSpace(directoryPath))
            {
                Directory.CreateDirectory(directoryPath);
            }

            IDictionary<string, object> rootObject = LoadExistingRoot();
            IDictionary<string, object> siteObject = EnsureSiteObject(rootObject);
            IDictionary<string, object> camerasObject = EnsureObject(siteObject, CamerasSectionName);
            UpdateCameras(camerasObject, NormalizeChannels(channels));

            string json = _serializer.Serialize(rootObject);
            File.WriteAllText(_configFilePath, PrettyPrintJson(json), new UTF8Encoding(false));
        }

        /// <summary>
        /// 카메라 설정과 같은 Config.json에서 검사 Score 및 유사도 Score 설정을 읽습니다.
        /// 설정 파일이 없거나 값이 잘못된 경우에는 운영 기본값 95/99를 사용합니다.
        /// </summary>
        public InspectionRuntimeSettings LoadInspectionRuntimeSettings()
        {
            InspectionRuntimeSettings settings = new InspectionRuntimeSettings();
            if (!File.Exists(_configFilePath))
            {
                return settings;
            }

            try
            {
                IDictionary<string, object> rootObject = DeserializeObject(File.ReadAllText(_configFilePath));
                IDictionary<string, object> siteObject = ResolveSiteObject(rootObject);
                if (siteObject == null)
                {
                    return settings;
                }

                settings.InspectionPassScoreThreshold = NormalizeScore(
                    ReadDecimal(siteObject, settings.InspectionPassScoreThreshold, InspectionPassScoreThresholdKey));
                settings.SinglePartSimilarityThreshold = NormalizeScore(
                    ReadDecimal(siteObject, settings.SinglePartSimilarityThreshold, SinglePartSimilarityThresholdKey));
                settings.HideInspectionScore =
                    ReadBoolean(siteObject, settings.HideInspectionScore, HideInspectionScoreKey);
            }
            catch
            {
                // 공유 Config.json 오류가 프로그램 시작을 막지 않도록 기본값을 유지합니다.
            }

            return settings;
        }

        /// <summary>
        /// 검사 설정만 갱신하고 기존 LAST/CAMS/MODEL 등 공유 설정은 그대로 보존합니다.
        /// </summary>
        public void SaveInspectionRuntimeSettings(InspectionRuntimeSettings settings)
        {
            if (settings == null)
            {
                throw new ArgumentNullException("settings");
            }

            string directoryPath = Path.GetDirectoryName(_configFilePath);
            if (!string.IsNullOrWhiteSpace(directoryPath))
            {
                Directory.CreateDirectory(directoryPath);
            }

            IDictionary<string, object> rootObject = LoadExistingRoot();
            IDictionary<string, object> siteObject = EnsureSiteObject(rootObject);
            siteObject[InspectionPassScoreThresholdKey] = NormalizeScore(settings.InspectionPassScoreThreshold);
            siteObject[SinglePartSimilarityThresholdKey] = NormalizeScore(settings.SinglePartSimilarityThreshold);
            siteObject[HideInspectionScoreKey] = settings.HideInspectionScore;

            string json = _serializer.Serialize(rootObject);
            File.WriteAllText(_configFilePath, PrettyPrintJson(json), new UTF8Encoding(false));
        }

        private IDictionary<string, object> LoadExistingRoot()
        {
            if (File.Exists(_configFilePath))
            {
                try
                {
                    return DeserializeObject(File.ReadAllText(_configFilePath));
                }
                catch
                {
                }
            }

            return BuildNewRoot();
        }

        private IDictionary<string, object> DeserializeObject(string json)
        {
            object value = _serializer.DeserializeObject(json);
            IDictionary<string, object> dictionary = value as IDictionary<string, object>;
            if (dictionary == null)
            {
                throw new InvalidDataException("Config.json 루트가 JSON 객체가 아닙니다.");
            }

            return dictionary;
        }

        private IDictionary<string, object> BuildNewRoot()
        {
            IDictionary<string, object> rootObject = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            IDictionary<string, object> lastObject = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            lastObject["LAST_UI"] = "CUSTOM";
            lastObject["LAST_MODE"] = "CAM";
            lastObject["LAST_USER"] = DefaultSiteName;
            rootObject[LastSectionName] = lastObject;

            IDictionary<string, object> customObject = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            customObject[DefaultSiteName] = BuildDefaultSiteObject();
            rootObject[CustomSectionName] = customObject;
            return rootObject;
        }

        private IDictionary<string, object> BuildDefaultSiteObject()
        {
            IDictionary<string, object> siteObject = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            siteObject["TITLE"] = "HD Hyundai Site Solution";
            siteObject["SEL_INDEX"] = 7;
            siteObject["MODEL"] = "RuntimeData/Models/VLAD/Ex_Weight";
            siteObject["IMAGE_PATH"] = "H:/Temp_Image";
            siteObject["OUTPUT_PATH"] = "H:/Test_Img";
            siteObject["OCR_PATH"] = "H:/OCR_Temp_Img";
            siteObject["MSG_VER"] = "V1";
            siteObject[CamerasSectionName] = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            return siteObject;
        }

        private IDictionary<string, object> EnsureSiteObject(IDictionary<string, object> rootObject)
        {
            IDictionary<string, object> lastObject = EnsureObject(rootObject, LastSectionName);
            SetDefault(lastObject, "LAST_UI", "CUSTOM");
            SetDefault(lastObject, "LAST_MODE", "CAM");
            SetDefault(lastObject, "LAST_USER", DefaultSiteName);

            string siteName = ReadString(lastObject, "LAST_USER");
            if (string.IsNullOrWhiteSpace(siteName))
            {
                siteName = DefaultSiteName;
            }

            IDictionary<string, object> customObject = EnsureObject(rootObject, CustomSectionName);
            IDictionary<string, object> siteObject = ReadObject(customObject, siteName);
            if (siteObject == null)
            {
                siteObject = BuildDefaultSiteObject();
                customObject[siteName] = siteObject;
            }

            SetDefault(siteObject, "OCR_PATH", "H:/OCR_Temp_Img");

            return siteObject;
        }

        private void SetDefault(IDictionary<string, object> target, string key, object value)
        {
            if (!target.ContainsKey(key))
            {
                target[key] = value;
            }
        }

        private IDictionary<string, object> EnsureObject(IDictionary<string, object> parent, string key)
        {
            IDictionary<string, object> child = ReadObject(parent, key);
            if (child == null)
            {
                child = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
                parent[key] = child;
            }

            return child;
        }

        private IDictionary<string, object> ReadObject(IDictionary<string, object> parent, string key)
        {
            if (parent == null || !parent.ContainsKey(key))
            {
                return null;
            }

            return parent[key] as IDictionary<string, object>;
        }

        private IList<CameraChannelConfig> LoadChannels(IDictionary<string, object> rootObject)
        {
            IDictionary<string, object> siteObject = ResolveSiteObject(rootObject);
            IDictionary<string, object> camerasObject = ReadObject(siteObject, CamerasSectionName);
            if (camerasObject == null)
            {
                return new List<CameraChannelConfig>();
            }

            IList<CameraChannelConfig> defaults = BuildDefaultChannels();
            IList<CameraChannelConfig> channels = new List<CameraChannelConfig>();
            int index = 0;
            foreach (KeyValuePair<string, object> pair in camerasObject)
            {
                IDictionary<string, object> cameraObject = pair.Value as IDictionary<string, object>;
                if (cameraObject == null)
                {
                    continue;
                }

                CameraChannelConfig channel = CloneChannel(ResolveDefaultChannel(defaults, cameraObject, index));
                ApplyCameraObject(channel, cameraObject, pair.Key);
                channels.Add(channel);
                index++;
            }

            return NormalizeChannels(channels);
        }

        private IDictionary<string, object> ResolveSiteObject(IDictionary<string, object> rootObject)
        {
            if (rootObject == null)
            {
                return null;
            }

            string siteName = DefaultSiteName;
            IDictionary<string, object> lastObject = ReadObject(rootObject, LastSectionName);
            string configuredSiteName = ReadString(lastObject, "LAST_USER");
            if (!string.IsNullOrWhiteSpace(configuredSiteName))
            {
                siteName = configuredSiteName;
            }

            IDictionary<string, object> customObject = ReadObject(rootObject, CustomSectionName);
            if (customObject == null)
            {
                return null;
            }

            IDictionary<string, object> siteObject = ReadObject(customObject, siteName);
            return siteObject ?? ReadObject(customObject, DefaultSiteName);
        }

        private CameraChannelConfig ResolveDefaultChannel(
            IList<CameraChannelConfig> defaults,
            IDictionary<string, object> cameraObject,
            int index)
        {
            ImageViewType viewType;
            if (TryReadViewType(cameraObject, out viewType))
            {
                CameraChannelConfig byViewType = FindChannel(defaults, viewType);
                if (byViewType != null)
                {
                    return byViewType;
                }
            }

            if (index >= 0 && index < defaults.Count)
            {
                return defaults[index];
            }

            return defaults[0];
        }

        private void ApplyCameraObject(
            CameraChannelConfig channel,
            IDictionary<string, object> cameraObject,
            string cameraKey)
        {
            channel.CameraKey = cameraKey;
            channel.ChannelId = cameraKey;
            channel.CamX = ReadInt(cameraObject, channel.CamX, "CAM_X");
            channel.CamY = ReadInt(cameraObject, channel.CamY, "CAM_Y");
            channel.ConnectionType = ReadConnectionType(cameraObject, channel.ConnectionType);
            channel.RtspUrl = ReadString(cameraObject, "CAM_RTSP_IP", "CAM_RTSP_URL");
            // 미리보기 전용 주소는 선택 항목입니다. 없으면 비워 두고 미리보기에서 CAM_RTSP_IP를 씁니다.
            channel.PreviewRtspUrl = ReadString(cameraObject, "CAM_RTSP_PREVIEW_IP", "CAM_RTSP_PREVIEW_URL");

            ApplyOptionalCameraValues(channel, cameraObject);
            ApplyRtspUrlDetails(channel);
        }

        private void ApplyOptionalCameraValues(CameraChannelConfig channel, IDictionary<string, object> cameraObject)
        {
            string value = ReadString(cameraObject, "CAM_VIEW", "VIEW_TYPE");
            ImageViewType viewType;
            if (Enum.TryParse(value, true, out viewType))
            {
                channel.ViewType = viewType;
            }

            value = ReadString(cameraObject, "CAM_NAME");
            if (!string.IsNullOrWhiteSpace(value))
            {
                channel.DisplayName = value;
            }

            value = ReadString(cameraObject, "CAM_MODEL");
            if (!string.IsNullOrWhiteSpace(value))
            {
                channel.CameraModel = value;
            }

            channel.IsEnabled = ReadBoolean(cameraObject, true, "CAM_ENABLED");
            channel.IpAddress = ReadString(cameraObject, "CAM_IP");
            channel.Port = ReadInt(cameraObject, channel.Port, "CAM_PORT");
            channel.UserName = ReadString(cameraObject, "CAM_USER", "CAM_ID");
            channel.Password = ReadString(cameraObject, "CAM_PASSWORD", "CAM_PW");
            channel.StreamPath = ReadString(cameraObject, "CAM_STREAM_PATH");
            channel.NvrChannel = ReadInt(cameraObject, channel.NvrChannel, "CAM_NVR_CHANNEL");
            channel.Width = ReadInt(cameraObject, channel.Width, "CAM_WIDTH");
            channel.Height = ReadInt(cameraObject, channel.Height, "CAM_HEIGHT");
            channel.Fps = ReadInt(cameraObject, channel.Fps, "CAM_FPS");
        }

        private void ApplyRtspUrlDetails(CameraChannelConfig channel)
        {
            if (string.IsNullOrWhiteSpace(channel.RtspUrl))
            {
                return;
            }

            Uri uri;
            if (!Uri.TryCreate(channel.RtspUrl, UriKind.Absolute, out uri))
            {
                return;
            }

            channel.IpAddress = uri.Host;
            if (uri.Port > 0)
            {
                channel.Port = uri.Port;
            }

            channel.StreamPath = uri.PathAndQuery.TrimStart('/');
            if (!string.IsNullOrWhiteSpace(uri.UserInfo))
            {
                string[] credentials = uri.UserInfo.Split(new char[] { ':' }, 2);
                channel.UserName = Uri.UnescapeDataString(credentials[0]);
                channel.Password = credentials.Length > 1 ? Uri.UnescapeDataString(credentials[1]) : string.Empty;
            }
        }

        private void UpdateCameras(
            IDictionary<string, object> camerasObject,
            IList<CameraChannelConfig> channels)
        {
            for (int index = 0; index < channels.Count; index++)
            {
                CameraChannelConfig channel = channels[index];
                string cameraKey = string.IsNullOrWhiteSpace(channel.CameraKey)
                    ? "CAM" + index.ToString(CultureInfo.InvariantCulture)
                    : channel.CameraKey.Trim();
                IDictionary<string, object> cameraObject = ReadObject(camerasObject, cameraKey);
                if (cameraObject == null)
                {
                    cameraObject = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
                    camerasObject[cameraKey] = cameraObject;
                }

                cameraObject["CAM_VIEW"] = channel.ViewType.ToString();
                cameraObject["CAM_X"] = channel.CamX;
                cameraObject["CAM_Y"] = channel.CamY;
                cameraObject["CAM_TYPE"] = ToLegacyCamType(channel.ConnectionType);
                cameraObject["CAM_ENABLED"] = channel.IsEnabled;
                cameraObject["CAM_WIDTH"] = channel.Width;
                cameraObject["CAM_HEIGHT"] = channel.Height;
                cameraObject["CAM_FPS"] = channel.Fps;
                cameraObject["CAM_RTSP_IP"] = ResolveRtspUrl(channel);

                // 미리보기 주소는 설정된 경우에만 기록합니다.
                // 빈 값을 남기면 미리보기가 빈 주소로 연결을 시도하게 되므로 항목 자체를 지웁니다.
                if (string.IsNullOrWhiteSpace(channel.PreviewRtspUrl))
                {
                    cameraObject.Remove("CAM_RTSP_PREVIEW_IP");
                }
                else
                {
                    cameraObject["CAM_RTSP_PREVIEW_IP"] = channel.PreviewRtspUrl.Trim();
                }
            }
        }

        private string ResolveRtspUrl(CameraChannelConfig channel)
        {
            return !string.IsNullOrWhiteSpace(channel.RtspUrl)
                ? channel.RtspUrl.Trim()
                : RtspUrlBuilder.Build(channel);
        }

        private CameraConnectionType ReadConnectionType(
            IDictionary<string, object> cameraObject,
            CameraConnectionType defaultValue)
        {
            string value = ReadString(cameraObject, "CAM_CONNECTION_TYPE");
            CameraConnectionType parsed;
            if (Enum.TryParse(value, true, out parsed))
            {
                return parsed;
            }

            value = ReadString(cameraObject, "CAM_TYPE");
            if (string.IsNullOrWhiteSpace(value))
            {
                return defaultValue;
            }

            string upperValue = value.Trim().ToUpperInvariant();
            if (upperValue.Contains("DIRECT") || upperValue.Contains("SDK"))
            {
                return CameraConnectionType.DirectSdk;
            }

            if (upperValue.Contains("NVR"))
            {
                return CameraConnectionType.NvrRtsp;
            }

            if (upperValue.Contains("RTSP"))
            {
                return CameraConnectionType.Rtsp;
            }

            if (upperValue.Contains("FILE"))
            {
                return CameraConnectionType.File;
            }

            return CameraConnectionType.Simulated;
        }

        private bool TryReadViewType(IDictionary<string, object> cameraObject, out ImageViewType viewType)
        {
            return Enum.TryParse(ReadString(cameraObject, "CAM_VIEW", "VIEW_TYPE"), true, out viewType);
        }

        private string ReadString(IDictionary<string, object> source, params string[] keys)
        {
            if (source == null)
            {
                return string.Empty;
            }

            foreach (string key in keys)
            {
                if (source.ContainsKey(key) && source[key] != null)
                {
                    return Convert.ToString(source[key], CultureInfo.InvariantCulture);
                }
            }

            return string.Empty;
        }

        private int ReadInt(IDictionary<string, object> source, int defaultValue, params string[] keys)
        {
            int parsed;
            return int.TryParse(ReadString(source, keys), NumberStyles.Integer, CultureInfo.InvariantCulture, out parsed)
                ? parsed
                : defaultValue;
        }

        private decimal ReadDecimal(IDictionary<string, object> source, decimal defaultValue, params string[] keys)
        {
            decimal parsed;
            string value = ReadString(source, keys);
            if (decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out parsed))
            {
                return parsed;
            }

            if (decimal.TryParse(value, NumberStyles.Number, CultureInfo.CurrentCulture, out parsed))
            {
                return parsed;
            }

            return defaultValue;
        }

        private decimal NormalizeScore(decimal value)
        {
            if (value < 0m)
            {
                value = 0m;
            }
            else if (value > 100m)
            {
                value = 100m;
            }

            // Config.json과 UI 표시 기준을 맞추기 위해 Score는 소수점 둘째 자리까지 관리합니다.
            return decimal.Round(value, 2, MidpointRounding.AwayFromZero);
        }

        private bool ReadBoolean(IDictionary<string, object> source, bool defaultValue, params string[] keys)
        {
            string value = ReadString(source, keys);
            bool parsedBoolean;
            if (bool.TryParse(value, out parsedBoolean))
            {
                return parsedBoolean;
            }

            int parsedInteger;
            return int.TryParse(value, out parsedInteger) ? parsedInteger != 0 : defaultValue;
        }

        private IList<CameraChannelConfig> NormalizeChannels(IList<CameraChannelConfig> channels)
        {
            IList<CameraChannelConfig> normalized = new List<CameraChannelConfig>();
            if (channels == null)
            {
                return normalized;
            }

            IList<CameraChannelConfig> defaults = BuildDefaultChannels();
            int index = 0;
            foreach (CameraChannelConfig source in channels)
            {
                CameraChannelConfig channel = CloneChannel(source);
                CameraChannelConfig defaultChannel = FindChannel(defaults, channel.ViewType);
                if (defaultChannel == null && index < defaults.Count)
                {
                    defaultChannel = defaults[index];
                }

                if (defaultChannel != null)
                {
                    FillMissingValues(channel, defaultChannel);
                }

                normalized.Add(channel);
                index++;
            }

            return normalized;
        }

        private void FillMissingValues(CameraChannelConfig channel, CameraChannelConfig defaultChannel)
        {
            if (string.IsNullOrWhiteSpace(channel.DisplayName))
            {
                channel.DisplayName = defaultChannel.DisplayName;
            }

            if (string.IsNullOrWhiteSpace(channel.CameraModel))
            {
                channel.CameraModel = defaultChannel.CameraModel;
            }

            if (channel.Port <= 0)
            {
                channel.Port = defaultChannel.Port;
            }

            if (channel.Width <= 0)
            {
                channel.Width = defaultChannel.Width;
            }

            if (channel.Height <= 0)
            {
                channel.Height = defaultChannel.Height;
            }

            if (channel.Fps <= 0)
            {
                channel.Fps = defaultChannel.Fps;
            }
        }

        private IList<CameraChannelConfig> BuildDefaultChannels()
        {
            IList<CameraChannelConfig> channels = new List<CameraChannelConfig>();
            channels.Add(BuildChannel(ImageViewType.Top, "Top View", "DC-T3145G", 2448, 2048, 30, 1));
            channels.Add(BuildChannel(ImageViewType.Front, "Front View", "DC-T3145R", 2592, 1944, 30, 2));
            channels.Add(BuildChannel(ImageViewType.Back, "Back View", "DC-T3145R", 2592, 1944, 30, 3));
            channels.Add(BuildChannel(ImageViewType.Left, "Left View", "DC-T3145R", 2592, 1944, 30, 4));
            channels.Add(BuildChannel(ImageViewType.Right, "Right View", "DC-T3145R", 2592, 1944, 30, 5));
            channels.Add(BuildChannel(ImageViewType.Thickness, "Thickness", "DC-T3145G", 2448, 2048, 30, 6));
            return channels;
        }

        private CameraChannelConfig BuildChannel(
            ImageViewType viewType,
            string displayName,
            string cameraModel,
            int width,
            int height,
            int fps,
            int nvrChannel)
        {
            CameraChannelConfig channel = new CameraChannelConfig();
            channel.ChannelId = viewType.ToString().ToUpperInvariant();
            channel.ViewType = viewType;
            channel.DisplayName = displayName;
            channel.CameraModel = cameraModel;
            channel.ConnectionType = CameraConnectionType.Simulated;
            channel.IsEnabled = true;
            channel.CamX = 10;
            channel.CamY = 10;
            channel.Port = 554;
            channel.NvrChannel = nvrChannel;
            channel.Width = width;
            channel.Height = height;
            channel.Fps = fps;
            channel.TriggerMode = CameraTriggerMode.Continuous;
            return channel;
        }

        private CameraChannelConfig FindChannel(IList<CameraChannelConfig> channels, ImageViewType viewType)
        {
            if (channels == null)
            {
                return null;
            }

            foreach (CameraChannelConfig channel in channels)
            {
                if (channel.ViewType == viewType)
                {
                    return channel;
                }
            }

            return null;
        }

        private CameraChannelConfig CloneChannel(CameraChannelConfig source)
        {
            CameraChannelConfig channel = new CameraChannelConfig();
            channel.ChannelId = source.ChannelId;
            channel.ViewType = source.ViewType;
            channel.DisplayName = source.DisplayName;
            channel.CameraModel = source.CameraModel;
            channel.ConnectionType = source.ConnectionType;
            channel.IsEnabled = source.IsEnabled;
            channel.IpAddress = source.IpAddress;
            channel.Port = source.Port;
            channel.UserName = source.UserName;
            channel.Password = source.Password;
            channel.SerialNumber = source.SerialNumber;
            channel.DeviceUserId = source.DeviceUserId;
            channel.CameraKey = source.CameraKey;
            channel.CamX = source.CamX;
            channel.CamY = source.CamY;
            channel.RtspUrl = source.RtspUrl;
            channel.PreviewRtspUrl = source.PreviewRtspUrl;
            channel.StreamPath = source.StreamPath;
            channel.NvrChannel = source.NvrChannel;
            channel.Width = source.Width;
            channel.Height = source.Height;
            channel.Fps = source.Fps;
            channel.ExposureTime = source.ExposureTime;
            channel.Gain = source.Gain;
            channel.TriggerMode = source.TriggerMode;
            channel.SdkDllPath = source.SdkDllPath;
            channel.NativeDllDirectory = source.NativeDllDirectory;
            channel.SnapshotFilePath = source.SnapshotFilePath;
            return channel;
        }

        private string ToLegacyCamType(CameraConnectionType connectionType)
        {
            if (connectionType == CameraConnectionType.DirectSdk)
            {
                return "DIRECTSDK";
            }

            if (connectionType == CameraConnectionType.File)
            {
                return "FILE";
            }

            if (connectionType == CameraConnectionType.Rtsp || connectionType == CameraConnectionType.NvrRtsp)
            {
                return "RTSP";
            }

            return "SIMULATED";
        }

        private string PrettyPrintJson(string json)
        {
            StringBuilder builder = new StringBuilder();
            bool inString = false;
            bool escaped = false;
            int indent = 0;
            foreach (char character in json)
            {
                if (inString)
                {
                    builder.Append(character);
                    if (escaped)
                    {
                        escaped = false;
                    }
                    else if (character == '\\')
                    {
                        escaped = true;
                    }
                    else if (character == '"')
                    {
                        inString = false;
                    }

                    continue;
                }

                if (character == '"')
                {
                    inString = true;
                    builder.Append(character);
                }
                else if (character == '{' || character == '[')
                {
                    builder.Append(character);
                    builder.AppendLine();
                    indent++;
                    AppendIndent(builder, indent);
                }
                else if (character == '}' || character == ']')
                {
                    builder.AppendLine();
                    indent--;
                    AppendIndent(builder, indent);
                    builder.Append(character);
                }
                else if (character == ',')
                {
                    builder.Append(character);
                    builder.AppendLine();
                    AppendIndent(builder, indent);
                }
                else if (character == ':')
                {
                    builder.Append(": ");
                }
                else if (!char.IsWhiteSpace(character))
                {
                    builder.Append(character);
                }
            }

            builder.AppendLine();
            return builder.ToString();
        }

        private void AppendIndent(StringBuilder builder, int indent)
        {
            for (int index = 0; index < indent; index++)
            {
                builder.Append("  ");
            }
        }
    }
}
