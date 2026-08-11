using System.Collections.Generic;

namespace AI.Vision.IOInspector.Application.Models
{
    /// <summary>
    /// VLAD_Search_Mat / VLAD_Search_Data 후보 목록 JSON 호출 결과입니다.
    /// </summary>
    public class ReferenceImageSimilarityResult
    {
        public ReferenceImageSimilarityResult()
        {
            Candidates = new List<ReferenceImageSimilarityCandidate>();
        }

        public bool IsSuccess { get; set; }

        public string Message { get; set; }

        public IList<ReferenceImageSimilarityCandidate> Candidates { get; private set; }

    }
}
