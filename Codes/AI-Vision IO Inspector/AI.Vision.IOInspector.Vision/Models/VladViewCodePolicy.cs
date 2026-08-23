using AI.Vision.IOInspector.Domain.Enums;
using AI.Vision.IOInspector.Domain.Models;

namespace AI.Vision.IOInspector.Vision.Models
{
    /// <summary>
    /// AI DLL 계약의 카메라 위치 코드(viewName)를 다루는 한 곳입니다.
    ///
    /// <para>
    /// 계약상 viewName은 1부터 6까지이고 우리 <see cref="ImageViewType"/>보다 1 큽니다.
    ///   Top=1, Front=2, Back=3, Left=4, Right=5, Thickness=6
    /// </para>
    ///
    /// <para>
    /// 이 변환이 여러 파일에 흩어져 있으면 한쪽만 고쳐도 알아채기 어렵습니다.
    /// 실제로 측정부 판정이 6번(Thickness)에만 붙어 있어, Top에 측정부를 두어도
    /// 판정이 나오지 않던 일이 있었습니다. 그래서 규칙을 여기로 모읍니다.
    /// </para>
    /// </summary>
    public static class VladViewCodePolicy
    {
        /// <summary>계약상 쓸 수 있는 가장 작은 코드입니다.</summary>
        public const int MinViewCode = 1;

        /// <summary>계약상 쓸 수 있는 가장 큰 코드입니다.</summary>
        public const int MaxViewCode = 6;

        /// <summary>우리 카메라 값을 계약 코드로 바꿉니다.</summary>
        public static int FromViewType(ImageViewType viewType)
        {
            return (int)viewType + 1;
        }

        /// <summary>계약 코드를 우리 카메라 값으로 바꿉니다. 범위를 벗어나면 미분류입니다.</summary>
        public static ImageViewType ToViewType(int viewCode)
        {
            if (viewCode < MinViewCode || viewCode > MaxViewCode)
            {
                return ImageViewType.Unclassified;
            }

            return (ImageViewType)(viewCode - 1);
        }

        public static bool IsValidViewCode(int viewCode)
        {
            return viewCode >= MinViewCode && viewCode <= MaxViewCode;
        }

        /// <summary>
        /// 이 코드의 카메라에 측정부를 둘 수 있는지입니다.
        ///
        /// <para>
        /// 측정부 판정을 붙일지 정할 때 씁니다. 코드 숫자를 직접 비교하면
        /// 나중에 측정부를 쓸 카메라가 늘어도 따라오지 않습니다.
        /// </para>
        /// </summary>
        public static bool IsMeasurementViewCode(int viewCode)
        {
            return MeasurementPointPolicy.IsSupportedViewType(ToViewType(viewCode));
        }
    }
}
