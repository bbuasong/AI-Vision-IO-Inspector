namespace AI.Vision.IOInspector.Application.Models
{
    /// <summary>
    /// VLAD 유사도 검색이 방향별로 반환한 후보 한 건입니다.
    /// </summary>
    public class ReferenceImageSimilarityCandidate
    {
        public int Rank { get; set; }

        public string ViewName { get; set; }

        public string PartNo { get; set; }

        public string PartName { get; set; }

        /// <summary>
        /// 프로그램이 설정된 유사도 기준과 AI DLL 후보 점수를 비교해 계산한 학습 DB 존재 여부입니다.
        /// </summary>
        public bool ExistsInLearningDatabase { get; set; }

        /// <summary>
        /// 유사도는 UI와 Config 기준에 맞춰 0~100 범위로 사용합니다.
        /// </summary>
        public decimal Score { get; set; }
    }
}
