using System.Reflection;
using System.Runtime.InteropServices;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using iText.IO.Font.Constants;
using iText.IO.Image;
using iText.Kernel.Font;
using iText.Kernel.Pdf;
using iText.Kernel.Pdf.Canvas;
using SysPath = System.IO.Path;

namespace EpsonScanApi.Services;

public class EpsonOcrError(string msg) : Exception(msg);

public sealed class OcrEpson : IDisposable
{
    // ── 경로/상수 ────────────────────────────────────────────────────────────
    private static readonly string NuOcrDir = @"C:\Program Files (x86)\EPSON Software\Scan OCR Cmponent Pro\NuOCR";
    private static readonly string DcpDir   = @"C:\Program Files (x86)\EPSON Software\Scan OCR Cmponent Pro\DCP";
    private static readonly string FfmtDir  = @"C:\Program Files (x86)\EPSON Software\Scan OCR Cmponent Pro\DCP\ffmt";
    private static readonly string KernelDll = Path.Combine(NuOcrDir, "KernelAPI.dll");
    private const string LicenseFile = @"C:\Program Files (x86)\EPSON Software\Scan OCR Cmponent Pro\NuOCR\epson.lcxz";
    private const string LicenseCode = "247ECFD6055D";
    private const string InitUserKey = "Seiko Epson";
    private const string InitCompany = "Document Capture";
    private const uint LangKrn = 0x7A;
    private const int LetterSize = 0x38;
    private const int DefaultDpi = 300;

    // 필수 앵커 — 없으면 품질 불량
    private static readonly string[] RequiredAnchors = ["EA"];

    // ── P/Invoke ─────────────────────────────────────────────────────────────
    [DllImport("KernelAPI", CharSet = CharSet.Unicode, CallingConvention = CallingConvention.StdCall)]
    private static extern int kRecSetLicenseW(string licFile, string licCode);

    [DllImport("KernelAPI", CharSet = CharSet.Unicode, CallingConvention = CallingConvention.StdCall)]
    private static extern int kRecInitW(string userKey, string company);

    [DllImport("KernelAPI", CallingConvention = CallingConvention.StdCall)]
    private static extern int kRecQuit();

    [DllImport("KernelAPI", CallingConvention = CallingConvention.StdCall)]
    private static extern int kRecSetDefaults(int sid);

    [DllImport("KernelAPI", CallingConvention = CallingConvention.StdCall)]
    private static extern int kRecManageLanguages(int sid, int op, uint lang);

    [DllImport("KernelAPI", CharSet = CharSet.Unicode, CallingConvention = CallingConvention.StdCall)]
    private static extern int kRecLoadImgFW(int sid, string path, out IntPtr hPage, int pageNum);

    [DllImport("KernelAPI", CallingConvention = CallingConvention.StdCall)]
    private static extern int kRecPreprocessImg(int sid, IntPtr hPage);

    [DllImport("KernelAPI", CallingConvention = CallingConvention.StdCall)]
    private static extern int kRecRecognizeW(int sid, IntPtr hPage, IntPtr hZones);

    [DllImport("KernelAPI", CallingConvention = CallingConvention.StdCall)]
    private static extern int kRecGetLetters(IntPtr hPage, int zone, out IntPtr pLetters, out int count);

    [DllImport("KernelAPI", CallingConvention = CallingConvention.StdCall)]
    private static extern int kRecFreeImg(IntPtr hPage);

    // ── 상태 ─────────────────────────────────────────────────────────────────
    private static bool _inited;
    private static string? _initError;
    private static readonly object _lock = new();

    static OcrEpson()
    {
        // DllImportResolver 등록 전에 PATH 설정 — KernelAPI.dll 로드 시 의존 DLL들을 찾을 수 있게
        SetupPaths();
        NativeLibrary.SetDllImportResolver(typeof(OcrEpson).Assembly, (name, asm, path) =>
        {
            if (name != "KernelAPI") return IntPtr.Zero;
            if (NativeLibrary.TryLoad(KernelDll, out var h)) return h;
            return IntPtr.Zero;
        });
    }

