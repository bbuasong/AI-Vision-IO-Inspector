namespace AI.Vision.IOInspector.Infrastructure.Services
{
    /// <summary>
    /// Epson Scan API 연동 설정입니다. appsettings / 설정화면 등에서 주입하기 좋게 분리했습니다.
    /// 기본값은 Epson Scan API(C#) 기본 구동값(127.0.0.1:8000)에 맞춰져 있습니다.
    /// </summary>
    public class EpsonScanOptions
    {
        /// <summary>Epson Scan API 베이스 주소. 서버 콘솔에 표시되는 host/port 와 동일해야 합니다.</summary>
        public string BaseUrl { get; set; } = "http://127.0.0.1:8000";

        /// <summary>특정 스캐너 지정 device_id. null 이면 첫 번째 스캐너 자동 선택.</summary>
        public string DeviceId { get; set; } = null;

        /// <summary>스캔 해상도(DPI).</summary>
        public int Dpi { get; set; } = 300;

        /// <summary>스캔 모드: color | gray | bw. 라벨 텍스트 OCR 은 gray 권장.</summary>
        public string Mode { get; set; } = "gray";

        /// <summary>급지 방식: flatbed | feeder(adf). 장치가 ADF 전용이면 자동 강제됩니다.</summary>
        public string Source { get; set; } = "flatbed";

        /// <summary>스캔 출력 포맷: bmp | png | jpeg.</summary>
        public string Fmt { get; set; } = "png";

        /// <summary>OCR 언어. 한국어+영문 라벨 기준 kor+eng.</summary>
        public string Lang { get; set; } = "kor+eng";

        /// <summary>OCR 엔진: auto | epson | tesseract. auto 권장(Epson 엔진 우선, 불가 시 폴백).</summary>
        public string Engine { get; set; } = "auto";

        /// <summary>
        /// PDF(검색가능 PDF) 생성을 건너뛰고 OCR + part_no 만 받을지.
        /// true 권장: part_no 만 필요하면 서버의 PDF(iText) 단계를 건너뛰어 32비트 메모리 부족을 피하고 DPI(정확도)는 유지.
        /// (요청 URL 에 ?ocrOnly=true 로 전달됨)
        /// </summary>
        public bool OcrOnly { get; set; } = true;

        /// <summary>
        /// 라벨 영역 자동 크롭(deskew + 카드 추출) 사용 여부.
        /// 기본 false 권장: 카드 추출은 큰 이미지에서 32비트 메모리 부족을 일으킬 수 있음.
        /// 엔진이 자체 기울기보정을 하므로 part_no 추출엔 없어도 됨.
        /// </summary>
        public bool UseCardExtraction { get; set; } = false;

        /// <summary>이 신뢰도 미만이면 작업자 확인이 필요한 것으로 표시(0.0~1.0).</summary>
        public double MinConfidence { get; set; } = 0.80;

        /// <summary>스캔+OCR 은 수 초 이상 걸릴 수 있으므로 넉넉히. 밀리초.</summary>
        public int TimeoutMilliseconds { get; set; } = 120000;
    }
}
