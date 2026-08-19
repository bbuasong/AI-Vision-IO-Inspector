using System.Collections.Generic;

namespace AI.Vision.IOInspector.Vision.Models
{
    /// <summary>
    /// 카메라/AI Vision 엔진이 반환하는 표준 출력 모델입니다.
    /// 서비스 어댑터는 이 결과를 현재 애플리케이션 결과 모델로 변환합니다.
    /// </summary>
    public class VisionInspectionOutput
    {
        public VisionInspectionOutput()
        {
            Measurements = new List<VisionMeasurementValue>();
            ViewResults = new List<VisionViewInspectionResult>();
        }

        public bool IsSuccess { get; set; }

        public bool IsMatched { get; set; }

        /// <summary>
        /// IsMatched와 측정부 판정이 신규 HD DLL의 viewJudge/judge에서 직접 나온 값인지 나타냅니다.
        /// </summary>
        public bool HasAuthoritativeJudgment { get; set; }

        public string PredictedClass { get; set; }

        public decimal Confidence { get; set; }

        /// <summary>
        /// 현재 DLL 결과에서 실제 Score 토큰을 받았는지 여부입니다.
        /// false이면 Confidence의 fallback 값으로 Score 기준 판정을 하지 않습니다.
        /// </summary>
        public bool HasScore { get; set; }

        public string Message { get; set; }

        public string ModelVersion { get; set; }

        /// <summary>
        /// AI가 반환한 전체 이미지 기준의 대략적인 너비입니다.
        /// </summary>
        public decimal? DimensionWidth { get; set; }

        /// <summary>
        /// AI가 반환한 전체 이미지 기준의 대략적인 깊이입니다.
        /// </summary>
        public decimal? DimensionDepth { get; set; }

        /// <summary>
        /// AI가 반환한 전체 이미지 기준의 대략적인 높이입니다.
        /// </summary>
        public decimal? DimensionHeight { get; set; }

        public string DimensionUnit { get; set; }

        public IList<VisionMeasurementValue> Measurements { get; private set; }

        /// <summary>
        /// 방향별 검사 결과입니다. IsMatched/Confidence는 이 값들을 합친 것이므로,
        /// 방향마다 다른 값을 보여줘야 하는 곳(결과 기록 이미지 등)에서는 이쪽을 씁니다.
        /// </summary>
        public IList<VisionViewInspectionResult> ViewResults { get; private set; }
    }
}
