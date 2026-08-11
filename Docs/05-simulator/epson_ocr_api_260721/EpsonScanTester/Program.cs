// ─────────────────────────────────────────────────────────────────────────────
//  Epson Scan 테스트 프로그램  (.NET Framework 4.7.2)
//
//  목적: IO Inspector 를 건드리지 않고, "스캔하면 부품번호가 나오는지"만 빠르게 확인.
//
//  쓰는 법 (cmd 또는 PowerShell):
//    1) 먼저 Epson 서버를 켠다:   cd epson_scan_api_cs  →  dotnet run
//    2) 스캐너에 라벨을 올린다.
//    3) 이 폴더에서:              cd EpsonScanTester    →  dotnet run
//       (서버 주소를 바꾸려면:    dotnet run -- http://127.0.0.1:8000 )
//
//  ※ net472 옛 문법에 맞춰 작성(클래스 + Main). IO Inspector 와 같은 .NET 버전입니다.
// ─────────────────────────────────────────────────────────────────────────────

using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace EpsonScanTester
{
    internal static class Program
    {
        private static async Task Main(string[] args)
        {
            // 서버 주소 (인자로 주면 그 값, 아니면 기본값)
            string baseUrl = args.Length > 0 ? args[0].TrimEnd('/') : "http://127.0.0.1:8000";

            Console.OutputEncoding = Encoding.UTF8;
            Console.WriteLine("==================================================");
            Console.WriteLine(" Epson Scan 테스트  (.NET 4.7.2)");
            Console.WriteLine(" 서버: " + baseUrl);
            Console.WriteLine("==================================================");

            using (HttpClient http = new HttpClient())
            {
                http.Timeout = TimeSpan.FromSeconds(120);

                // ── 1단계: 서버가 켜져 있는지 확인 ──────────────────────────────
                Console.WriteLine("");
                Console.WriteLine("[1/2] 서버 연결 확인 중... (GET /health)");
                try
                {
                    using (HttpResponseMessage health = await http.GetAsync(baseUrl + "/health"))
                    {
                        if (!health.IsSuccessStatusCode)
                        {
                            Console.WriteLine("  [실패] 서버는 응답했지만 상태가 이상합니다. (HTTP " + (int)health.StatusCode + ")");
                            return;
                        }
                    }
                    Console.WriteLine("  [OK] 서버 연결 정상");
                }
                catch
                {
                    Console.WriteLine("  [실패] 서버에 연결할 수 없습니다.");
                    Console.WriteLine("        → epson_scan_api_cs 폴더에서 'dotnet run' 으로 서버부터 켜주세요.");
                    return;
                }

                // ── 2단계: 스캔 + OCR 요청 ──────────────────────────────────────
                Console.WriteLine("");
                Console.WriteLine("[2/2] 스캔 + OCR 요청 중... (POST /scan-to-pdf?ocrOnly=true, PDF 생략)");
                Console.WriteLine("      (스캐너에 라벨이 올라가 있어야 합니다. 수 초 걸릴 수 있어요.)");

                // 서버에 보낼 요청 내용. 필요하면 값만 바꾸면 됩니다.
                // card(라벨 자동 크롭)는 32비트 메모리 한계로 큰 이미지에서 OutOfMemory가 날 수 있어 끔.
                // 주의: 서버는 card 를 '안 보내면' 기본값(켜짐)으로 채우므로, 반드시 명시적으로 null 을 보내야 꺼진다.
                // 부품번호 추출에는 카드 추출이 없어도 됩니다(엔진이 자체 전처리/기울기보정 함).
                var requestBody = new
                {
                    scan = new { device_id = (string)null, dpi = 300, mode = "gray", source = "flatbed", fmt = "png" },
                    card = (object)null,   // ← 명시적 null = 카드 추출 건너뜀
                    pdf = new { lang = "kor+eng", use_processed = true, engine = "auto" }
                };
                string json = JsonSerializer.Serialize(requestBody);

                try
                {
                    using (HttpContent content = new StringContent(json, Encoding.UTF8, "application/json"))
                    // ocrOnly=true : 서버가 PDF(iText) 생성을 건너뛰고 OCR+part_no만 함.
                    //   32비트 메모리 부족(OCR/PDF 단계) 회피 + DPI(정확도) 그대로 유지.
                    using (HttpResponseMessage response = await http.PostAsync(baseUrl + "/scan-to-pdf?ocrOnly=true", content))
                    {
                        string body = await response.Content.ReadAsStringAsync();

                        if (!response.IsSuccessStatusCode)
                        {
                            // 스캐너 오류(용지 없음/오프라인 등)는 보통 여기로 옵니다.
                            Console.WriteLine("");
                            Console.WriteLine("  [실패] 스캔 실패 (HTTP " + (int)response.StatusCode + ")");
                            Console.WriteLine("        사유: " + ReadDetail(body));
                            return;
                        }

                        PrintResult(body);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine("");
                    Console.WriteLine("  [실패] 처리 중 오류: " + ex.Message);
                }
            }
        }

        // 성공 응답(JSON)에서 부품번호/상태/신뢰도를 꺼내 출력
        private static void PrintResult(string body)
        {
            using (JsonDocument doc = JsonDocument.Parse(body))
            {
                JsonElement root = doc.RootElement;

                string status = GetString(root, "status");      // done | low_quality | error
                string partNo = string.Empty;
                string partNoSub = string.Empty;
                double confidence = 0;
                string reason = string.Empty;
                string fullText = string.Empty;

                JsonElement ocr;
                if (root.TryGetProperty("ocr", out ocr) && ocr.ValueKind == JsonValueKind.Object)
                {
                    partNo = GetString(ocr, "part_no");
                    partNoSub = GetString(ocr, "part_no_sub");
                    fullText = GetString(ocr, "text");
                    JsonElement q;
                    if (ocr.TryGetProperty("quality", out q) && q.ValueKind == JsonValueKind.Object)
                    {
                        confidence = GetDouble(q, "confidence");
                        reason = GetString(q, "reason");
                    }
                }

                Console.WriteLine("");
                Console.WriteLine("--------------------------------------------------");
                Console.WriteLine(" 결과");
                Console.WriteLine("--------------------------------------------------");
                Console.WriteLine("  상태(status)     : " + status);
                Console.WriteLine("  부품번호(part_no) : " + (string.IsNullOrEmpty(partNo) ? "(인식 실패)" : partNo));
                if (!string.IsNullOrEmpty(partNoSub))
                {
                    Console.WriteLine("  보조번호(sub)     : " + partNoSub);
                }
                Console.WriteLine("  신뢰도           : " + confidence.ToString("0.00"));
                if (!string.IsNullOrEmpty(reason))
                {
                    Console.WriteLine("  품질 사유        : " + reason);
                }
                Console.WriteLine("--------------------------------------------------");

                // 엔진이 라벨을 실제로 뭐라고 읽었는지(전체 텍스트). 오인식 진단용.
                Console.WriteLine("");
                Console.WriteLine(" [OCR 전체 텍스트] (엔진이 읽은 그대로)");
                Console.WriteLine("--------------------------------------------------");
                Console.WriteLine(string.IsNullOrEmpty(fullText) ? "  (읽은 텍스트 없음)" : fullText);
                Console.WriteLine("--------------------------------------------------");
                Console.WriteLine("");

                if (!string.IsNullOrEmpty(partNo))
                {
                    Console.WriteLine("  [OK] 부품번호를 받았습니다. 이 값을 검사 로직의 InputCode 로 넘기면 됩니다.");
                    Console.WriteLine("       위 [OCR 전체 텍스트]에서 실제 라벨과 글자가 맞는지 확인하세요.");
                    Console.WriteLine("       (1/I/l/i 처럼 닮은 글자는 OCR이 자주 헷갈립니다.)");
                }
                else
                {
                    Console.WriteLine("  [실패] 부품번호를 못 뽑았습니다. 라벨 위치/방향/초점을 확인하고 다시 시도하세요.");
                }
            }
        }

        // ── 작은 도우미 함수들 ──────────────────────────────────────────────
        private static string GetString(JsonElement obj, string name)
        {
            JsonElement v;
            if (obj.TryGetProperty(name, out v) && v.ValueKind == JsonValueKind.String)
            {
                return v.GetString();
            }
            return string.Empty;
        }

        private static double GetDouble(JsonElement obj, string name)
        {
            JsonElement v;
            double d;
            if (obj.TryGetProperty(name, out v) && v.ValueKind == JsonValueKind.Number && v.TryGetDouble(out d))
            {
                return d;
            }
            return 0.0;
        }

        private static string ReadDetail(string body)
        {
            try
            {
                using (JsonDocument doc = JsonDocument.Parse(body))
                {
                    JsonElement d;
                    if (doc.RootElement.TryGetProperty("detail", out d))
                    {
                        return d.GetString();
                    }
                }
            }
            catch
            {
            }
            return string.IsNullOrWhiteSpace(body) ? "(내용 없음)" : body;
        }
    }
}
