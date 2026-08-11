namespace AI.Vision.IOInspector.Infrastructure.Services.Camera
{
    /// <summary>
    /// Config.json의 CUSTOM.{Site}에 저장하는 검사 화면 공통 설정입니다.
    /// VLAD 네이티브 검출 임계값과 혼동하지 않도록 화면 판정 점수와 유사도 검색 점수를 분리합니다.
    /// </summary>
    public class InspectionRuntimeSettings
    {
        public InspectionRuntimeSettings()
        {
            InspectionPassScoreThreshold = 95m;
            SinglePartSimilarityThreshold = 99m;
        }

        /// <summary>
        /// AI가 반환한 검사 Score가 이 값 이상일 때 이미지 검사 PASS 후보가 됩니다.
        /// 최종 OK는 측정값 기준 비교까지 모두 통과해야 합니다.
        /// </summary>
        public decimal InspectionPassScoreThreshold { get; set; }

        /// <summary>
        /// 향후 VLAD_Search_Mat/Data가 제공되었을 때 단일품목 유사도 결과를 표시할 최소 Score입니다.
        /// </summary>
        public decimal SinglePartSimilarityThreshold { get; set; }
    }
}