    // ── Letter 레코드 ─────────────────────────────────────────────────────────
    private record struct Letter(ushort Code, ushort Left, ushort Top, ushort Width, ushort Height);
    private record struct Word(string Text, int Left, int Right, int Top, int Bottom);

    // ── 초기화 ───────────────────────────────────────────────────────────────
    private bool EnsureInit()
    {
        lock (_lock)
        {
            if (_inited) return true;
            if (_initError != null) return false;
            if (!File.Exists(KernelDll))
            {
                _initError = $"KernelAPI.dll 없음: {KernelDll}";
                Console.Error.WriteLine($"[EpsonOCR] {_initError}");
                return false;
            }
            if (!File.Exists(LicenseFile))
            {
                _initError = $"라이선스 파일 없음: {LicenseFile}";
                Console.Error.WriteLine($"[EpsonOCR] {_initError}");
                return false;
            }
            try
            {
                int rc = kRecSetLicenseW(LicenseFile, LicenseCode);
                if (rc < 0) { _initError = $"kRecSetLicenseW 실패 rc=0x{(uint)rc:X8}"; Console.Error.WriteLine($"[EpsonOCR] {_initError}"); return false; }
                rc = kRecInitW(InitUserKey, InitCompany);
                if (rc < 0) { _initError = $"kRecInitW 실패 rc=0x{(uint)rc:X8}"; Console.Error.WriteLine($"[EpsonOCR] {_initError}"); return false; }
                kRecSetDefaults(0);
                kRecManageLanguages(0, 0, LangKrn);
                _inited = true;
                Console.WriteLine("[EpsonOCR] 엔진 초기화 완료");
                return true;
            }
            catch (Exception ex)
            {
                _initError = $"초기화 예외: {ex.GetType().Name}: {ex.Message}";
                Console.Error.WriteLine($"[EpsonOCR] {_initError}");
                return false;
            }
        }
    }

    private static void SetupPaths()
    {
        var dirs = new[] { NuOcrDir, DcpDir, FfmtDir };
        var cur = Environment.GetEnvironmentVariable("PATH") ?? "";
        Environment.SetEnvironmentVariable("PATH",
            string.Join(";", dirs.Where(Directory.Exists)) + ";" + cur);
    }

    public bool IsAvailable(out string? reason)
    {
        bool ok = EnsureInit();
        reason = ok ? null : _initError;
        return ok;
    }

    // ── 인식 ─────────────────────────────────────────────────────────────────
    private List<Letter> Recognize(string imgPath, int zone = -3)
    {
        if (!EnsureInit()) throw new EpsonOcrError(_initError ?? "엔진 사용 불가");

        string norm = NormalizeImage(imgPath);
        try
        {
            IntPtr hPage;
            int rc = kRecLoadImgFW(0, norm, out hPage, 0);
            if (rc < 0 || hPage == IntPtr.Zero)
                rc = kRecLoadImgFW(0, imgPath, out hPage, 0);
            if (rc < 0 || hPage == IntPtr.Zero) throw new EpsonOcrError("이미지 로드 실패(kRecLoadImgFW)");
            try
            {
                kRecPreprocessImg(0, hPage);
                rc = kRecRecognizeW(0, hPage, IntPtr.Zero);
                if (rc < 0) throw new EpsonOcrError($"kRecRecognizeW rc=0x{(uint)rc:X}");

                rc = kRecGetLetters(hPage, zone, out IntPtr pLet, out int n);
                var letters = new List<Letter>();
                if (rc >= 0 && n > 0 && pLet != IntPtr.Zero)
                {
                    for (int i = 0; i < n; i++)
                    {
                        IntPtr p = IntPtr.Add(pLet, i * LetterSize);
                        letters.Add(new Letter(
                            Code:   (ushort)Marshal.ReadInt16(p, 0x12),
                            Left:   (ushort)Marshal.ReadInt16(p, 0x00),
                            Top:    (ushort)Marshal.ReadInt16(p, 0x02),
                            Width:  (ushort)Marshal.ReadInt16(p, 0x04),
                            Height: (ushort)Marshal.ReadInt16(p, 0x06)
                        ));
                    }
                }
                return letters;
            }
            finally { try { kRecFreeImg(hPage); } catch { } }
        }
        finally { try { if (File.Exists(norm) && norm != imgPath) File.Delete(norm); } catch { } }
    }

