using System;

namespace AI.Vision.IOInspector.Domain.Models
{
    /// <summary>
    /// 결과 기록 이미지에 적을 검사 정보입니다.
    ///
    /// AI 결과에는 아직 방향별 Score나 방향별 치수가 따로 오지 않습니다.
    /// 현재 검사 화면도 6방향에 같은 값을 표시하므로 여기서도 검사 단위 값 하나를 6장에 공통으로 적습니다.
    /// 나중에 AI가 방향별 값을 반환하면 이 객체를 방향마다 다르게 채우기만 하면 됩니다.
    /// </summary>
    public class InspectionImageResultInfo
    {
        public string PartNo { get; set; }

        public string PartName { get; set; }

        /// <summary>검사 시작 시각입니다. 파일 이름과 폴더 이름의 기준이며 이미지에도 적습니다.</summary>
        public DateTime InspectionStartedAt { get; set; }

        /// <summary>최종 판정입니다. true면 PASS, false면 FAIL로 적습니다.</summary>
        public bool IsPass { get; set; }

        /// <summary>AI가 반환한 Score입니다. HasScore가 false면 표시하지 않습니다.</summary>
        public decimal Score { get; set; }

        /// <summary>설정된 통과 기준 Score입니다.</summary>
        public decimal ScoreThreshold { get; set; }

        public bool HasScore { get; set; }

        public decimal? DimensionWidth { get; set; }

        public decimal? DimensionDepth { get; set; }

        public decimal? DimensionHeight { get; set; }

        public string DimensionUnit { get; set; }

        /// <summary>
        /// 카메라 프레임을 받지 못해 검정 이미지로 대체한 방향이면 true입니다.
        /// 결과 이미지에도 표시해, 나중에 이미지만 봤을 때 실제 촬영본이 아니라는 것을 알 수 있게 합니다.
        /// </summary>
        public bool IsPlaceholder { get; set; }

        public bool HasDimensions
        {
            get
            {
                return DimensionWidth.HasValue || DimensionDepth.HasValue || DimensionHeight.HasValue;
            }
        }
    }
}
