using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using AI.Vision.IOInspector.Application.Interfaces;

namespace AI.Vision.IOInspector.Infrastructure.Services
{
    /// <summary>
    /// ILabelScanService 의 Epson Scan API(HTTP) 구현체입니다.
    ///
    /// 동작:
    ///   POST {BaseUrl}/scan-to-pdf  ─→  스캐너 스캔 → (라벨 크롭) → OCR → 검색가능 PDF
    ///   응답 JSON 의 ocr.part_no 를 부품번호로, ocr.quality 를 신뢰도로 정리해서 돌려줍니다.
    ///
    /// 비트니스 참고: Epson 서버는 x86(32bit)로 동작하지만, 이 클라이언트는
    /// HTTP 로만 호출하므로 본 앱(net472/AnyCPU/x64 무관)에서 그대로 호출 가능합니다.
    /// </summary>
    public class EpsonScanLabelService : ILabelScanService
    {
        private readonly EpsonScanOptions _options;
        private readonly HttpClient _httpClient;

        // HttpClient 는 재사용이 원칙이므로 외부에서 주입하는 형태를 권장합니다.
        // 간단히 쓰려면 아래 두 번째 생성자(자체 생성)를 사용하세요.
        public EpsonScanLabelService(EpsonScanOptions options, HttpClient httpClient)
        {
            _options = options ?? new EpsonScanOptions();
            _httpClient = httpClient ?? new HttpClient();
            _httpClient.Timeout = TimeSpan.FromMilliseconds(_options.TimeoutMilliseconds);
        }

        public EpsonScanLabelService(EpsonScanOptions options)
            : this(options, new HttpClient())
        {
        }

        public async Task<LabelScanResult> ScanPartNoAsync(CancellationToken cancellationToken)
        {
            LabelScanResult result = new LabelScanResult();

            // OcrOnly=true 면 ?ocrOnly=true 로 요청 → 서버가 PDF 생성 건너뛰고 OCR+part_no만(메모리 절약).
            string url = _options.BaseUrl.TrimEnd('/') + "/scan-to-pdf"
                       + (_options.OcrOnly ? "?ocrOnly=true" : "");
            string requestJson = BuildRequestJson();

            try
            {
                using (HttpContent content = new StringContent(requestJson, Encoding.UTF8, "application/json"))
                using (HttpResponseMessage response = await _httpClient
                    .PostAsync(url, content, cancellationToken)
                    .ConfigureAwait(false))
                {
                    string body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

                    if (!response.IsSuccessStatusCode)
                    {
                        // Epson API 는 스캐너 오류(용지없음/오프라인 등)를 409/500 + {"detail": "..."} 로 돌려줍니다.
                        result.IsSuccess = false;
                        result.NeedsConfirmation = true;
                        result.ErrorMessage = ExtractDetail(body) ??
                            ("스캔 요청 실패 (HTTP " + (int)response.StatusCode + ").");
                        return result;
                    }

                    ParseSuccessBody(body, result);
                }
            }
            catch (OperationCanceledException)
            {
                throw; // 취소는 그대로 전파 (UI 에서 처리)
            }
            catch (Exception ex)
            {
                // 서버 미기동/네트워크 오류 등.
                result.IsSuccess = false;
                result.NeedsConfirmation = true;
                result.ErrorMessage =
                    "Epson Scan API 에 연결할 수 없습니다. 스캔 서버가 실행 중인지 확인하세요. (" + ex.Message + ")";
            }

            return result;
        }

        /// <summary>ScanToPdfRequest 스키마에 맞춘 요청 본문을 만듭니다.</summary>
        private string BuildRequestJson()
        {
            // 주의: System.Text.Json 은 '컴파일타임 타입' 기준으로 직렬화합니다.
            //       중간 변수를 object 로 두면 {} 로 비어버리므로, 익명 타입을 var 로 유지합니다.
            //       키 이름은 API 의 JsonPropertyName(device_id, use_processed 등)과 정확히 일치해야 합니다.
            if (_options.UseCardExtraction)
            {
                var payload = new
                {
                    scan = new
                    {
                        device_id = _options.DeviceId,
                        dpi = _options.Dpi,
                        mode = _options.Mode,
                        source = _options.Source,
                        fmt = _options.Fmt
                    },
                    card = new { dpi = _options.Dpi, debug = false },
                    pdf = new { lang = _options.Lang, use_processed = true, engine = _options.Engine }
                };
                return JsonSerializer.Serialize(payload);
            }
            else
            {
                // card = null 을 '명시적으로' 보내야 서버가 카드 추출을 끈다.
                //   (안 보내면 서버가 기본값(켜짐)으로 채워 32비트 메모리 부족이 날 수 있음)
                var payload = new
                {
                    scan = new
                    {
                        device_id = _options.DeviceId,
                        dpi = _options.Dpi,
                        mode = _options.Mode,
                        source = _options.Source,
                        fmt = _options.Fmt
                    },
                    card = (object)null,
                    pdf = new { lang = _options.Lang, use_processed = true, engine = _options.Engine }
                };
                return JsonSerializer.Serialize(payload);
            }
        }

