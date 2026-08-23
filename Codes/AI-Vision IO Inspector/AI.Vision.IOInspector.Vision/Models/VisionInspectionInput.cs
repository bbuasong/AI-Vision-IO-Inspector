using System;
using System.Collections.Generic;
using AI.Vision.IOInspector.Domain.Models;
using AI.Vision.IOInspector.Domain.Enums;

namespace AI.Vision.IOInspector.Vision.Models
{
    /// <summary>
    /// 애플리케이션 검사 흐름에서 Vision 추론 엔진으로 전달하는 입력 묶음입니다.
    /// 선택된 부품 기준정보와 촬영된 카메라 이미지를 함께 담습니다.
    /// </summary>
    public class VisionInspectionInput
    {
        public VisionInspectionInput()
        {
            CapturedImages = new List<CapturedImage>();
            MeasurementPoints = new List<VisionMeasurementPointInput>();
        }

        public Part Part { get; set; }

        public IList<CapturedImage> CapturedImages { get; private set; }

        public IList<VisionMeasurementPointInput> MeasurementPoints { get; private set; }

        /// <summary>
        /// 한 번의 검사 요청을 구분하는 애플리케이션 생성 식별자입니다.
        /// 현재 4인자 VLAD DLL에는 전달되지 않지만 향후 JSON Context 계약에서 사용합니다.
        /// </summary>
        public string InspectionId { get; set; }

        /// <summary>
        /// 검사 캡처가 시작된 시각입니다. 이미지별 파일 시각과 구분해 요청 단위로 고정합니다.
        /// </summary>
        public DateTime CaptureTime { get; set; }

        /// <summary>
        /// Config.json에서 읽은 화면 PASS/FAIL Score 기준입니다. 범위는 0~100입니다.
        /// </summary>
        public decimal InspectionPassScoreThreshold { get; set; }

        public void LoadMeasurementPointsFromPart()
        {
            MeasurementPoints.Clear();
            if (Part == null || Part.MeasurementRegions == null)
            {
                return;
            }

            foreach (MeasurementRegion region in Part.MeasurementRegions)
            {
                // 카메라마다 다섯 개까지 보냅니다.
                // 전체를 세면 Top 다섯 개를 담은 뒤 Thickness 측정부가 AI 에 실리지 않습니다.
                if (region == null || CountPointsByViewType(region.ViewType) >= MeasurementPointPolicy.MaxCount)
                {
                    continue;
                }

                VisionMeasurementPointInput point = new VisionMeasurementPointInput();
                point.MeasurementRegionId = region.Id;
                point.IndexNo = region.IndexNo;
                point.ItemType = region.ItemType;
                point.ViewType = region.ViewType;
                point.LineColor = string.IsNullOrWhiteSpace(region.LineColor)
                    ? MeasurementPointPolicy.GetDefaultColor(region.IndexNo)
                    : region.LineColor;
                point.NominalValue = region.NominalValue;

                // AI 요청 JSON은 아래쪽 허용값을 음수로 표기하는 계약입니다.
                // MeasurementRegion은 크기(양수)만 들고 있으므로 여기서 부호를 붙입니다.
                point.ToleranceMin = region.SignedToleranceMin;
                point.ToleranceMax = region.SignedToleranceMax;
                point.Tolerance = System.Math.Max(region.ToleranceMin, region.ToleranceMax);
                point.X1 = region.X1;
                point.Y1 = region.Y1;
                point.X2 = region.X2;
                point.Y2 = region.Y2;
                point.Unit = string.IsNullOrWhiteSpace(region.Unit) ? "mm" : region.Unit;
                MeasurementPoints.Add(point);
            }
        }

        /// <summary>
        /// 이미 담은 측정부 가운데 그 카메라의 것이 몇 개인지 셉니다.
        /// </summary>
        private int CountPointsByViewType(ImageViewType viewType)
        {
            int count = 0;
            foreach (VisionMeasurementPointInput point in MeasurementPoints)
            {
                if (point != null && point.ViewType == viewType)
                {
                    count++;
                }
            }

            return count;
        }
    }
}
