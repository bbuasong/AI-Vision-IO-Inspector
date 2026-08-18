using System;

namespace AI.Vision.IOInspector.Infrastructure.Services.Camera
{
    /// <summary>
    /// 로그에 남길 RTSP 주소에서 계정 정보를 가립니다.
    ///
    /// RTSP 주소는 rtsp://아이디:비밀번호@호스트/경로 형태라, 그대로 로그에 적으면
    /// NVR 계정과 비밀번호가 평문으로 파일에 남습니다. 로그 파일은 진단을 위해
    /// 외부로 전달되는 일이 잦으므로, 기록하는 시점에 가려야 합니다.
    /// </summary>
    public static class RtspUrlMasker
    {
        private const string MaskedCredential = "***:***";

        /// <summary>
        /// 주소 하나에서 계정 부분만 가립니다. 호스트와 경로는 진단에 필요하므로 남깁니다.
        /// 예) rtsp://user:pass@192.168.1.230:554/trackID=1
        ///  -> rtsp://***:***@192.168.1.230:554/trackID=1
        /// </summary>
        public static string Mask(string sUrl)
        {
            if (string.IsNullOrWhiteSpace(sUrl))
            {
                return sUrl;
            }

            int nSchemeEnd = sUrl.IndexOf("://", StringComparison.Ordinal);
            if (nSchemeEnd < 0)
            {
                return sUrl;
            }

            int nAuthorityStart = nSchemeEnd + 3;

            // 계정 구분자 @ 는 authority 안에만 의미가 있습니다.
            // 경로에 들어 있는 @ 를 계정으로 오해하지 않도록 첫 / 앞까지만 봅니다.
            int nPathStart = sUrl.IndexOf('/', nAuthorityStart);
            int nSearchEnd = nPathStart < 0 ? sUrl.Length : nPathStart;

            int nAtIndex = sUrl.LastIndexOf('@', nSearchEnd - 1);
            if (nAtIndex < nAuthorityStart)
            {
                return sUrl;
            }

            return sUrl.Substring(0, nAuthorityStart) + MaskedCredential + sUrl.Substring(nAtIndex);
        }

        /// <summary>
        /// 문장 안에 섞여 있는 RTSP 주소들을 모두 가립니다.
        /// ffmpeg 실행 인자처럼 주소가 다른 글자와 함께 있는 경우에 씁니다.
        /// </summary>
        public static string MaskAllInText(string sText)
        {
            if (string.IsNullOrWhiteSpace(sText))
            {
                return sText;
            }

            string sResult = sText;
            int nSearchFrom = 0;

            while (true)
            {
                int nStart = sResult.IndexOf("rtsp://", nSearchFrom, StringComparison.OrdinalIgnoreCase);
                if (nStart < 0)
                {
                    return sResult;
                }

                // 주소의 끝은 공백이나 따옴표입니다.
                int nEnd = nStart;
                while (nEnd < sResult.Length &&
                       sResult[nEnd] != ' ' &&
                       sResult[nEnd] != '"' &&
                       sResult[nEnd] != '\t')
                {
                    nEnd++;
                }

                string sUrl = sResult.Substring(nStart, nEnd - nStart);
                string sMasked = Mask(sUrl);

                sResult = sResult.Substring(0, nStart) + sMasked + sResult.Substring(nEnd);
                nSearchFrom = nStart + sMasked.Length;
            }
        }
    }
}
