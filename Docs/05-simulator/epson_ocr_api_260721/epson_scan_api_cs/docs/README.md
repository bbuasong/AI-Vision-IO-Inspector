# 20260720 변경: PDF 생성 건너뛰고 part_no만 받기 (OCR only)

## 왜

`/scan-to-pdf` 는 [엡손 OCR 인식] → [iText로 검색가능 PDF 조립] → [part_no 추출] 순서다.
그런데 `part_no` 는 **OCR 인식 결과(letters)에서 바로 뽑는다.** PDF 파일을 읽어서 뽑는 게 아니다.
즉 PDF 조립(iText)은 part_no 와 무관한 별개 작업인데, 32비트 프로세스에서 이미지 통째로
PDF 에 넣느라 메모리를 크게 먹어 OutOfMemory 가 났다.

→ **PDF 파일이 필요 없을 땐 그 단계만 건너뛰면**, DPI(정확도) 그대로 유지하면서 메모리 문제가 사라진다.
part_no 는 그대로 나온다.

## 쓰는 법

기존과 동일하게 호출하되 쿼리 파라미터 `ocrOnly=true` 만 붙인다.

```
POST http://127.0.0.1:8000/scan-to-pdf?ocrOnly=true
Body: { "scan": {...}, "card": null, "pdf": {...} }
```

- `ocrOnly=true`  : PDF 안 만들고 OCR + part_no 만. (메모리 절약, 권장 - part_no만 필요할 때)
- `ocrOnly` 생략/false : 기존과 동일하게 검색가능 PDF 까지 생성.

응답의 `ocr.part_no`, `ocr.quality` 등은 두 경우 모두 동일하게 나온다.
`ocrOnly=true` 일 때는 `pdf_path` 가 비어있고 `ocr.out_pdf` 가 "" 이다(파일을 안 만들었으니).

EpsonScanTester 를 쓰면 요청 URL 을 `.../scan-to-pdf?ocrOnly=true` 로 바꾸면 된다
(Program.cs 에서 `PostAsync(baseUrl + "/scan-to-pdf", ...)` → `baseUrl + "/scan-to-pdf?ocrOnly=true"`).

## C# 변경 내역 (3개 파일)

### 1) Services/OcrEpson.cs - `ImageToSearchablePdf`
`bool buildPdf = true` 파라미터 추가, buildPdf=false 면 BuildPdfPage 건너뜀.

```csharp
// 시그니처에 buildPdf 추가
public Dictionary<string, object> ImageToSearchablePdf(string imagePath, string outPdf,
                                                        string lang = "kor", bool detectOrientation = false,
                                                        string? partNoOverride = null, bool buildPdf = true)
{
    if (!EnsureInit()) throw new EpsonOcrError(_initError ?? "엔진 사용 불가");
    var (letters, oriented) = RecognizeOriented(imagePath, detectOrientation);

    if (buildPdf)                                        // ← PDF 조립을 옵션으로
        BuildPdfPage(oriented, letters, outPdf, partNoOverride);

    var info = new Dictionary<string, object>
    {
        ["out_pdf"] = buildPdf ? outPdf : "", ["engine"] = "Epson OmniPage",
        ["pages"] = buildPdf ? 1 : 0, ["letters"] = letters.Count,
    };
    var structure = StructureToFields(letters);         // ← part_no는 여기서(letters로) 그대로 추출
    ...
}
```

### 2) Services/OcrEngine.cs - `ImageToSearchablePdf`
`bool buildPdf = true` 추가, 엡손 호출에 전달.

```csharp
public Dictionary<string, object> ImageToSearchablePdf(string imagePath, string outPdf,
                                                        string lang = "kor+eng", string engine = "auto",
                                                        bool buildPdf = true)
{
    ...
    var info = epson.ImageToSearchablePdf(
        imagePath, outPdf, lang, detectOrientation: false,
        partNoOverride: string.IsNullOrEmpty(rpn) ? null : rpn,
        buildPdf: buildPdf);        // ← 전달
    ...
}
```
(참고: tesseract 폴백 경로에는 아직 buildPdf 를 안 넘긴다. 엡손 엔진이 기본이라 실사용엔 문제없음.
 tesseract 로도 OCR only 를 하려면 OcrTesseract.ImageToSearchablePdf 에도 동일 옵션을 추가하면 된다.)

### 3) Program.cs - `POST /scan-to-pdf`
쿼리 파라미터 `ocrOnly` 추가, `buildPdf: !ocrOnly` 로 전달, PDF 안 만들면 PdfPath 비움.

```csharp
app.MapPost("/scan-to-pdf", async ([FromBody] ScanToPdfRequest? body,
                                    [FromQuery] bool ocrOnly, JobRegistry jobs, OcrEngine ocr) =>
{
    ...
    var info = ocr.ImageToSearchablePdf(ocrSrc, outPdf, req.Pdf.Lang, req.Pdf.Engine, buildPdf: !ocrOnly);
    ...
    j.Status = isOk ? "done" : "low_quality"; j.PdfPath = ocrOnly ? null : outPdf; j.Ocr = info;
    ...
});
```

## Python 버전에도 똑같이 적용하는 법

Python 프로젝트(`../epson_scan_api`)는 이 폴더에 연결돼 있지 않아 직접 못 고쳤다.
아래 세 군데를 C# 과 같은 방식으로 고치면 된다. (함수명은 실제 코드에 맞게 확인)

1. **`ocr_epson.py`** 의 `image_to_searchable_pdf(...)` 에 `build_pdf: bool = True` 인자 추가.
   - PDF 만드는 부분(ReportLab 로 캔버스/이미지 그려 저장하는 코드)을 `if build_pdf:` 로 감싼다.
   - part_no 추출(`_extract_partno` / structure 파싱)은 그대로 둔다. (letters 기반이라 PDF 와 무관)
   - 반환 dict 의 `out_pdf` 는 build_pdf 가 False 면 "" 로.

2. **`ocr_engine.py`** 의 디스패처 `image_to_searchable_pdf(...)` 에 `build_pdf=True` 추가 후
   엡손 호출에 `build_pdf=build_pdf` 로 전달.

3. **`main.py`** 의 `/scan-to-pdf` 핸들러에 쿼리 파라미터 추가:
   ```python
   @app.post("/scan-to-pdf")
   def scan_to_pdf(body: ScanToPdfRequest | None = None, ocr_only: bool = False):
       ...
       info = ocr.image_to_searchable_pdf(ocr_src, out_pdf, lang, engine, build_pdf=not ocr_only)
       ...
       pdf_path = None if ocr_only else out_pdf
   ```
   FastAPI 는 `ocr_only: bool = False` 를 자동으로 쿼리 파라미터로 받는다.
   호출: `POST /scan-to-pdf?ocr_only=true`

> 핵심 개념은 C# 과 동일하다: **PDF 조립만 건너뛰고, OCR 과 part_no 추출은 그대로.**
> ReportLab(파이썬) / iText(C#) 가 메모리 먹는 범인이고, part_no 는 거기서 나오는 게 아니다.

## 주의
- 32비트 프로세스 메모리 한계 때문에 생긴 문제라, 근본은 이 옵션으로 회피하는 것.
- DPI 는 낮추지 않는다(정확도 유지). ocrOnly 로 메모리만 아낀다.
- 이 폴더(20260720)는 문서만 들어있다. 실제 코드는 상위 프로젝트에 반영되어 있다.