    // 엔진 호환: DPI 보정 BMP (이진화 금지 — 엔진이 직접 처리)
    // Python _normalize와 동일: 이미 RGB(24bit)면 컬러 유지, 그 외(RGBA/팔레트/1bit/그레이 등)는
    // 그레이(L)로 변환. PIL의 `if im.mode not in ("L","RGB"): im.convert("L")` 동작을 그대로 포팅.
    private static string NormalizeImage(string imgPath)
    {
        string tmp = SysPath.Combine(SysPath.GetDirectoryName(imgPath)!,
                                     SysPath.GetFileNameWithoutExtension(imgPath) + "._engine.bmp");

        var id = Image.Identify(imgPath);
        int bpp = id.PixelType.BitsPerPixel;

        if (bpp == 24)
        {
            // 24bit RGB(알파 없음) → PIL "RGB"에 해당, 색 정보 유지
            using var rgb = Image.Load<Rgb24>(imgPath);
            int dpi = rgb.Metadata.HorizontalResolution >= 150
                ? (int)rgb.Metadata.HorizontalResolution : DefaultDpi;
            rgb.Metadata.HorizontalResolution = dpi;
            rgb.Metadata.VerticalResolution   = dpi;
            rgb.SaveAsBmp(tmp);
        }
        else
        {
            // RGBA/팔레트/1bit/그레이 등 → PIL "L"로 변환에 해당
            using var im = Image.Load(imgPath);
            int dpi = im.Metadata.HorizontalResolution >= 150
                ? (int)im.Metadata.HorizontalResolution : DefaultDpi;
            using var gray = im.CloneAs<L8>();
            gray.Metadata.HorizontalResolution = dpi;
            gray.Metadata.VerticalResolution   = dpi;
            gray.SaveAsBmp(tmp);
        }
        return tmp;
    }

    // ── 방향 자동 감지 ───────────────────────────────────────────────────────
    private (List<Letter> Letters, string OrientedPath) RecognizeOriented(string imgPath, bool detect = false)
    {
        if (!detect) return (Recognize(imgPath), imgPath);

        using var orig = Image.Load(imgPath);
        int dpi = orig.Metadata.HorizontalResolution >= 150
            ? (int)orig.Metadata.HorizontalResolution : DefaultDpi;
        string stem = SysPath.Combine(SysPath.GetDirectoryName(imgPath)!,
                                      SysPath.GetFileNameWithoutExtension(imgPath));

        (int score, int rot, List<Letter> letters, string path) best = default;
        var tmps = new List<string>();

        foreach (int rot in new[] { 0, 90, 180, 270 })
        {
            string tp = $"{stem}._ori{rot}.png";
            tmps.Add(tp);
            using var tmp2 = orig.Clone(ctx => { if (rot != 0) ctx.Rotate(rot); });
            tmp2.SaveAsPng(tp);
            try
            {
                var letters = Recognize(tp);
                int score   = letters.Count(l => IsTextCode(l.Code));
                if (best.path == null || score > best.score)
                    best = (score, rot, letters, tp);
            }
            catch { }
        }
        foreach (var tp in tmps)
            if (tp != best.path) try { File.Delete(tp); } catch { }
        return (best.letters ?? new(), best.path ?? imgPath);
    }

