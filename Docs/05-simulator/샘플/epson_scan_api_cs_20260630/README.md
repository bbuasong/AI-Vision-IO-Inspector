# Epson Scan API (C# / ASP.NET Core)

EPSON ES-C320W 스캐너 → 검색가능 PDF 파이프라인. Python(FastAPI) 버전의 C# 포트.

스캔(수동) → 작업 목록 → (선택)라벨 추출 / 전처리 / 영역 덮기 → 검색가능 PDF 저장.

- 스캔: **WIA**(COM dynamic dispatch) — ES-C320W가 WIA로 인식됨
- OCR/PDF (기본): **Epson 번들 OmniPage 엔진**(Kofax/Nuance CSDK) — P/Invoke로 직접 호출. 한국어. 검색가능 PDF·한글 텍스트 레이어 담당.
- **부품번호(part_no): RapidOCR 하이브리드** — Epson은 흐린 윗줄 part_no를 자주 깨뜨려서(`87S7`→`「느:`), **RapidOCR**(파이썬 사이드카, 번들 동봉)로 part_no를 따로 읽어 화해. PDF의 부품번호 줄도 RapidOCR 값으로 교체(검색 가능). 한글은 Epson 유지. (x86 .NET엔 onnxruntime 네이티브가 없어 사이드카 사용 — 아래 "부품번호 인식" 참고)
- OCR/PDF (폴백): **Tesseract 5.x** — Epson 엔진 사용 불가 시 자동 폴백
- 라벨 추출: 자동 deskew(기울기 보정) + 라벨 영역 크롭 (projection-variance 알고리즘, 추가 설치 불필요)
- 전처리(선택): 그레이스케일·이진화·디스큐·대비·리사이즈·회전·노이즈 제거 — **주로 Tesseract용. Epson 엔진은 자체 전처리를 하므로 auto/epson에선 자동 생략** (아래 "처리 단계" 참고)
- 영역 덮기(redaction): 특정 영역을 박스로 가림 (DPI/해상도 보존 → OCR 품질 영향 없음)
- OCR 결과: 검색가능 PDF + 줄/단어 단위 파싱 필드(bbox 포함) + 부품번호(part_no) + 품질 점수 + `needs_rescan`
- **방향 자동감지 미사용**: 작업자가 라벨을 정해진 방향(똑바로)으로 넣는 전제(부작용 제거).
- **다중 스캔 다수결(`POST /vote`)**: 흐린 라벨은 같은 라벨을 여러 번(다시 올려) 스캔해 part_no 합의.
- **64비트 클라이언트와 자유롭게 연동 가능** — 서버 프로세스만 x86, HTTP 경계가 비트니스 분리

## 필요 환경 / 사전 준비

| 구분 | 요구사항 | 비고 |
|------|----------|------|
| OS | **Windows 10 / 11** | WIA·Epson 엔진 모두 Windows 전용 |
| .NET SDK | **.NET 8.0** | `dotnet --version` 으로 확인 |
| 빌드 타겟 | **x86 (32비트)** | 프로젝트에 이미 설정됨 — 별도 조치 불필요 |
| 스캐너 드라이버 | **EPSON ES-C320W WIA 드라이버** | 아래 "스캐너 드라이버" 참고 |
| Epson OCR 엔진 | **Epson Scan OCR Component Pro** (ScanSmart 번들) | 아래 "Epson OCR 엔진" 참고 |
| (폴백) OCR | **Tesseract 5.x** + `kor`/`eng` 데이터 | 선택. Epson 엔진 불가 시 자동 폴백 |

> Python 버전과 달리 **별도의 32비트 venv 생성이 필요 없습니다.** `<PlatformTarget>x86</PlatformTarget>` 설정이 .csproj에 이미 포함되어 있어 `dotnet build` 한 번으로 됩니다.

### 스캐너 드라이버 (WIA)

스캔은 **WIA(Windows Image Acquisition)** COM 인터페이스로 합니다 (`ScannerWia.cs`).

- **EPSON ES-C320W** 용 드라이버를 설치하면 WIA 스캐너로 등록됩니다.
  - **Epson Scan 2** (스캐너 드라이버) — Epson 공식 다운로드. WIA 인터페이스 제공.
  - 또는 OS 표준 **WIA 드라이버** (Windows Update / 제조사 패키지).
