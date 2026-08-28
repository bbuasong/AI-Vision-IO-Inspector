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

        /// <summary>카메라 번호가 없음을 뜻하는 값입니다.</summary>
        public const int NoMonitorIndex = -1;

        /// <summary>
        /// 카메라를 번호로 바꿉니다. 여섯 대에 들지 않으면 <see cref="NoMonitorIndex"/> 입니다.
        ///
        /// <para>
        /// 예전에는 모르는 카메라를 0 번으로 돌려주었습니다. 0 번은 Top 입니다. 그래서 방향을
        /// 읽지 못한 채널이 Top 자리로 등록되어, Top 화면에 다른 카메라 그림이 올라오고
        /// Top 의 자를 자리가 엉뚱한 사진으로 덮이는 일이 생겼습니다. 모르는 것을 아무 데나
        /// 넣기보다 모른다고 말하는 편이 낫습니다.
        /// </para>
        ///
        /// <para>
        /// 부르는 쪽은 이 값이 0 보다 작은지 보고 건너뛰어야 합니다.
        /// 결과 이미지를 그리는 곳은 이미 그렇게 하고 있었는데, 여기서 0 을 돌려주는 바람에
        /// 그 방어가 듣지 않았습니다.
        /// </para>
        /// </summary>
        public static int FromViewType(ImageViewType viewType)
        {
            int monitorIndex = (int)viewType;
            return IsValid(monitorIndex) ? monitorIndex : NoMonitorIndex;
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