    // ── 텍스트 구조 파싱 ─────────────────────────────────────────────────────
    private static (List<List<Word>> Lines, int MedH) StructureLines(List<Letter> letters)
    {
        var textH = letters
            .Where(l => l.Height > 0 && (IsDigit(l.Code) || IsAlpha(l.Code) || IsHangul(l.Code)))
            .Select(l => (int)l.Height).Order().ToList();
        int medH = textH.Count > 0 ? textH[textH.Count / 2] : 20;

        var bcBands = BarcodeBands(letters, medH);
        var lines   = new List<List<Word>>();
        var line    = new List<Word>();
        var wc      = new System.Text.StringBuilder();
        int wl = 0, wr = 0, wt = int.MaxValue, wb = 0;

        void FlushWord()
        {
            if (wc.Length > 0) { line.Add(new Word(wc.ToString(), wl, wr, wt, wb)); wc.Clear(); wt = int.MaxValue; }
        }
        void FlushLine() { FlushWord(); if (line.Count > 0) { lines.Add(new(line)); line.Clear(); } }

        foreach (var l in letters)
        {
            if (l.Code == 0x20)
            {
                if (l.Width == 0) FlushLine(); else FlushWord();
                continue;
            }
            if (l.Code == 0 || IsCjkJunk(l.Code)) continue;
            if (IsThin(l.Height, l.Width, medH) && bcBands.Contains((int)Math.Round((double)l.Top / Math.Max(medH, 1)))) continue;

            if (wc.Length == 0) { wl = l.Left; wt = l.Top; wb = l.Top + l.Height; }
            wc.Append((char)l.Code);
            wr = l.Left + l.Width;
            wt = Math.Min(wt, l.Top);
            wb = Math.Max(wb, l.Top + l.Height);
        }
        FlushLine();

        // 좌표 재정렬: (행밴드, leftmost_x)
        double rh = medH > 0 ? medH : 30.0;
        lines.Sort((a, b) =>
        {
            double aTop = Median(a.Select(w => (double)w.Top));
            double bTop = Median(b.Select(w => (double)w.Top));
            int aRow = (int)Math.Round(aTop / (rh * 1.2));
            int bRow = (int)Math.Round(bTop / (rh * 1.2));
            int c = aRow.CompareTo(bRow);
            return c != 0 ? c : a.Min(w => w.Left).CompareTo(b.Min(w => w.Left));
        });
        return (lines, medH);
    }

    private static HashSet<int> BarcodeBands(List<Letter> letters, int medH)
    {
        var cnt = new Dictionary<int, int>();
        foreach (var l in letters)
        {
            if (l.Code == 0x20 || l.Code == 0 || IsCjkJunk(l.Code)) continue;
            if (IsThin(l.Height, l.Width, medH))
            {
                int b = (int)Math.Round((double)l.Top / Math.Max(medH, 1));
                cnt[b] = cnt.GetValueOrDefault(b) + 1;
            }
        }
        return cnt.Where(kv => kv.Value >= 8).Select(kv => kv.Key).ToHashSet();
    }

    // ── 구조 → 필드 딕셔너리 ────────────────────────────────────────────────
    private static Dictionary<string, object> StructureToFields(List<Letter> letters)
    {
        var (lines, _) = StructureLines(letters);
        var fields = new Dictionary<string, object>();
        var outLines = new List<object>();
        var full = new System.Text.StringBuilder();

        for (int li = 0; li < lines.Count; li++)
        {
            string lineText = string.Join(" ", lines[li].Select(w => w.Text));
            full.AppendLine(lineText);
            outLines.Add(new
            {
                line  = li + 1,
                text  = lineText,
                words = lines[li].Select(w => new
                {
                    text = w.Text,
                    bbox = new[] { w.Left, w.Top, w.Right - w.Left, w.Bottom - w.Top }
                }).ToList(),
            });
            for (int wi = 0; wi < lines[li].Count; wi++)
                fields[$"line_{li + 1}_{wi + 1}"] = lines[li][wi].Text;
        }

        // 부품번호/괄호번호 추출 (ExtractPartNo 참고)
        var (partNo, partNoSub) = ExtractPartNo(lines);
        fields["part_no"] = partNo;
        if (partNoSub != "") fields["part_no_sub"] = partNoSub;

        return new Dictionary<string, object>
        {
            ["text"]        = full.ToString().TrimEnd(),
            ["lines"]       = outLines,
            ["fields"]      = fields,
            ["part_no"]     = partNo,
            ["part_no_sub"] = partNoSub,
        };
    }

