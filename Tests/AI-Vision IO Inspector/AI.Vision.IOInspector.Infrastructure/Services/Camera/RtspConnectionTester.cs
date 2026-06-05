using System;
using System.IO;
using System.Net.Sockets;
using System.Text;
using AI.Vision.IOInspector.Domain.Enums;
using AI.Vision.IOInspector.Domain.Models;

namespace AI.Vision.IOInspector.Infrastructure.Services.Camera
{
    /// <summary>
    /// 옵션 화면의 상태 새로고침에서 실제 카메라/NVR RTSP 포트와 RTSP 응답을 확인합니다.
    /// 영상 디코딩이 아니라 네트워크/RTSP 응답 확인용이며, 프레임 수신은 RtspCameraFrameSource가 담당합니다.
    /// </summary>
    internal class RtspConnectionTester
    {
        private const int ConnectTimeoutMilliseconds = 2000;
        private const int ReadTimeoutMilliseconds = 2000;

        public CameraConnectionTestResult Test(CameraChannelConfig channel)
        {
            CameraConnectionTestResult result = new CameraConnectionTestResult();

            if (channel == null)
            {
                result.IsConnected = false;
                result.Message = "카메라 설정이 없습니다.";
                return result;
            }

            if (!channel.IsEnabled)
            {
                result.IsConnected = false;
                result.Message = "채널이 미사용으로 설정되어 있습니다.";
                return result;
            }

            if (channel.ConnectionType == CameraConnectionType.Simulated)
            {
                result.IsConnected = false;
                result.Message = "시뮬레이션 모드입니다. 실제 카메라 연결 상태가 아닙니다.";
                return result;
            }

            if (channel.ConnectionType == CameraConnectionType.File)
            {
                bool exists = !string.IsNullOrWhiteSpace(channel.SnapshotFilePath) && File.Exists(channel.SnapshotFilePath);
                result.IsConnected = exists;
                result.Message = exists ? "파일 소스 확인됨" : "파일 소스가 없거나 경로가 잘못되었습니다.";
                return result;
            }

            if (channel.ConnectionType == CameraConnectionType.DirectSdk)
            {
                result.IsConnected = false;
                result.Message = "Direct SDK 실제 연결은 아직 구현되지 않았습니다. RTSP로 먼저 검증하세요.";
                return result;
            }

            if (channel.ConnectionType != CameraConnectionType.Rtsp && channel.ConnectionType != CameraConnectionType.NvrRtsp)
            {
                result.IsConnected = false;
                result.Message = "지원하지 않는 연결 방식입니다: " + channel.ConnectionType.ToString();
                return result;
            }

            return TestRtsp(channel);
        }

        private CameraConnectionTestResult TestRtsp(CameraChannelConfig channel)
        {
            CameraConnectionTestResult result = new CameraConnectionTestResult();
            string rtspUrl = RtspUrlBuilder.Build(channel);
            if (string.IsNullOrWhiteSpace(rtspUrl))
            {
                result.IsConnected = false;
                result.Message = "RTSP URL 또는 IP 주소가 설정되지 않았습니다.";
                return result;
            }

            Uri uri;
            if (!Uri.TryCreate(rtspUrl, UriKind.Absolute, out uri))
            {
                result.IsConnected = false;
                result.Message = "RTSP URL 형식이 올바르지 않습니다.";
                return result;
            }

            int port = uri.Port > 0 ? uri.Port : 554;
            using (TcpClient client = new TcpClient())
            {
                IAsyncResult asyncResult = client.BeginConnect(uri.Host, port, null, null);
                bool connected = asyncResult.AsyncWaitHandle.WaitOne(ConnectTimeoutMilliseconds);
                if (!connected)
                {
                    result.IsConnected = false;
                    result.Message = "RTSP 포트 연결 시간 초과: " + uri.Host + ":" + port.ToString();
                    return result;
                }

                client.EndConnect(asyncResult);
                client.ReceiveTimeout = ReadTimeoutMilliseconds;
                client.SendTimeout = ReadTimeoutMilliseconds;

                try
                {
                    return SendRtspOptions(client, uri, channel);
                }
                catch (IOException)
                {
                    result.IsConnected = true;
                    result.Message = "RTSP 포트 연결됨. RTSP 응답 읽기는 시간 초과되었습니다.";
                    return result;
                }
                catch (SocketException ex)
                {
                    result.IsConnected = false;
                    result.Message = "RTSP 응답 확인 실패: " + ex.Message;
                    return result;
                }
            }
        }

        private CameraConnectionTestResult SendRtspOptions(TcpClient client, Uri uri, CameraChannelConfig channel)
        {
            CameraConnectionTestResult result = new CameraConnectionTestResult();
            NetworkStream stream = client.GetStream();
            string request = BuildOptionsRequest(uri, channel);
            byte[] requestBytes = Encoding.ASCII.GetBytes(request);
            stream.Write(requestBytes, 0, requestBytes.Length);

            byte[] responseBytes = new byte[2048];
            int readCount = stream.Read(responseBytes, 0, responseBytes.Length);
            string response = readCount > 0 ? Encoding.ASCII.GetString(responseBytes, 0, readCount) : string.Empty;

            if (response.Contains("RTSP/1.0 200"))
            {
                result.IsConnected = true;
                result.Message = "RTSP 응답 OK";
                return result;
            }

            if (response.Contains("RTSP/1.0 401"))
            {
                result.IsConnected = true;
                result.Message = "RTSP 서버 응답 401 - 계정/인증 방식 확인 필요";
                return result;
            }

            if (response.StartsWith("RTSP/1.0"))
            {
                result.IsConnected = true;
                result.Message = "RTSP 응답 수신: " + ExtractFirstLine(response);
                return result;
            }

            result.IsConnected = true;
            result.Message = "RTSP 포트 연결됨. 응답 내용은 확인 필요";
            return result;
        }

        private string BuildOptionsRequest(Uri uri, CameraChannelConfig channel)
        {
            StringBuilder builder = new StringBuilder();
            builder.Append("OPTIONS ");
            builder.Append(uri.AbsoluteUri);
            builder.Append(" RTSP/1.0\r\n");
            builder.Append("CSeq: 1\r\n");
            builder.Append("User-Agent: AI-Vision-IOInspector\r\n");

            if (!string.IsNullOrWhiteSpace(channel.UserName))
            {
                string rawCredential = channel.UserName + ":" + (channel.Password ?? string.Empty);
                string encodedCredential = Convert.ToBase64String(Encoding.ASCII.GetBytes(rawCredential));
                builder.Append("Authorization: Basic ");
                builder.Append(encodedCredential);
                builder.Append("\r\n");
            }

            builder.Append("\r\n");
            return builder.ToString();
        }

        private string ExtractFirstLine(string response)
        {
            if (string.IsNullOrWhiteSpace(response))
            {
                return string.Empty;
            }

            int index = response.IndexOf("\r\n", StringComparison.Ordinal);
            if (index < 0)
            {
                return response.Trim();
            }

            return response.Substring(0, index).Trim();
        }
    }
}