        /// <summary>성공(2xx) 응답(JobModel JSON)에서 부품번호/품질/경로를 뽑아 채웁니다.</summary>
        private void ParseSuccessBody(string body, LabelScanResult result)
        {
            using (JsonDocument doc = JsonDocument.Parse(body))
            {
                JsonElement root = doc.RootElement;

                result.Status = GetString(root, "status");
                result.PdfPath = GetString(root, "pdf_path");
                result.ImagePath = GetString(root, "image_path");

                JsonElement ocr;
                if (root.TryGetProperty("ocr", out ocr) && ocr.ValueKind == JsonValueKind.Object)
                {
                    // 부품번호: 최상위 ocr.part_no 우선, 없으면 ocr.fields.part_no.
                    result.PartNo = GetString(ocr, "part_no");
                    result.PartNoSub = GetString(ocr, "part_no_sub");

                    if (string.IsNullOrEmpty(result.PartNo))
                    {
                        JsonElement fields;
                        if (ocr.TryGetProperty("fields", out fields) && fields.ValueKind == JsonValueKind.Object)
                        {
                            result.PartNo = GetString(fields, "part_no");
                            if (string.IsNullOrEmpty(result.PartNoSub))
                            {
                                result.PartNoSub = GetString(fields, "part_no_sub");
                            }
                        }
                    }

                    result.RawText = GetString(ocr, "text");

                    JsonElement quality;
                    if (ocr.TryGetProperty("quality", out quality) && quality.ValueKind == JsonValueKind.Object)
                    {
                        result.Confidence = GetDouble(quality, "confidence");
                        result.QualityOk = GetBool(quality, "ok");
                        result.QualityReason = GetString(quality, "reason");
                    }
                    else
                    {
                        result.QualityOk = true; // 품질 정보가 없으면 보수적으로 신뢰도 판단은 part_no 유무로만.
                    }
                }

                // 검사 자체 성공 여부: status 가 error 가 아니면 스캔/OCR 은 수행된 것.
                result.IsSuccess = !string.Equals(result.Status, "error", StringComparison.OrdinalIgnoreCase);

                // 작업자 확인 필요 판단:
                //   - 부품번호가 비었거나
                //   - 품질 OK 아니거나
                //   - 신뢰도가 임계값 미만
                bool lowConfidence = result.Confidence < _options.MinConfidence;
                result.NeedsConfirmation =
                    !result.IsSuccess
                    || string.IsNullOrWhiteSpace(result.PartNo)
                    || !result.QualityOk
                    || lowConfidence;

                if (!result.IsSuccess && string.IsNullOrEmpty(result.ErrorMessage))
                {
                    result.ErrorMessage = GetString(root, "error");
                }
            }
        }

        private static string ExtractDetail(string body)
        {
            if (string.IsNullOrWhiteSpace(body))
            {
                return null;
            }

            try
            {
                using (JsonDocument doc = JsonDocument.Parse(body))
                {
                    JsonElement detail;
                    if (doc.RootElement.TryGetProperty("detail", out detail))
                    {
                        return detail.GetString();
                    }
                }
            }
            catch
            {
                // JSON 이 아니면 본문 일부를 그대로 노출.
            }

            return body.Length > 300 ? body.Substring(0, 300) : body;
        }

        // ── System.Text.Json 안전 추출 헬퍼들 ──────────────────────────────
        private static string GetString(JsonElement obj, string name)
        {
            JsonElement v;
            if (obj.TryGetProperty(name, out v) && v.ValueKind == JsonValueKind.String)
            {
                return v.GetString() ?? string.Empty;
            }
            return string.Empty;
        }

        private static double GetDouble(JsonElement obj, string name)
        {
            JsonElement v;
            if (obj.TryGetProperty(name, out v) && v.ValueKind == JsonValueKind.Number)
            {
                double d;
                if (v.TryGetDouble(out d))
                {
                    return d;
                }
            }
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
