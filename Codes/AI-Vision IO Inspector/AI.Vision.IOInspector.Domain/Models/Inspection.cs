using System;
using System.Collections.Generic;
using AI.Vision.IOInspector.Domain.Enums;

namespace AI.Vision.IOInspector.Domain.Models
{
    /// <summary>
    /// 한 번의 입고검사 이력입니다. DB 저장과 이력 화면 조회의 기준 데이터입니다.
    /// </summary>
    public class Inspection
    {
        public Inspection()
        {
            Measurements = new List<MeasurementResult>();
            Images = new List<CapturedImage>();
            Events = new List<EventLogEntry>();
            ViewResults = new Dictionary<ImageViewType, AiViewInferenceResult>();
            InspectedAt = DateTime.Now;
        }

        public int Id { get; set; }

        public string PartNo { get; set; }

        public string PartName { get; set; }

        public string CategoryCode { get; set; }

        public string CategoryDescription { get; set; }

        public string PartType { get; set; }

        public string InputCode { get; set; }

        public InspectionResult Result { get; set; }

        public DateTime InspectedAt { get; set; }

        public decimal ElapsedMilliseconds { get; set; }

        public string ResultMessage { get; set; }

        /// <summary>
        /// 검사 실행 시 AI가 반환한 전체 Score입니다. AI 결과에 Score가 없으면 HasAiScore는 false입니다.
        /// </summary>
        public decimal AiScore { get; set; }

        /// <summary>
        /// 이번 검사에 적용한 Config.json의 PASS/FAIL Score 기준입니다.
        /// </summary>
        public decimal AiScoreThreshold { get; set; }

        /// <summary>
        /// AI 결과 문자열에서 실제 Score를 받았는지 여부입니다.
        /// </summary>
        public bool HasAiScore { get; set; }

        /// <summary>
        /// AI가 반환한 전체 이미지 기준의 대략적인 W/D/H 값입니다.
        /// 현재 검사 화면 표시에 사용하며 값이 없으면 null로 유지합니다.
        /// </summary>
        public decimal? DimensionWidth { get; set; }

        public decimal? DimensionDepth { get; set; }

        public decimal? DimensionHeight { get; set; }

        public string DimensionUnit { get; set; }

        public IList<MeasurementResult> Measurements { get; private set; }

        public IList<CapturedImage> Images { get; private set; }

        /// <summary>
        /// 이번 검사에서 MAT API가 반환한 방향별 이미지 판정입니다.
        /// 전체 <see cref="Result"/>는 검사 단위의 최종 판정이고, 카메라 이미지 슬롯은 이 값을
        /// ViewType별로 사용해야 다른 카메라의 FAIL이 모든 이미지에 표시되지 않습니다.
        /// 이 값은 현재 검사 UI에 전달하기 위한 실행 중 데이터이며, 기존 이력 DB 형식은 변경하지 않습니다.
        /// </summary>
        public IDictionary<ImageViewType, AiViewInferenceResult> ViewResults { get; private set; }

        public IList<EventLogEntry> Events { get; private set; }
    }
}