    // ── 품질 평가 ────────────────────────────────────────────────────────────
    private static Dictionary<string, object> AssessQuality(List<Letter> letters,
                                                              int minLetters = 40, double minRatio = 0.80)
    {
        var real = letters.Where(l => l.Code != 0x20 && l.Code != 0 && l.Code != 0xFFFF).ToList();
        int n = real.Count;
        if (n == 0) return new()
        {
            ["letters"]=0, ["valid_ratio"]=0.0, ["confidence"]=0.0, ["ok"]=false,
            ["missing_anchors"]=RequiredAnchors.ToList(), ["reason"]="인식된 글자 없음",
        };
        int valid = real.Count(l => IsPlausible(l.Code));
        double ratio = (double)valid / n;
        string text = string.Concat(letters.Where(l => l.Code != 0 && l.Code != 0xFFFF)
                                           .Select(l => (char)l.Code))
                           .ToUpper().Replace(" ", "");
        var missing = RequiredAnchors.Where(a => !text.Contains(a.ToUpper())).ToList();
        bool ok = n >= minLetters && ratio >= minRatio && missing.Count == 0;
        string reason = ok ? "" : missing.Count > 0
            ? $"필수 항목 미검출({string.Join(", ", missing)}) — 라벨 방향/위치 확인"
            : n < minLetters ? $"글자 수 부족({n})"
            : $"정상글자 비율 낮음({ratio * 100:F0}%) — 방향/위치/초점 의심";
        return new()
        {
            ["letters"] = n, ["valid_ratio"] = Math.Round(ratio, 2),
            ["confidence"] = Math.Round(ratio, 2), ["missing_anchors"] = missing,
            ["ok"] = ok, ["reason"] = reason,
        };
    }

    // ── 검색가능 PDF 생성 ────────────────────────────────────────────────────
    public Dictionary<string, object> ImageToSearchablePdf(string imagePath, string outPdf,
                                                            string lang = "kor", bool detectOrientation = false)
    {
        if (!EnsureInit()) throw new EpsonOcrError(_initError ?? "엔진 사용 불가");

        var (letters, oriented) = RecognizeOriented(imagePath, detectOrientation);
        BuildPdfPage(oriented, letters, outPdf);

        var info = new Dictionary<string, object>
        {
            ["out_pdf"] = outPdf, ["engine"] = "Epson OmniPage",
            ["pages"] = 1, ["letters"] = letters.Count,
        };
        var structure = StructureToFields(letters);
        foreach (var kv in structure) info[kv.Key] = kv.Value;
        info["quality"] = AssessQuality(letters);

        if (oriented != imagePath) try { System.IO.File.Delete(oriented); } catch { }
        return info;
    }