- 설치 후 `GET /scanners` 로 인식 여부를 확인하세요 (Type==1 스캐너만 표시).
- 스캔은 수동/온디맨드: 용지를 올린 상태에서 호출해야 하며, 용지가 없으면 드라이버가
  에러(예: `0x80210003` ADF empty)를 반환하고 API가 정리해 전달합니다.
- ADF(급지)/평판(flatbed)은 `source` 로 선택하며, 장치가 ADF 전용이면 자동으로 급지를 강제합니다.

### Epson OCR 엔진 (기본 OCR/PDF)

기본 OCR/PDF는 Epson 번들 **OmniPage(Kofax/Nuance) CSDK** 를 **P/Invoke** 로 직접 호출합니다 (`OcrEpson.cs`).
별도 설치 없이, ScanSmart 설치 PC에 깔려 있는 아래 컴포넌트/파일을 그대로 사용합니다:

- 엔진 DLL: `C:\Program Files (x86)\EPSON Software\Scan OCR Cmponent Pro\NuOCR\KernelAPI.dll`
- 출력 포맷 모듈: `...\Scan OCR Cmponent Pro\DCP\` 및 `DCP\ffmt\`
- 라이선스: `...\NuOCR\epson.lcxz` (+ OEM 코드)

기본 엔진을 쓰려면 **Epson ScanSmart / Scan OCR Component Pro** 가 설치돼 있어야 합니다.
설치/경로가 없으면 엔진이 자동 비활성화되고 Tesseract로 폴백합니다 (`GET /health`·`GET /languages`로 상태 확인).

> **비트니스 참고:** Epson DLL이 32비트라 서버 프로세스는 x86으로 빌드됩니다.
> 하지만 64비트 클라이언트(WPF, WinForms, 웹 등)에서 HTTP API로 호출하는 데는 전혀 지장 없습니다.

### Tesseract (폴백 OCR)

Epson 엔진을 못 쓰는 환경에서 검색가능 PDF를 만들려면:

- **Tesseract 5.x** (UB Mannheim 빌드 권장) 설치.
- `tessdata`에 `kor.traineddata`, `eng.traineddata` 배치.
- 설치 경로는 자동 탐색합니다 (`C:\Program Files\Tesseract-OCR\...` 등). 커스텀 경로는
  `TESSDATA_PREFIX` 환경변수 또는 `appsettings.json`의 환경변수 설정으로 지정 가능.

## 빌드 및 실행

### 빌드

```cmd
cd epson_scan_api_cs
dotnet build
```

NuGet 패키지는 빌드 시 자동으로 복원됩니다:

| 패키지 | 용도 |
|--------|------|
| `Swashbuckle.AspNetCore` | Swagger UI (`/swagger`) |
| `SixLabors.ImageSharp` | 이미지 처리 (PIL 대체) |
| `Tesseract` | Tesseract OCR 래퍼 |
| `itext7` + `itext7.bouncy-castle-adapter` | 검색가능 PDF 생성 (ReportLab 대체) |

### 실행

```cmd
dotnet run
# 또는 빌드된 바이너리 직접:
bin\Debug\net8.0-windows\EpsonScanApi.exe
```

- Swagger UI: `http://localhost:8000/swagger`
- 엔진 상태 확인: `GET /health` 또는 `GET /languages`

### 설정 (appsettings.json)

```jsonc
{
  "ScanOutputDir": "D:\\epson_scans",   // 스캔/PDF 결과 저장 폴더
  "ScanApiHost":   "127.0.0.1",         // 바인드 주소. 다른 PC 접속 허용은 "0.0.0.0"
  "ScanApiPort":   8000                 // 서버 포트
}
```

환경변수로도 재정의 가능합니다 (ASP.NET Core 표준 방식):

| 환경변수 | 기본값 | 설명 |
|----------|--------|------|
| `ScanOutputDir` | `D:\epson_scans` | 스캔/PDF 결과 저장 폴더 |
| `ScanApiHost` | `127.0.0.1` | 바인드 주소. 다른 PC 접속 허용은 `0.0.0.0` |
| `ScanApiPort` | `8000` | 서버 포트 |
| `TESSDATA_PREFIX` | (자동탐색) | Tesseract `tessdata` 경로 직접 지정 |

## API 흐름

