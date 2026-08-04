using System.Collections.Generic;
using AI.Vision.IOInspector.Domain.Models;

namespace AI.Vision.IOInspector.Application.Models
{
    /// <summary>
    /// 단일품목 등록 화면에서 기준 이미지 유사도 검색 DLL로 전달하는 요청입니다.
    /// 원본 이미지는 방향별로 하나씩 VLAD_Search_Mat에 전달합니다.
    /// </summary>
    public class ReferenceImageSimilarityRequest
    {
        public ReferenceImageSimilarityRequest()
        {
            SourceImages = new List<CapturedImage>();
        }

        public IList<CapturedImage> SourceImages { get; private set; }

        /// <summary>
        /// 검색 대상 기준이미지의 부품 및 측정부 기준정보입니다.
        /// </summary>
        /// <summary>
        /// 화면에서 설정한 0~100 유사도 기준값입니다.
        /// </summary>
        public decimal ScoreThreshold { get; set; }

    }
}
