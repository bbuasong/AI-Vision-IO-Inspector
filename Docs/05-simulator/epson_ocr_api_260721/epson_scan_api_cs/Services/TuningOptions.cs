using Microsoft.Extensions.Configuration;

namespace EpsonScanApi.Services;

/// <summary>
/// 튜닝 가능한 값(임계값·정규식·카드 파라미터 등)을 appsettings.json 의 "Tuning" 섹션에서
/// 시작 시 한 번 읽어 정적으로 보관한다. static 클래스(OcrEpson/CardExtractor)들이 DI 없이
/// 참조할 수 있게 하기 위함이며, 설정이 없으면 아래 기본값(기존 하드코딩값)을 그대로 쓴다.
///
/// Program.cs 시작부에서 TuningOptions.Load(app.Configuration) 를 한 번 호출한다.
/// (고정 상수 — Epson 라이선스/키/LangKrn/WIA GUID 등 — 은 여기 넣지 않는다.)
/// </summary>
public static class TuningOptions
{
    // ── OCR 품질 / 부품번호 ─────────────────────────────────────────────
    public static int OcrMinLetters { get; private set; } = 40;
    public static double OcrMinRatio { get; private set; } = 0.80;
    public static string[] OcrRequiredAnchors { get; private set; } = new[] { "EA" };
    public static string OcrPartNoPattern { get; private set; } = @"[A-Za-z0-9]+(?:[-‐-―][A-Za-z0-9]+)*";
    public static string OcrPartNoBrackets { get; private set; } = "()[]{}<>«»‹›＜＞｛｝〈〉《》";
    public static int OcrDefaultDpi { get; private set; } = 300;

    // ── 카드 추출 ───────────────────────────────────────────────────────
    public static double CardMarginFrac { get; private set; } = 0.04;
    public static int CardInkCap { get; private set; } = 150;
    public static int CardDeskewDim { get; private set; } = 1100;
    public static double CardDeskewRange { get; private set; } = 20;
    public static double CardDeskewCoarse { get; private set; } = 1.0;
    public static double CardDeskewFine { get; private set; } = 0.2;
    public static int CardBarcodeBandMinCount { get; private set; } = 8;

    public static void Load(IConfiguration cfg)
    {
        var t = cfg.GetSection("Tuning");

        var ocr = t.GetSection("Ocr");
        OcrMinLetters = ocr.GetValue("MinLetters", OcrMinLetters);
        OcrMinRatio   = ocr.GetValue("MinRatio", OcrMinRatio);
        OcrDefaultDpi = ocr.GetValue("DefaultDpi", OcrDefaultDpi);

        var anchors = ocr.GetSection("RequiredAnchors").Get<string[]>();
        if (anchors is { Length: > 0 }) OcrRequiredAnchors = anchors;

        var pat = ocr["PartNoPattern"];
        if (!string.IsNullOrWhiteSpace(pat)) OcrPartNoPattern = pat;

        var br = ocr["PartNoBrackets"];
        if (!string.IsNullOrWhiteSpace(br)) OcrPartNoBrackets = br;

        var card = t.GetSection("Card");
        CardMarginFrac          = card.GetValue("MarginFrac", CardMarginFrac);
        CardInkCap              = card.GetValue("InkCap", CardInkCap);
        CardDeskewDim           = card.GetValue("DeskewDim", CardDeskewDim);
        CardDeskewRange         = card.GetValue("DeskewRange", CardDeskewRange);
        CardDeskewCoarse        = card.GetValue("DeskewCoarse", CardDeskewCoarse);
        CardDeskewFine          = card.GetValue("DeskewFine", CardDeskewFine);
        CardBarcodeBandMinCount = card.GetValue("BarcodeBandMinCount", CardBarcodeBandMinCount);
    }
}
