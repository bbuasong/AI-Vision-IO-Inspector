using AI.Vision.IOInspector.Domain.Enums;

namespace AI.Vision.IOInspector.Domain.Models
{
    /// <summary>
    /// 카메라(View) 한 방향의 AI 검사 결과입니다.
    ///
    /// <para>
    /// AI는 이미지 6장을 각각 검사해 방향마다 판정과 Score, 치수를 돌려줍니다.
    /// 예전에는 이 값들을 검사 단위 하나로 합쳐서 상위에 넘겼기 때문에
    /// (판정은 6방향 AND, Score는 최대값 하나), 결과 이미지 6장에 같은 값이 적혔습니다.
    /// 방향별로 남기려면 합치기 전의 값이 필요합니다.
    /// </para>
    /// </summary>
    public class AiViewInferenceResult
    {
        public ImageViewType ViewType { get; set; }

        /// <summary>이 방향의 판정입니다. AI의 viewJudge를 그대로 옮깁니다.</summary>
        public bool IsPass { get; set; }

        /// <summary>이 방향의 Score입니다.</summary>
        public decimal Score { get; set; }

        /// <summary>AI가 이 방향의 Score를 돌려주었는지입니다.</summary>
        public bool HasScore { get; set; }

        public decimal? DimensionWidth { get; set; }

        public decimal? DimensionDepth { get; set; }

        public decimal? DimensionHeight { get; set; }

        public string DimensionUnit { get; set; }
    }
}
