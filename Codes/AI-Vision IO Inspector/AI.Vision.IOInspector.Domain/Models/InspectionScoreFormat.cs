using System.Globalization;

namespace AI.Vision.IOInspector.Domain.Models
{
    /// <summary>
    /// AI Score를 화면과 이미지에 같은 값으로 적기 위한 규칙입니다.
    ///
    /// <para>
    /// 예전에는 두 곳이 서로 다르게 다뤘습니다.
    ///   결과 기록 이미지  0~1 범위면 100을 곱해서 적음
    ///   화면 결과 문구    받은 값을 그대로 적음
    /// 그래서 AI가 0.97처럼 0~1로 돌려주면 화면에는 0.97, 이미지에는 97.00이 적혔습니다.
    /// 같은 검사인데 숫자가 달라 보이므로 어느 쪽이 맞는지 알 수 없었습니다.
    /// </para>
    ///
    /// <para>
    /// 이제 값을 만드는 규칙과 적는 규칙을 여기 한 곳에 둡니다.
    /// 양쪽이 이 규칙을 함께 쓰면 값이 갈릴 수 없습니다.
    /// </para>
    /// </summary>
    public static class InspectionScoreFormat
    {
        /// <summary>화면과 이미지가 함께 쓰는 소수 자리입니다.</summary>
        public const string DisplayFormat = "0.00";

        /// <summary>
        /// 받은 Score를 100점 기준으로 맞춥니다.
        ///
        /// <para>
        /// 계약상 score와 scoreThreshold는 0~100입니다. 다만 AI가 0~1로 돌려주는 경우가 있어
        /// 그때는 100을 곱해 기준값과 같은 자로 만듭니다. 이미 100점 기준이면 그대로 둡니다.
        /// </para>
        /// </summary>
        public static decimal Normalize(decimal score)
        {
            return score >= 0m && score <= 1m ? score * 100m : score;
        }

        /// <summary>
        /// 화면과 이미지에 적을 문자열입니다. 100점 기준으로 맞춘 뒤 같은 자리수로 적습니다.
        /// </summary>
        public static string Format(decimal score)
        {
            return Normalize(score).ToString(DisplayFormat, CultureInfo.InvariantCulture);
        }
    }
}