```
GET    /scanners                  # 연결된 스캐너 확인
GET    /languages                 # 사용 가능한 OCR 언어 + 엔진 상태
GET    /health                    # 엔진(Epson/RapidOCR/Tesseract) 상태 + 출력 경로
POST   /scan                      # 종이 올리고 호출 -> 스캔, 작업 생성
GET    /jobs                      # 작업(스캔) 목록
GET    /jobs/{id}                 # 작업 상세
DELETE /jobs/{id}                 # 작업 삭제
POST   /jobs/{id}/extract-card    # (선택) 라벨 자동 검출+deskew+크롭
POST   /jobs/{id}/preprocess      # (선택) 전처리
POST   /jobs/{id}/redact          # (선택) 특정 영역 덮기
POST   /jobs/{id}/pdf             # 검색가능 PDF 생성
GET    /jobs/{id}/fields          # OCR 파싱 필드(줄/단어 + bbox, part_no)
GET    /jobs/{id}/download/{kind} # kind: image|processed|card|redacted|pdf
POST   /scan-to-pdf               # 스캔→(라벨추출)→(전처리)→(덮기)→PDF 한 번에
POST   /vote                      # 다중 스캔 다수결  {"job_ids":["id1","id2","id3"]} -> part_no 합의
```

가공 단계(extract-card / preprocess / redact)는 **선형 누적**입니다. 각 단계는 직전 결과 위에
적용되고, 마지막 결과로 OCR을 수행합니다.

### 요청 예시 — POST /scan

```jsonc
POST /scan
{
  "device_id": null,     // null이면 첫 번째 스캐너 자동 선택
  "dpi":       300,
  "mode":      "gray",   // color | gray | bw
  "source":    "flatbed",// flatbed | feeder
  "fmt":       "bmp"     // bmp | png | jpeg
}
```

### 요청 예시 — POST /jobs/{id}/redact

```jsonc
POST /jobs/{id}/redact
{
  "rects": [[100, 200, 300, 60]],  // [x, y, width, height] 픽셀 좌표
  "fill":  "white"                  // white | black
}
```

### 요청 예시 — POST /jobs/{id}/pdf

```jsonc
POST /jobs/{id}/pdf
{
  "lang":   "kor+eng",  // Tesseract 언어 코드 (Epson 엔진은 항상 한국어)
  "engine": "auto"      // auto(Epson 우선→폴백) | epson | tesseract
}
```

### 요청 예시 — POST /scan-to-pdf (한 번에)

```jsonc
POST /scan-to-pdf
{
  "scan":    {"mode":"gray", "dpi":300, "source":"flatbed"},
  "card":    {"dpi":300},                                    // 기본 ON (라벨 deskew+크롭). 끄려면 "card":null
  "redact":  {"rects":[[100,200,300,60]], "fill":"white"},   // 선택
  "pdf":     {"lang":"kor", "engine":"auto"}
}
```

실행 순서: 스캔 → (카드 추출) → (전처리) → (덮기) → OCR/PDF.

## 처리 단계: 어디까지 코드가 하고, 어디부터 Epson 엔진(DLL)이 하나

스캔 → 검색가능 PDF까지 단계마다 "주체"가 다릅니다. 핵심은 **이미지 화질 정리(이진화·잡티 제거 등)는
Epson 엔진 DLL이 자체적으로 하므로, 코드에서 중복으로 전처리하면 오히려 인식이 나빠진다**는 점입니다.

| 단계 | 주체 | 내용 |
|------|------|------|
| 스캔 | **코드** (WIA / `ScannerWia`) | 스캐너에서 원본 이미지 획득 |
| 라벨 추출 (extract-card) | **코드** (`CardExtractor`) | 라벨 검출 + deskew(기울기 보정) + 크롭. 엔진은 라벨을 찾거나 잘라주지 못하므로 **코드가 한다.** |
| 입력 정규화 (`NormalizeImage`) | **코드** | 그레이 변환(필요 시) + DPI 보정(150 미만→300). 엔진이 제대로 읽도록 최소 정규화만. |
| **이미지 화질 정리** | **Epson 엔진 DLL** (`kRecPreprocessImg`) | **이진화 · 잡티 제거(despeckle) · 미세 deskew · 대비 정리. 여기는 엔진 담당.** |
| 문자 인식 | **Epson 엔진 DLL** (`kRecRecognizeW`/`kRecGetLetters`) | 글자 코드 + 좌표 추출 |
| 검색가능 PDF 합성 | **코드** (iText7) | 이미지 위에 투명 텍스트 레이어 |
| 필드 파싱 (part_no 등) | **코드** (`OcrEpson.StructureToFields`) | 줄/단어 재구성, 부품번호 추출, 품질 평가 |

