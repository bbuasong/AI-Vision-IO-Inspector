using AI.Vision.IOInspector.Domain.Enums;

namespace AI.Vision.IOInspector.Vision.Models
{
    /// <summary>
    /// RTSP 콜백에서 카메라를 구분하는 번호(mon_idx)를 다루는 한 곳입니다.
    ///
    /// <para>
    /// 콜백은 카메라마다 이 번호를 붙여 프레임을 넘겨줍니다.
    /// 등록할 때 우리가 정해서 넘긴 값이 그대로 돌아오는 구조입니다.
    /// </para>
    ///
    /// <para>
    /// 번호는 <see cref="ImageViewType"/> 값과 같게 씁니다.
    ///   Top=0, Front=1, Back=2, Left=3, Right=4, Thickness=5
    /// 두 값이 어긋나면 다른 카메라의 화면이 엉뚱한 자리에 그려지는데,
    /// 그림만 보고는 알아채기 어렵습니다. 그래서 규칙을 여기로 모읍니다.
    /// </para>
    /// </summary>
    public static class RtspMonitorIndexPolicy
    {
        /// <summary>카메라 수만큼의 번호를 씁니다. 0부터 이 값 미만입니다.</summary>
        public const int MonitorCount = 6;

        public static int FromViewType(ImageViewType viewType)
        {
            int monitorIndex = (int)viewType;
            return IsValid(monitorIndex) ? monitorIndex : 0;
        }

        /// <summary>
        /// 번호를 카메라로 되돌립니다. 범위를 벗어나면 미분류입니다.
        /// </summary>
        public static ImageViewType ToViewType(int monitorIndex)
        {
            return IsValid(monitorIndex) ? (ImageViewType)monitorIndex : ImageViewType.Unclassified;
        }

        public static bool IsValid(int monitorIndex)
        {
            return monitorIndex >= 0 && monitorIndex < MonitorCount;
        }
    }
}