    // ── PDF 페이지 빌드 (iText7) ─────────────────────────────────────────────
    private static void BuildPdfPage(string imgPath, List<Letter> letters, string outPdf)
    {
        using var imgInfo = Image.Load(imgPath);
        float iw = imgInfo.Width, ih = imgInfo.Height;

        using var writer = new PdfWriter(outPdf);
        using var pdf    = new PdfDocument(writer);
        var page         = pdf.AddNewPage(new iText.Kernel.Geom.PageSize(iw, ih));
        var canvas       = new PdfCanvas(page);

        // 배경 이미지
        var imgData = ImageDataFactory.Create(imgPath);
        canvas.AddImageWithTransformationMatrix(imgData, iw, 0, 0, ih, 0, 0);

        // 투명 텍스트 레이어
        var font = LoadKoreanFont();
        var (lines, _) = StructureLines(letters);

        // 폰트 메트릭
        float ascR = 0.75f, dscR = 0.25f;
        try
        {
            var metrics = font.GetFontProgram().GetFontMetrics();
            float a = metrics.GetTypoAscender()  / 1000f;
            float d = -metrics.GetTypoDescender() / 1000f;
            if (a > 0) { ascR = a; dscR = d; }
        }
        catch { }
        float totR = ascR + dscR;

        canvas.BeginText();
        canvas.SetTextRenderingMode(PdfCanvasConstants.TextRenderingMode.INVISIBLE);

        foreach (var words in lines)
        {
            if (words.Count == 0) continue;
            // 줄 공통 baseline (중앙값)
            var bls = words.Select(w => w.Top + ascR * Math.Max((w.Bottom - w.Top) / totR, 4f)).ToList();
            bls.Sort();
            float baseline = (float)bls[bls.Count / 2];
            float pdfY = ih - baseline;

            foreach (var w in words)
            {
                float sz   = Math.Max((w.Bottom - w.Top) / totR, 4f);
                float pixW = Math.Max(w.Right - w.Left, 1);
                float glW  = font.GetWidth(w.Text, sz);
                float scale = glW > 0 ? 100f * pixW / glW : 100f;

                canvas.SetFontAndSize(font, sz);
                canvas.SetHorizontalScaling(scale);
                canvas.SetTextMatrix(1f, 0f, 0f, 1f, w.Left, pdfY);
                canvas.ShowText(w.Text);
            }
        }
        canvas.EndText();
    }

    // ⚠️ PdfFont 는 '생성된 PdfDocument' 에 묶이므로 캐싱해서 여러 문서에 재사용하면
    //    "Pdf indirect object belongs to other PDF document" 오류가 난다.
    //    → 문서와 무관한 FontProgram(폰트 파일 파싱 결과)만 캐싱하고,
    //      PdfFont 는 BuildPdfPage 호출(=문서)마다 새로 생성한다.
    private static iText.IO.Font.FontProgram? _cachedFontProgram;
    private static bool _fontResolved;

    private static PdfFont LoadKoreanFont()
    {
        if (!_fontResolved)
        {
            var candidates = new[]
            {
                @"C:\Windows\Fonts\malgun.ttf",
                @"C:\Windows\Fonts\gulim.ttc",
                @"C:\Windows\Fonts\batang.ttc",
            };
            foreach (var f in candidates)
            {
                if (!File.Exists(f)) continue;
                try
                {
                    _cachedFontProgram = iText.IO.Font.FontProgramFactory.CreateFont(f);
                    break;
                }
                catch { }
            }
            _fontResolved = true;
        }

        // 한국어 폰트를 찾았으면 매 문서마다 새 PdfFont 생성(임베디드).
        if (_cachedFontProgram != null)
        {
            return PdfFontFactory.CreateFont(_cachedFontProgram,
                iText.IO.Font.PdfEncodings.IDENTITY_H,
                PdfFontFactory.EmbeddingStrategy.PREFER_EMBEDDED);
        }

        // 못 찾으면 표준 폰트로 폴백(이것도 문서마다 새로 생성).
        return PdfFontFactory.CreateFont(StandardFonts.HELVETICA);
    }

