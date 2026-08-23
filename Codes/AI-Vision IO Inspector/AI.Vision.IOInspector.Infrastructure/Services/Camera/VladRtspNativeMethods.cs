using System;
using System.Runtime.InteropServices;

namespace AI.Vision.IOInspector.Infrastructure.Services.Camera
{
    /// <summary>
    /// VLAD_SDK - Rev3의 RTSP 콜백 API 선언입니다.
    /// 현재 서비스는 이 함수를 직접 호출하지 않지만, RTSP 어댑터 구현 시 이 계약을 기준으로 연결합니다.
    /// </summary>
    internal static class VladRtspNativeMethods
    {
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        public delegate void RtspCallback(IntPtr vladId, string userName, int uiType, int monitorIndex, IntPtr display);

        [DllImport("VLAD_SDK.dll", CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi)]
        public static extern void VLAD_Rtsp_Info_Client_Registration(IntPtr vladId, string urlInfo, string userName, int uiType, int monitorIndex, RtspCallback callback);

        [DllImport("VLAD_SDK.dll", CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi)]
        public static extern void VLAD_Rtsp_Info_Client_Monitoring_Registration(IntPtr vladId, string urlInfo, int width, int height, RtspCallback callback);
    }
}