### 그래서 수동 전처리(preprocess)는?

- 수동 전처리(`Preprocessor`: 이진화/denoise/대비/디스큐)는 **엔진이 하는 일과 겹쳐서** Epson 경로에선
  오히려 화질을 깎습니다(특히 denoise가 잔글씨를 뭉갬).
- 그래서 `POST /scan-to-pdf`는 **`engine`이 `auto`/`epson`이면 수동 전처리를 자동으로 건너뜁니다.**
  `tesseract`일 때만 적용합니다(Tesseract는 엔진 내부 정리가 약해 전처리가 도움됨).
- 수동 전처리를 꼭 강제하려면 단독 엔드포인트 `POST /jobs/{id}/preprocess`를 직접 호출하세요.

> 엔진 전처리 강도(deskew/despeckle/이진화 모드)를 더 조절하려면 OmniPage 커널 설정 API가 필요한데,
> 이 번들 `KernelAPI.dll`에 해당 export가 있는지는 미확인입니다(`NUANCE_FINDINGS.md` 참고).
> `dumpbin /exports KernelAPI.dll`로 `kRecSetting*` 류가 보이면 추가 튜닝이 가능합니다.

## 라벨 추출 (extract-card)

평판 스캔본은 원근 왜곡이 거의 없고 '회전(skew)'만 있으므로, **projection-variance deskew**
알고리즘으로 자동 기울기 보정 + 라벨 영역 크롭을 수행합니다 (`CardExtractor.cs`).

Python 버전의 `card_pil.py` 알고리즘을 그대로 C#으로 포팅한 것이며, OpenCV나 별도 프로세스가
**전혀 필요 없습니다.** 서버 프로세스 안에서 직접 동작합니다.

- 잉크 마스크 생성 → Otsu 임계값 (cap 제한) → 3x3 open(침식+팽창)으로 노이즈 제거
- projection-variance로 ±20° 범위에서 최적 각도 탐색 (coarse 1° → fine 0.2°)
- 잉크 bounding box로 크롭 + margin 4%

> Python 버전에서 `CARD_EXTRACT_PY`(OpenCV 64비트)를 쓰던 원근 보정(perspective warp)은
> 이 C# 버전에 포함되지 않습니다. 평판 스캔에는 deskew만으로 충분합니다.

## OCR 결과 / 필드

PDF 생성(`POST /jobs/{id}/pdf`, `POST /scan-to-pdf`) 응답과 `GET /jobs/{id}/fields`에서
파싱 결과를 제공합니다.

```jsonc
{
  "text":    "7338-1026546\n(181420252)\n...",  // 전체 텍스트
  "lines":   [                                   // 줄 단위
    {
      "line": 1,
      "text": "7338-1026546",
      "words": [
        { "text": "7338-", "bbox": [x, y, w, h] },
        { "text": "1026546", "bbox": [x, y, w, h] }
      ]
    }
  ],
  "fields":  { "line_1_1": "7338-", "line_1_2": "1026546", ... },  // 줄R 단어C
  "part_no":     "7338-1026546",     // 최종 부품번호 (RapidOCR 우선, 화해 규칙)
  "part_no_sub": "(181420252)",      // 괄호번호 — RapidOCR 사용 시 RapidOCR 값(깔끔한 (숫자)만)
  "part_no_epson":      "...",       // Epson이 읽은 값
  "part_no_rapid":      "...",       // RapidOCR이 읽은 부품번호
  "part_no_rapid_sub":  "(181420252)", // RapidOCR이 읽은 괄호번호
  "part_no_rapid_conf": 0.93,        // RapidOCR 신뢰도
  "part_no_source":     "both",      // both | rapid | rapid_disagree | epson | none
                                     //   part_no/part_no_sub 둘 다 RapidOCR로 일원화(영숫자 정확)
  "needs_rescan":       false,       // true면 다시 올려 스캔 후 POST /vote 권장
  "quality": {
    "letters":      87,
    "valid_ratio":  0.94,
    "confidence":   0.94,
    "ok":           true,
    "reason":       ""
  }
}
```

