using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;

namespace AI.Vision.IOInspector.Infrastructure.Services
{
    /// <summary>
    /// 가장 단순한 "함수 콜 → 부품번호" 헬퍼입니다.
    /// (DI / 인터페이스 없이) Epson Scan API 서버를 띄워둔 상태에서
    /// EpsonScanClient.ScanPartNo() 한 번 부르면 스캔→OCR→부품번호까지 끝냅니다.
    ///
    /// 비트니스: Epson 서버는 x86(32bit)지만 이 클라이언트는 HTTP 호출만 하므로
    ///           64bit IO Inspector 든 32bit Worker 든 그대로 호출됩니다.
    ///           (VisionWorker 를 별도 프로세스로 두는 구조와 동일한 발상)
    /// </summary>
    public static class EpsonScanClient
    {
        // 스캔+OCR 은 수 초 걸리므로 타임아웃을 넉넉히. HttpClient 는 정적 재사용.
        private static readonly HttpClient _http = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(120)
        };

        /// <summary>스캔 결과 상태.</summary>
        public enum ScanOutcome
        {
            /// <summary>정상 + 신뢰도 충분.</summary>
            Ok = 0,

            /// <summary>스캔/OCR 은 됐지만 품질이 낮음(part_no 가 있을 수도/없을 수도).</summary>
            LowQuality = 1,

            /// <summary>스캔은 됐는데 부품번호를 못 뽑음.</summary>
            NotFound = 2,

            /// <summary>스캐너/네트워크/서버 오류 (용지 없음, 오프라인, 서버 미기동 등).</summary>
            ScanError = 3
        }

        /// <summary>스캔 결과. PartNo 와 함께 상태/신뢰도/사유를 같이 돌려줍니다.</summary>
        public struct ScanPartNoResult
        {
            public ScanOutcome Outcome;
            public string PartNo;       // 실패/미검출이면 "" (절대 "0" 같은 sentinel 안 씀)
            public double Confidence;   // 0.0 ~ 1.0
            public string Message;      // 저품질 사유 / 오류 메시지
            public string PdfPath;      // 생성된 검색가능 PDF (이력용)
        }

        /// <summary>
        /// [권장] 스캔하고 부품번호 + 상태를 함께 반환합니다.
        /// </summary>
        /// <param name="baseUrl">Epson Scan API 주소. 기본 http://127.0.0.1:8000</param>
        /// <param name="minConfidence">이 값 미만이면 LowQuality 로 처리(0~1).</param>
        public static ScanPartNoResult ScanPartNo(string baseUrl = "http://127.0.0.1:8000",
                                                  double minConfidence = 0.80)
        {
            ScanPartNoResult result = new ScanPartNoResult
            {
                Outcome = ScanOutcome.ScanError,
                PartNo = string.Empty,
                Message = string.Empty,
                PdfPath = string.Empty
            };

            string url = baseUrl.TrimEnd('/') + "/scan-to-pdf";

            // ScanToPdfRequest 스키마. 익명타입은 var 로 유지해야 직렬화가 비지 않습니다.
            var payload = new
            {
                scan = new { device_id = (string)null, dpi = 300, mode = "gray", source = "flatbed", fmt = "png" },
                card = new { dpi = 300, debug = false },
                pdf = new { lang = "kor+eng", use_processed = true, engine = "auto" }
            };
            string requestJson = JsonSerializer.Serialize(payload);

            try
            {
                using (HttpContent content = new StringContent(requestJson, Encoding.UTF8, "application/json"))
                using (HttpResponseMessage response = _http.PostAsync(url, content).GetAwaiter().GetResult())
                {
                    string body = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();

                    if (!response.IsSuccessStatusCode)
                    {
                        // 스캐너 오류: 409/500 + {"detail":"..."}
                        result.Outcome = ScanOutcome.ScanError;
                        result.Message = ExtractDetail(body) ?? ("HTTP " + (int)response.StatusCode);
                        return result;
                    }

                    ParseBody(body, minConfidence, ref result);
                }
            }
            catch (Exception ex)
            {
                result.Outcome = ScanOutcome.ScanError;
                result.Message = "Epson Scan API 연결 실패(서버 실행 여부 확인): " + ex.Message;
            }

            return result;
        }

