using System.Collections.Generic;
using AI.Vision.IOInspector.Domain.Models;

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

        public void LoadMeasurementPointsFromPart()
        {
            MeasurementPoints.Clear();
            if (Part == null || Part.MeasurementRegions == null)
            {
                return;
            }

            foreach (MeasurementRegion region in Part.MeasurementRegions)
            {
                if (region == null || MeasurementPoints.Count >= MeasurementPointPolicy.MaxCount)
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
                point.ToleranceMin = region.ToleranceMin;
                point.ToleranceMax = region.ToleranceMax;
                point.Tolerance = System.Math.Max(System.Math.Abs(region.ToleranceMin), System.Math.Abs(region.ToleranceMax));
                point.X1 = region.X1;
                point.Y1 = region.Y1;
                point.X2 = region.X2;
                point.Y2 = region.Y2;
                point.Unit = string.IsNullOrWhiteSpace(region.Unit) ? "mm" : region.Unit;
                MeasurementPoints.Add(point);
            }
        }
    }
}
