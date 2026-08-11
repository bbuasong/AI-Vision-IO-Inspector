using System;
using System.IO;
using System.Net.Sockets;
using System.Text;
using AI.Vision.IOInspector.Domain.Enums;
using AI.Vision.IOInspector.Domain.Models;

namespace AI.Vision.IOInspector.Infrastructure.Services.Camera
{
    /// <summary>
    /// 옵션 화면의 상태 새로고침에서 RTSP 서버가 응답하는지 1차 확인합니다.
    /// 이 클래스는 포트와 RTSP 응답만 확인하며, 실제 영상 프레임 수신 여부는 RtspCameraFrameSource가 최종 판단합니다.
    /// </summary>
    internal class RtspConnectionTester
    {
        // 2초는 현장 네트워크가 조금만 지연돼도 정상 카메라를 "연결 안 됨"으로 잘못 판정하는 값이었습니다.
        // 실제 카메라 응답 지연을 감안해 여유를 둡니다.
        private const int ConnectTimeoutMilliseconds = 5000;
        private const int ReadTimeoutMilliseconds = 5000;

        /// <summary>
        /// 이 메서드는 예외를 밖으로 던지지 않습니다.
        /// 한 채널의 예외가 옵션 화면의 상태 새로고침 전체를 중단시켜
        /// 나머지 채널 상태까지 갱신되지 않던 문제를 막기 위한 것입니다.
        /// </summary>
        public CameraConnectionTestResult Test(CameraChannelConfig channel)
        {
            try
            {
                return TestCore(channel);
            }
            catch (Exception ex)
            {
                CameraConnectionTestResult failure = new CameraConnectionTestResult();
                failure.IsConnected = false;
                failure.Message = "연결 확인 중 오류가 발생했습니다: " + ex.Message;
                return failure;
            }
        }

        private CameraConnectionTestResult TestCore(CameraChannelConfig channel)
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
                result.Message = "지원하지 않는 연결 방식입니다. " + channel.ConnectionType.ToString();
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
                bool completed = asyncResult.AsyncWaitHandle.WaitOne(ConnectTimeoutMilliseconds);
                if (!completed)
                {
                    result.IsConnected = false;
                    result.Message = "RTSP 포트 연결 시간 초과: " + uri.Host + ":" + port.ToString();
                    return result;
                }

                // WaitOne이 true인 것은 연결 시도가 "끝났다"는 뜻일 뿐 성공했다는 뜻이 아닙니다.
                // 카메라가 꺼져 연결이 거부되면 EndConnect가 SocketException을 던지는데,
                // 이 호출이 try 밖에 있어서 예외가 옵션 화면 상태 새로고침 전체를 중단시켰습니다.
                try
                {
                    client.EndConnect(asyncResult);
                }
                catch (SocketException ex)
                {
                    result.IsConnected = false;
                    result.Message = "RTSP 포트 연결 실패: " + uri.Host + ":" + port.ToString() + " (" + ex.Message + ")";
                    return result;
                }

                client.ReceiveTimeout = ReadTimeoutMilliseconds;
                client.SendTimeout = ReadTimeoutMilliseconds;

                try
                {
                    return SendRtspOptions(client, uri, channel);
                }
                catch (IOException)
                {
                    result.IsConnected = true;
                    result.Message = "RTSP 포트는 열려 있으나 응답 읽기 시간이 초과되었습니다. 실제 프레임 수신으로 최종 확인합니다.";
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
                result.Message = "RTSP 서버 응답 OK. 실제 연결됨 여부는 영상 프레임 수신 성공으로 최종 판단합니다.";
                return result;
            }

            if (response.Contains("RTSP/1.0 401"))
            {
                result.IsConnected = true;
                result.Message = "RTSP 서버가 인증을 요구합니다. ID/Password 또는 Digest 권한 확인이 필요하며, 실제 프레임 수신으로 최종 판단합니다.";
                return result;
            }

            if (response.StartsWith("RTSP/1.0"))
            {
                result.IsConnected = true;
                result.Message = "RTSP 서버 응답 수신: " + ExtractFirstLine(response) + ". 실제 프레임 수신으로 최종 판단합니다.";
                return result;
            }

            result.IsConnected = true;
            result.Message = "RTSP 포트는 열려 있으나 응답 형식 확인이 필요합니다. 실제 프레임 수신으로 최종 판단합니다.";
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