- `quality.ok = false` → 작업 `status = low_quality` (흐림/엉뚱한 스캔 의심 → 재스캔 유도)
- 방향 자동감지는 사용하지 않음(작업자가 라벨을 똑바로 넣는 전제).

## 부품번호 인식 (RapidOCR 파이썬 사이드카)

Epson은 한글·검색가능 PDF는 잘 만들지만 흐린 **부품번호 윗줄**을 자주 깨뜨린다(`87S7`→`「느:`). 그래서 part_no만 **RapidOCR**(파이썬/ONNX)로 따로 읽는다.

> **왜 사이드카인가:** C# 서버는 Epson 32비트 DLL 때문에 **x86 프로세스**여야 하는데, .NET용 onnxruntime은 x86 네이티브가 없어 인프로세스로 못 띄운다(`Microsoft.ML.OnnxRuntime.NativeMethods` 타입 초기화 실패). 반면 **파이썬 onnxruntime은 32비트 win32 휠이 있어** 잘 돈다. 그래서 RapidOCR만 파이썬 사이드카(별도 프로세스)로 돌리고 C#이 HTTP로 호출한다.

- 흐름: `OcrEngine`이 사이드카로 part_no를 먼저 읽고 → Epson으로 PDF 생성(이때 PDF 텍스트 레이어 **부품번호 줄을 RapidOCR 값으로 교체**) → 두 값 화해(`ReconcilePartNo`). 한글·PDF는 Epson 유지.
- 사이드카가 없거나 실패하면 자동으로 Epson 결과로 폴백(무해). `GET /health`의 `engines.rapidocr.available`로 확인.
- C# 서버가 시작 후 첫 요청 때 **사이드카를 자동 기동**한다. 우선순위: `rapid_sidecar.exe` → (없으면) `pyruntime\python.exe rapid_sidecar.py`.
- 코드: `Services/OcrRapid.cs`(사이드카 HTTP 클라이언트), `rapid_sidecar.py`(사이드카).

### 납품 배포 — 단일 exe 하나만 동봉 (고객 PC에 파이썬 설치 불필요)

사이드카를 **단일 실행파일 `rapid_sidecar.exe`**로 구워서 앱과 함께 넣는다. 고객 PC엔 파이썬·설치 일절 필요 없다.

