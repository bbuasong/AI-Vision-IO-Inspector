using System.Collections.Generic;
using System.Globalization;
using AI.Vision.IOInspector.Domain.Enums;

namespace AI.Vision.IOInspector.Domain.Models
{
    /// <summary>
    /// 측정부의 개수, 번호, 이름, 색상 규칙을 모든 프로젝트에서 같게 쓰기 위한 정책입니다.
    ///
    /// <para>
    /// 측정부는 카메라(View)마다 따로 관리합니다. Top에 5개, Thickness에 5개를 둘 수 있고
    /// 번호는 각각 1부터 셉니다. AI에는 이미지 한 장마다 그 방향의 측정부만 보내므로,
    /// 번호가 1부터 순차로 이어져야 한다는 계약을 이 방식으로 지킬 수 있습니다.
    /// </para>
    ///
    /// <para>
    /// 부품 전체에서 번호를 매기면 Top이 1·3번, Thickness가 2번처럼 흩어져
    /// 어느 쪽도 1부터 순차가 되지 않습니다. 그래서 View별 독립 번호를 씁니다.
    /// </para>
    /// </summary>
    public static class MeasurementPointPolicy
    {
        /// <summary>한 카메라에 둘 수 있는 측정부 수입니다.</summary>
        public const int MaxCount = 5;

        /// <summary>
        /// 측정부 선에 쓰는 기본 색입니다.
        ///
        /// <para>
        /// 원색 그대로 씁니다. 선이 사진 위에 그어지므로 색이 흐리면 배경에 묻혀
        /// 어느 선이 몇 번인지 가리기 어렵습니다.
        /// </para>
        ///
        /// <para>
        /// 다섯 색은 서로 섞이지 않게 골랐습니다.
        ///   빨강 · 주황 · 노랑 · 초록 · 파랑
        /// </para>
        /// </summary>
        private static readonly string[] DefaultColors =
        {
            "#FF0000",
            "#FF8000",
            "#FFFF00",
            "#00FF00",
            "#0000FF"
        };

        public static string GetDefaultColor(int indexNo)
        {
            int colorIndex = indexNo <= 0 ? 0 : (indexNo - 1) % DefaultColors.Length;
            return DefaultColors[colorIndex];
        }

        /// <summary>
        /// 측정부를 둘 수 있는 카메라입니다.
        /// 여기에 방향을 더하면 화면과 저장이 함께 따라옵니다.
        /// </summary>
        public static IList<ImageViewType> GetSupportedViewTypes()
        {
            return new List<ImageViewType>
            {
                ImageViewType.Top,
                ImageViewType.Thickness
            };
        }

        public static bool IsSupportedViewType(ImageViewType viewType)
        {
            foreach (ImageViewType supported in GetSupportedViewTypes())
            {
                if (supported == viewType)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// 화면에 적을 측정부 이름입니다.
        ///   예) Top 1, Thk 2
        ///
        /// <para>
        /// 이름에 하이픈을 쓰지 않습니다. 저장할 때 "이름 - 항목" 형태에서 하이픈으로
        /// 항목을 잘라내는 곳이 여러 곳이라, 이름에 하이픈이 들어가면 항목이 잘못 잘립니다.
        /// </para>
        /// </summary>
        public static string BuildPointName(ImageViewType viewType, int indexNo)
        {
            return GetViewShortName(viewType) + " " + indexNo.ToString(CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// 화면에서 카메라를 짧게 부르는 이름입니다. 목록 열이 좁아 전체 이름은 잘립니다.
        /// </summary>
        public static string GetViewShortName(ImageViewType viewType)
        {
            if (viewType == ImageViewType.Thickness)
            {
                return "Thk";
            }

            // 카메라가 정해지지 않은 측정부입니다. 화면에 그대로 "Unclassified"라고 적으면
            // 열 폭을 넘고 무슨 뜻인지도 알기 어려워 짧은 우리말로 적습니다.
            if (viewType == ImageViewType.Unclassified)
            {
                return "미지정";
            }

            return viewType.ToString();
        }

        /// <summary>
        /// 이 카메라에 다음으로 붙일 번호입니다. 카메라마다 1부터 셉니다.
        /// </summary>
        public static int ResolveNextIndexNo(IEnumerable<MeasurementRegion> regions, ImageViewType viewType)
        {
            int maxIndexNo = 0;
            if (regions != null)
            {
                foreach (MeasurementRegion region in regions)
                {
                    if (region == null || region.ViewType != viewType)
                    {
                        continue;
                    }

                    if (region.IndexNo > maxIndexNo)
                    {
                        maxIndexNo = region.IndexNo;
                    }
                }
            }

            return maxIndexNo + 1;
        }

        /// <summary>
        /// 이 카메라에 측정부를 더 넣을 수 있는지입니다.
        /// </summary>
        public static bool CanAddMore(IEnumerable<MeasurementRegion> regions, ImageViewType viewType)
        {
            return CountByViewType(regions, viewType) < MaxCount;
        }

        public static int CountByViewType(IEnumerable<MeasurementRegion> regions, ImageViewType viewType)
        {
            int count = 0;
            if (regions == null)
            {
                return 0;
            }

            foreach (MeasurementRegion region in regions)
            {
                if (region != null && region.ViewType == viewType)
                {
                    count++;
                }
            }

            return count;
        }

        /// <summary>
        /// 카메라 안에서 번호를 1부터 다시 이어 붙입니다.
        ///
        /// <para>
        /// 중간을 지우면 번호가 비는데, AI 계약은 1부터 순차로 이어지길 요구합니다.
        /// 그래서 저장하기 전에 이 정리를 거칩니다.
        /// </para>
        /// </summary>
        public static void RenumberByViewType(IList<MeasurementRegion> regions, ImageViewType viewType)
        {
            if (regions == null)
            {
                return;
            }

            int nextIndexNo = 1;
            foreach (MeasurementRegion region in regions)
            {
                if (region == null || region.ViewType != viewType)
                {
                    continue;
                }

                region.IndexNo = nextIndexNo;
                nextIndexNo++;
            }
        }
    }
}