        /// <summary>
        /// [초단순] 부품번호 문자열만 필요할 때. 실패/저품질/미검출이면 "" 를 돌려줍니다.
        /// 호출부는 string.IsNullOrEmpty() 로만 분기하면 됩니다. ("0" 같은 sentinel 대신 빈 문자열)
        /// </summary>
        public static string ScanPartNoOrEmpty(string baseUrl = "http://127.0.0.1:8000",
                                               double minConfidence = 0.80)
        {
            ScanPartNoResult r = ScanPartNo(baseUrl, minConfidence);
            return r.Outcome == ScanOutcome.Ok ? r.PartNo : string.Empty;
        }

        // ── 응답 파싱 ────────────────────────────────────────────────────────
        private static void ParseBody(string body, double minConfidence, ref ScanPartNoResult result)
        {
            using (JsonDocument doc = JsonDocument.Parse(body))
            {
                JsonElement root = doc.RootElement;
                string status = GetString(root, "status");
                result.PdfPath = GetString(root, "pdf_path");

                string partNo = string.Empty;
                double confidence = 0.0;
                bool qualityOk = true;
                string reason = string.Empty;

                JsonElement ocr;
                if (root.TryGetProperty("ocr", out ocr) && ocr.ValueKind == JsonValueKind.Object)
                {
                    partNo = GetString(ocr, "part_no");
                    if (string.IsNullOrEmpty(partNo))
                    {
                        JsonElement fields;
                        if (ocr.TryGetProperty("fields", out fields) && fields.ValueKind == JsonValueKind.Object)
                        {
                            partNo = GetString(fields, "part_no");
                        }
                    }

                    JsonElement quality;
                    if (ocr.TryGetProperty("quality", out quality) && quality.ValueKind == JsonValueKind.Object)
                    {
                        confidence = GetDouble(quality, "confidence");
                        qualityOk = GetBool(quality, "ok");
                        reason = GetString(quality, "reason");
                    }
                }

                result.PartNo = partNo ?? string.Empty;
                result.Confidence = confidence;
                result.Message = reason;

                // 상태 결정
                if (string.Equals(status, "error", StringComparison.OrdinalIgnoreCase))
                {
                    result.Outcome = ScanOutcome.ScanError;
                    if (string.IsNullOrEmpty(result.Message)) result.Message = GetString(root, "error");
                }
                else if (string.IsNullOrEmpty(result.PartNo))
                {
                    result.Outcome = ScanOutcome.NotFound;
                }
                else if (!qualityOk || confidence < minConfidence)
                {
                    // 부품번호는 있지만 신뢰도가 낮음 → 값은 주되 LowQuality 로 표시.
                    result.Outcome = ScanOutcome.LowQuality;
                }
                else
                {
                    result.Outcome = ScanOutcome.Ok;
                }
            }
        }

        private static string ExtractDetail(string body)
        {
            if (string.IsNullOrWhiteSpace(body)) return null;
            try
            {
                using (JsonDocument doc = JsonDocument.Parse(body))
                {
                    JsonElement detail;
                    if (doc.RootElement.TryGetProperty("detail", out detail))
                        return detail.GetString();
                }
            }
            catch { }
            return body.Length > 300 ? body.Substring(0, 300) : body;
        }

        private static string GetString(JsonElement obj, string name)
        {
            JsonElement v;
            if (obj.TryGetProperty(name, out v) && v.ValueKind == JsonValueKind.String)
                return v.GetString() ?? string.Empty;
            return string.Empty;
        }

        private static double GetDouble(JsonElement obj, string name)
        {
            JsonElement v;
            double d;
            if (obj.TryGetProperty(name, out v) && v.ValueKind == JsonValueKind.Number && v.TryGetDouble(out d))
                return d;
            return 0.0;
        }

        private static bool GetBool(JsonElement obj, string name)
        {
            JsonElement v;
            if (obj.TryGetProperty(name, out v))
            {
                if (v.ValueKind == JsonValueKind.True) return true;
                if (v.ValueKind == JsonValueKind.False) return false;
            }
            return false;
        }
    }
}
