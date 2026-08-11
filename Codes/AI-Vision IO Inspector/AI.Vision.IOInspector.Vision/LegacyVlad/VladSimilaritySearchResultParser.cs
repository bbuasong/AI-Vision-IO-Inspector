using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Web.Script.Serialization;
using AI.Vision.IOInspector.Application.Models;

namespace AI.Vision.IOInspector.Vision.LegacyVlad
{
    /// <summary>
    /// VLAD_Search_Mat이 반환한 후보 목록 JSON을 애플리케이션 모델로 변환합니다.
    /// JSON 문자열만 읽으며 TLV나 비관리 메모리 포인터를 해석하지 않습니다.
    /// </summary>
    public class VladSimilaritySearchResultParser
    {
        private readonly JavaScriptSerializer _serializer;

        public VladSimilaritySearchResultParser()
        {
            _serializer = new JavaScriptSerializer();
        }

        /// <summary>
        /// 후보 목록 JSON을 방향별 후보 목록으로 변환합니다.
        /// 후보가 없는 정상 응답은 빈 목록으로 성공 처리합니다.
        /// </summary>
        public bool TryParse(
            string resultJson,
            string fallbackViewName,
            out IList<ReferenceImageSimilarityCandidate> candidates,
            out string errorMessage)
        {
            candidates = new List<ReferenceImageSimilarityCandidate>();
            errorMessage = string.Empty;

            if (string.IsNullOrWhiteSpace(resultJson))
            {
                errorMessage = "VLAD_Search_Mat가 결과 JSON을 반환하지 않았습니다.";
                return false;
            }

            VladSearchResultPayload payload;
            try
            {
                payload = _serializer.Deserialize<VladSearchResultPayload>(resultJson);
            }
            catch (Exception ex)
            {
                errorMessage = "VLAD_Search_Mat 결과 JSON 형식이 올바르지 않습니다. " + ex.Message;
                return false;
            }

            if (payload == null)
            {
                errorMessage = "VLAD_Search_Mat 결과 JSON이 비어 있습니다.";
                return false;
            }

            if (payload.hasAlternatives.HasValue && !payload.hasAlternatives.Value)
            {
                return true;
            }

            string viewName = ResolveViewName(payload.viewName, fallbackViewName);
            if (payload.candidates == null)
            {
                return true;
            }

            int fallbackRank = 1;
            foreach (VladSearchCandidatePayload source in payload.candidates)
            {
                if (source == null)
                {
                    continue;
                }

                string partNo = source.partNo ?? string.Empty;
                if (string.IsNullOrWhiteSpace(partNo))
                {
                    errorMessage = "VLAD_Search_Mat 후보에 partNo 값이 없습니다.";
                    return false;
                }

                if (Encoding.UTF8.GetByteCount(partNo) > 63)
                {
                    errorMessage = "VLAD_Search_Mat 후보의 partNo가 UTF-8 63 byte 제한을 초과했습니다.";
                    return false;
                }

                ReferenceImageSimilarityCandidate candidate = new ReferenceImageSimilarityCandidate();
                candidate.Rank = source.rank > 0 ? source.rank : fallbackRank;
                candidate.ViewName = viewName;
                candidate.PartNo = partNo;
                candidate.PartName = string.Empty;
                candidate.Score = NormalizeScore(source.score);
                candidates.Add(candidate);
                fallbackRank++;

                // API 계약상 AI가 점수 내림차순으로 최대 3개만 반환합니다.
                // 잘못된 추가 응답이 UI까지 전파되지 않도록 수신부에서도 3개로 제한합니다.
                if (candidates.Count >= 3)
                {
                    break;
                }
            }

            return true;
        }

        private decimal NormalizeScore(decimal score)
        {
            if (score < 0m)
            {
                return 0m;
            }

            if (score > 100m)
            {
                return 100m;
            }

            return decimal.Round(score, 2, MidpointRounding.AwayFromZero);
        }

        private string FirstNonEmpty(string first, string second)
        {
            return string.IsNullOrWhiteSpace(first) ? (second ?? string.Empty) : first;
        }

        private string ResolveViewName(object rawViewName, string fallbackViewName)
        {
            int viewCode;
            if (rawViewName != null &&
                int.TryParse(Convert.ToString(rawViewName, CultureInfo.InvariantCulture), out viewCode))
            {
                switch (viewCode)
                {
                    case 1:
                        return "Top";
                    case 2:
                        return "Front";
                    case 3:
                        return "Back";
                    case 4:
                        return "Left";
                    case 5:
                        return "Right";
                    case 6:
                        return "Thickness";
                }
            }

            return FirstNonEmpty(Convert.ToString(rawViewName, CultureInfo.InvariantCulture), fallbackViewName);
        }

        public class VladSearchResultPayload
        {
            public object viewName { get; set; }
            public decimal scoreThreshold { get; set; }
            public int topK { get; set; }
            public bool? hasAlternatives { get; set; }
            public List<VladSearchCandidatePayload> candidates { get; set; }
        }

        public class VladSearchCandidatePayload
        {
            public int rank { get; set; }
            public string partNo { get; set; }
            public decimal score { get; set; }
        }
    }
}
