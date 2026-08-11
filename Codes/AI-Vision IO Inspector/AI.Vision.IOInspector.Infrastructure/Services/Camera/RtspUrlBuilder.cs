using System;
using AI.Vision.IOInspector.Domain.Models;

namespace AI.Vision.IOInspector.Infrastructure.Services.Camera
{
    /// <summary>
    /// IDIS RTSP 규칙을 기준으로 카메라 설정에서 접속 URL을 만듭니다.
    /// 명시된 RtspUrl이 있으면 그 값을 우선 사용하고, 없으면 IP/Port/계정/StreamPath로 생성합니다.
    /// </summary>
    public static class RtspUrlBuilder
    {
        public static string Build(CameraChannelConfig channel)
        {
            if (channel == null)
            {
                throw new ArgumentNullException("channel");
            }

            if (!string.IsNullOrWhiteSpace(channel.RtspUrl))
            {
                return channel.RtspUrl.Trim();
            }

            if (string.IsNullOrWhiteSpace(channel.IpAddress))
            {
                return string.Empty;
            }

            int port = channel.Port <= 0 ? 554 : channel.Port;
            string streamPath = string.IsNullOrWhiteSpace(channel.StreamPath) ? "trackID=1" : channel.StreamPath.Trim();
            while (streamPath.StartsWith("/"))
            {
                streamPath = streamPath.Substring(1);
            }

            string credential = string.Empty;
            if (!string.IsNullOrWhiteSpace(channel.UserName))
            {
                credential = Uri.EscapeDataString(channel.UserName.Trim());
                if (!string.IsNullOrEmpty(channel.Password))
                {
                    credential = credential + ":" + Uri.EscapeDataString(channel.Password);
                }

                credential = credential + "@";
            }

            return "rtsp://" + credential + channel.IpAddress.Trim() + ":" + port.ToString() + "/" + streamPath;
        }
    }
}