    // ── 헬퍼 ─────────────────────────────────────────────────────────────────
    private static bool IsTextCode(ushort c) =>
        IsDigit(c) || IsAlpha(c) || IsHangul(c) || (c >= 0x3000 && c <= 0x9FFF);
    private static bool IsDigit(ushort c)   => c >= 0x30 && c <= 0x39;
    private static bool IsAlpha(ushort c)   => (c >= 0x41 && c <= 0x5A) || (c >= 0x61 && c <= 0x7A);
    private static bool IsHangul(ushort c)  => c >= 0xAC00 && c <= 0xD7A3;
    private static bool IsCjkJunk(ushort c) => (c >= 0x3400 && c <= 0x9FFF) || (c >= 0xFF00 && c <= 0xFFEF);
    private static bool IsThin(ushort h, ushort w, int medH) => h > medH * 1.3 && h >= 2 * Math.Max((int)w, 1);

    private static readonly HashSet<int> PunctOk = "-/().,:%#*+_=' ".Select(c => (int)c).ToHashSet();
    private static bool IsPlausible(ushort c) => IsDigit(c) || IsAlpha(c) || IsHangul(c) || PunctOk.Contains(c);

    // 부품번호 핵심 패턴 (Python _partno_core와 동일): 영숫자 그룹을 하이픈으로 연결한 최장 구간.
    //   → "7338-"+"1026546"=분할도 "7338-1026546"으로 합쳐지고, "!=-1427202-12"의 앞 잡음('!=')은 제거.
    //   숫자/영문/조합 모두 허용. 한글·점('s.p.a')은 자동 제외.
    private const string PartNoBrackets = "()[]{}<>«»‹›＜＞｛｝〈〉《》";
    private static readonly System.Text.RegularExpressions.Regex PartNoCore =
        new(@"[A-Za-z0-9]+(?:[-‐-―][A-Za-z0-9]+)*",
            System.Text.RegularExpressions.RegexOptions.Compiled);

    private static string PartNoCoreOf(string s)
    {
        string best = "";
        foreach (System.Text.RegularExpressions.Match m in PartNoCore.Matches(s ?? ""))
            if (m.Value.Length > best.Length) best = m.Value;
        return best;
    }

    // part_no_sub 후보: 괄호 안에 '숫자 포함' 영숫자 핵심이 있는 토큰('(181420252)')만 인정.
    //   빈 '(' · '(내부포장)'(한글) · '(IT)'(숫자 없음)는 제외.
    private static bool IsSubCandidate(string tok)
    {
        string c = PartNoCoreOf(tok);
        return c != "" && c.Any(ch => ch >= '0' && ch <= '9');
    }

    private static (string PartNo, string PartNoSub) ExtractPartNo(List<List<Word>> lines)
    {
        // 라벨 형식은 'PARTNO (숫자)'. '(숫자)' 괄호 앞의 영숫자+하이픈을 부품번호로, 그 괄호를 sub로.
        //   빈 '(' · '(내부포장)' · '(IT)'는 경계로 안 봄. 유효한 '(숫자)'를 못 찾으면 ("","") 반환
        //   → 엔진이 윗줄을 못 읽었을 때 엉뚱한 값(박스치수 '400-150-150' 등)을 넣느니 비운다.
        foreach (var line in lines)
        {
            var raw = new System.Text.StringBuilder();
            string sub = "";
            foreach (var w in line)
            {
                if (w.Text.Any(ch => PartNoBrackets.Contains(ch)))
                {
                    if (IsSubCandidate(w.Text)) { sub = w.Text; break; }
                    continue;       // 빈 '(' · '(내부포장)' · '(IT)' 등은 무시
                }
                raw.Append(w.Text);
            }
            string core = PartNoCoreOf(raw.ToString());
            if (sub != "" && core != "") return (core, sub);
        }
        return ("", "");
    }

    private static double Median(IEnumerable<double> vals)
    {
        var s = vals.Order().ToList();
        return s.Count == 0 ? 0 : s[s.Count / 2];
    }

    public void Dispose() { try { kRecQuit(); } catch { } }
}