**1) (개발 PC, 1회) 사이드카 exe 굽기** — `build_sidecar.bat` (`epson_scan_api_cs\`)
```cmd
:: rapidocr 가 설치된 venv32 를 활성화한 상태에서 실행 (anaconda 등 다른 파이썬에서 돌리면
::  obsolete 'pathlib' 백포트 충돌이 날 수 있음 → venv32 활성화 후 실행)
venv32\Scripts\activate.bat
build_sidecar.bat
:: → dist\rapid_sidecar.exe 생성
```
`.csproj`가 `dist\rapid_sidecar.exe`를 빌드/게시 시 출력 루트(`EpsonScanApi.exe` 옆)로 **자동 복사**한다. (루트에 따로 복사할 필요 없음. `rapid_sidecar.py`는 런타임 불필요라 복사 안 함)

**2) 납품본 만들기** — `publish.bat` **더블클릭** (또는 실행)
```cmd
publish.bat
```
- self-contained x86로 게시 → **`bin\Publish\EpsonScanApi-win-x86\`** 에 생성 (Debug/Release와 나란히, `%~dp0` 상대경로라 프로젝트 옮겨도 OK)
- VS로 하려면: 프로젝트 우클릭 → 게시 → `win-x86-selfcontained` 프로파일 (같은 위치로 감)

**3) 배포** — `bin\Publish\EpsonScanApi-win-x86\` 폴더를 **통째로 zip해서 전달**. 안에 `EpsonScanApi.exe` + `rapid_sidecar.exe` + .NET 런타임(DLL 다수=정상)이 들어있어 **고객 PC에 .NET·파이썬 설치 불필요**.

**동작 방식**
- 서버 시작 시 `OcrRapid`가 **앱 폴더의 `rapid_sidecar.exe`를 자동 기동·예열**한다(첫 스캔 콜드스타트 방지). 앱 폴더 기준 상대경로라 설치 위치 무관.
- `appsettings.json`: `RapidSidecarExe`(exe 경로/이름), `RapidSidecarUrl`(포트).

**개발 중 (exe 안 구울 때)**
- `appsettings.json`의 `RapidPythonExe`를 venv32 절대경로(`...\venv32\Scripts\python.exe`)로 지정하면 `rapid_sidecar.py`로 바로 돈다. (Epson DLL 때문에 .NET용 onnxruntime은 x86 인프로세스 불가 → 사이드카가 정답)

**문제 해결 — `GET /health` 의 `engines.rapidocr`**
```jsonc
"rapidocr": {
  "available": true,           // false면 사이드카 미연결
  "exe": "...\\rapid_sidecar.exe",
  "exe_exists": true,          // false면 exe가 그 폴더에 없음 → 복사
  ...
}
```
- `exe_exists:false` → `rapid_sidecar.exe`를 `exe` 경로 폴더(보통 `bin\...\`)에 복사.
- `available:false` 인데 `exe_exists:true` → 콜드스타트 대기 중일 수 있음(잠시 후 재확인). 사이드카만 따로 `rapid_sidecar.exe` 실행해 콘솔에 `RapidOCR available=True` 뜨는지 확인.
- 사이드카는 떴는데 part_no만 빈다면 → C#이 본문을 **길이 명시(StringContent)**로 보내야 함(파이썬 stdlib 서버는 chunked 본문을 못 읽음). 현재 코드에 반영돼 있음(`OcrRapid.PostOnce`).

### 32비트 메모리 / 큰 스캔

Epson DLL 때문에 서버가 x86(주소공간 제한)이라 큰 스캔(예: 2550×4650)에서 메모리 고갈이 날 수 있다. 대응(반영됨):
- `Program.cs`: ImageSharp 메모리 풀 끔(누적 방지).
- `CardExtractor`: 그레이 L8 직접 회전(불필요한 RGBA 복제 제거) + 스캔마다 GC + **결과 크기 상한(2200px)** — deskew 오각도/크롭 실패로 거대 이미지가 만들어져 PDF(iText)가 터지는 것 방지.
- 카드 추출이 메모리로 실패하면 **크롭만 건너뛰고 원본으로 OCR**(스캔이 500으로 죽지 않음).

## 적응형 워크플로 (느린 3회 스캔 대신)

대부분 **1회 스캔으로 끝**. 응답 `needs_rescan`이 `false`면 `part_no` 그대로 사용. `true`(흐려서 불확실)인 라벨만 **다시 올려서** 1~2회 더 스캔한 뒤 그 작업 ID들을 `POST /vote`로 묶어 다수결한다.

- `POST /vote {"job_ids":[...]}` → `{part_no, confidence, n, needs_review, candidates}`
- 같은 라벨을 **다시 올려** 스캔할 때만 효과(위치/기울기가 달라 서로 다른 곳에서 틀리므로).
- 코드: `PartNoVoter.Vote`.

## 영역 덮기 시 OCR 품질 유지 (중요)

자르기/덮기 단계에서 인식률이 떨어지지 않게:

- **해상도(픽셀)를 줄이지 마세요** — 다운스케일하면 글자가 뭉개짐.
- **DPI를 유지**하세요 — 윈도우 기본 자르기 툴은 DPI를 96으로 떨굼 → 인식 저하.
  (이 API의 `/redact`와 엔진 입력 정규화는 DPI를 보존/300 보정합니다.)
- **JPEG 저장 금지** — 압축 잡티가 OCR을 깎음. `/redact`는 확장자가 `.jpg`여도 PNG로 강제 저장합니다.

## 파일 구조

```
epson_scan_api_cs/
├── EpsonScanApi.csproj          # x86, net8.0-windows, NuGet 참조 (+ dist\rapid_sidecar.exe 자동 복사)
├── appsettings.json             # 출력 경로 · 호스트 · 포트 · RapidOCR 사이드카 경로
├── Program.cs                   # ASP.NET Core Minimal API — 모든 엔드포인트 (+ /vote)
├── rapid_sidecar.py             # RapidOCR part_no 사이드카 소스 (런타임엔 exe만 필요)
├── build_sidecar.bat           # (1회) rapid_sidecar.py → dist\rapid_sidecar.exe (PyInstaller, venv32에서)
├── publish.bat                 # (납품) self-contained x86 게시 → bin\Publish\EpsonScanApi-win-x86
├── dist/rapid_sidecar.exe       # build_sidecar.bat 결과물 (빌드/게시 시 출력 루트로 자동 복사됨)
├── Properties/PublishProfiles/
│   └── win-x86-selfcontained.pubxml   # VS '게시'용 (publish.bat과 동일 위치로 출력)
├── Models/
│   ├── Requests.cs              # 요청 DTO (ScanRequest, PdfRequest, VoteRequest 등)
│   └── JobModel.cs              # 작업 모델
└── Services/
    ├── JobRegistry.cs           # 인메모리 작업 레지스트리 (ConcurrentDictionary + JSON 영속화)
    ├── ScannerWia.cs            # WIA 스캐너 제어 (COM dynamic dispatch)
    ├── OcrEngine.cs             # 엔진 디스패처 (Epson 우선 → Tesseract 폴백) + RapidOCR 화해(part_no/sub) + 다수결
    ├── OcrEpson.cs              # Epson OmniPage P/Invoke 래퍼 + iText7 검색가능 PDF (부품번호 줄 교체 지원)
    ├── OcrRapid.cs             # RapidOCR 사이드카 HTTP 클라이언트 (번들 exe 자동 기동·예열·폴백)
    ├── PartNoVoter.cs          # 다중 스캔 다수결 (medoid + 신뢰도)
    ├── OcrTesseract.cs          # Tesseract CLI 래퍼 (폴백)
    ├── Preprocessor.cs          # 이미지 전처리 (ImageSharp)
    ├── Redactor.cs              # 영역 덮기 — DPI/해상도 보존 PNG
    └── CardExtractor.cs         # 라벨 자동 추출 — deskew + 크롭 (32비트 메모리 절감 + 크기 상한)
```

## Python 버전과의 차이점

| 항목 | Python 버전 | C# 버전 |
|------|------------|---------|
| 프레임워크 | FastAPI + Uvicorn | ASP.NET Core Minimal API |
| 비트니스 | 32비트 venv 별도 생성 필요 | `.csproj`에 x86 설정, `dotnet build` 한 번 |
| 이미지 처리 | Pillow (PIL) | SixLabors.ImageSharp |
| PDF 생성 | ReportLab | iText7 |
| Tesseract | pytesseract | tesseract.exe CLI subprocess |
| WIA 스캐너 | pywin32 COM | .NET dynamic COM dispatch |
| OpenCV 라벨추출 | 별도 64비트 프로세스 (`CARD_EXTRACT_PY`) | 미지원 (deskew+크롭만 내장) |
| 설정 파일 | 환경변수 | `appsettings.json` + 환경변수 |
| Swagger | `/docs` | `/swagger` |

## 트러블슈팅

### Epson 엔진이 `GET /health` 에서 `available: false`로 표시됨

1. **ScanSmart / Scan OCR Component Pro** 가 설치돼 있는지 확인.
2. `C:\Program Files (x86)\EPSON Software\Scan OCR Cmponent Pro\NuOCR\KernelAPI.dll` 존재 여부 확인.
3. `epson.lcxz` 라이선스 파일이 같은 경로에 있는지 확인.
4. 빌드가 정말 x86인지 확인: `bin\Debug\net8.0-windows\EpsonScanApi.exe` → 우클릭 → 속성 → 32비트 여부.

### 스캐너가 `/scanners` 에 안 나옴

- Epson Scan 2 드라이버가 설치됐는지 확인.
- 제어판 → 스캐너 및 카메라 에서 장치가 보이는지 확인 (WIA Type==1 으로 등록돼야 함).
- USB/네트워크 연결 상태 확인.

### 스캔 시 `0x80210003` 오류 (용지 없음)

- ADF 급지대에 용지가 올바르게 삽입됐는지 확인. 롤러가 물 때까지 끝까지 밀어 넣으세요.
- 평판(flatbed)이면 `"source": "flatbed"` 로 요청하세요.

### Tesseract 폴백도 동작 안 함

- `C:\Program Files\Tesseract-OCR\tesseract.exe` 존재 여부 확인.
- `tessdata\kor.traineddata` 또는 `eng.traineddata` 파일이 있는지 확인.
- `TESSDATA_PREFIX` 환경변수로 커스텀 경로를 명시하거나, `appsettings.json`에 추가:
  ```json
  "TESSDATA_PREFIX": "D:\\my_tessdata"
  ```
