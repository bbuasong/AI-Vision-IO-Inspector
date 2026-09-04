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
            HideInspectionScore = false;
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

        /// <summary>
        /// 검사 화면 여섯 칸에서 Score 배지를 감출지입니다.
        ///
        /// <para>
        /// 현장에서 점수는 필요 없고 합불만 보고 싶다는 요청으로 넣었습니다. 켜면 점수 배지만
        /// 사라지고 PASS/FAIL 은 그대로 보입니다. 표시만 감추는 것이라 점수는 판정에 그대로 쓰이고
        /// 이력과 결과 이미지에도 남습니다.
        /// </para>
        /// </summary>
        public bool HideInspectionScore { get; set; }
    }
}
