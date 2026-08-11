using System.Text.RegularExpressions;

namespace EpsonScanApi.Services;

public class OcrEngine(OcrEpson epson, OcrTesseract tesseract, OcrRapid rapid)
{
    public Dictionary<string, object> EngineStatus() => new()
    {
        ["epson"] = new Dictionary<string, object?>
        {
            ["available"] = epson.IsAvailable(out string? reason),
            ["reason"]    = reason,
        },
        ["rapidocr"] = rapid.Diagnostics(),   // part_no 보조 인식기(파이썬 사이드카) — 경로/존재/연결 진단
        ["tesseract_languages"] = tesseract.AvailableLanguages(),
    };

    public Dictionary<string, object> AvailableLanguages()
    {
        bool epOk = epson.IsAvailable(out _);
        return new()
        {
            ["epson"]      = epOk ? new[] { "kor" } : Array.Empty<string>(),
            ["tesseract"]  = tesseract.AvailableLanguages(),
        };
    }

    public Dictionary<string, object> ImageToSearchablePdf(string imagePath, string outPdf,
                                                            string lang = "kor+eng", string engine = "auto",
                                                            bool buildPdf = true)
    {
        engine = (engine ?? "auto").ToLower();
        if (engine is "auto" or "epson")
        {
            try
            {
                // 방향감지 미사용(작업자가 라벨을 똑바로 넣는 전제).
                // RapidOCR을 먼저 읽어 (1) PDF 부품번호 줄 교체, (2) part_no 화해에 공용으로 쓴다.
                var (rpn, rsub, rconf) = rapid.ReadPartNo(imagePath);
                var info = epson.ImageToSearchablePdf(
                    imagePath, outPdf, lang, detectOrientation: false,
                    partNoOverride: string.IsNullOrEmpty(rpn) ? null : rpn,
                    buildPdf: buildPdf);   // buildPdf=false 면 PDF 생성 건너뜀(메모리 절약), part_no는 그대로
                ReconcilePartNo(info, rpn, rsub, rconf);
                return info;
            }
            catch (EpsonOcrError ex)
            {
                if (engine == "epson")
                    throw new OcrError($"Epson 엔진 실패: {ex.Message}");
                var info = tesseract.ImageToSearchablePdf(imagePath, outPdf, lang);
                info["engine"] = $"Tesseract (Epson 폴백: {ex.Message})";
                return info;
            }
        }
        var r = tesseract.ImageToSearchablePdf(imagePath, outPdf, lang);
        r.TryAdd("engine", "Tesseract");
        return r;
    }

    // Epson이 만든 part_no를 RapidOCR(rapid)로 보강·화해한다.
    //   일치 -> both / RapidOCR만 -> rapid / 불일치 -> rapid_disagree(+재스캔) / Epson만 -> epson / 둘다없음 -> none(+재스캔)
    private static void ReconcilePartNo(Dictionary<string, object> info, string rapid, string rapidSub, double rconf)
    {
        string ep = info.TryGetValue("part_no", out var p) ? (p as string ?? "") : "";
        info["part_no_epson"]      = ep;
        info["part_no_rapid"]      = rapid;
        info["part_no_rapid_sub"]  = rapidSub;
        info["part_no_rapid_conf"] = rconf;

        string epc = Clean(ep), rc = Clean(rapid);
        bool? rescan = null;
        bool useRapid = false;
        if (rc.Length > 0 && epc.Length > 0 && rc == epc) { info["part_no"] = ep;    info["part_no_source"] = "both";  rescan = false; useRapid = true; }
        else if (rc.Length > 0 && epc.Length == 0)        { info["part_no"] = rapid; info["part_no_source"] = "rapid"; rescan = false; useRapid = true; }
        else if (rc.Length > 0 && epc.Length > 0)          { info["part_no"] = rapid; info["part_no_source"] = "rapid_disagree"; rescan = true; useRapid = true; }
        else if (epc.Length > 0 && rc.Length == 0)         { info["part_no"] = ep;    info["part_no_source"] = "epson"; }
        else                                               { info["part_no"] = "";    info["part_no_source"] = "none";  rescan = true; }

        // 영숫자 핵심 필드는 RapidOCR로 일원화: part_no를 RapidOCR로 쓸 땐 괄호번호(sub)도 RapidOCR 값으로.
        if (useRapid && !string.IsNullOrEmpty(rapidSub))
            info["part_no_sub"] = rapidSub;

        if (rescan.HasValue)
        {
            bool cur = info.TryGetValue("needs_rescan", out var nr) && nr is true;
            info["needs_rescan"] = cur || rescan.Value;
        }
    }

    private static string Clean(string s) =>
        Regex.Replace(s ?? "", @"\s+", "").ToUpperInvariant();
}

public class OcrError(string msg) : Exception(msg);
