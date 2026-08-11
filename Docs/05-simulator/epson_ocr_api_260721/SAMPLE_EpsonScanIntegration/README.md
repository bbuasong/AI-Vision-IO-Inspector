# 라벨 스캔 → 부품번호 받기 (실무 적용 가이드)

스캐너에 라벨을 올리고 호출하면 **부품번호(part_no)를 돌려주는 함수**를 준비해 둔 모음입니다.
버튼이나 실행 시점 같은 UI/흐름 결정은 **앱 개발자가 자유롭게 배치**하도록 일부러 비워 두었습니다.
이 문서는 "왜 이렇게 만들었는지"와 "개발자가 정해야 할 2가지"를 설명합니다.

> 이 폴더는 **IO Inspector 안에 복사해서 쓰는 참고 코드**입니다. 폴더 자체는 빌드/실행하지 않습니다.
> 동작만 빠르게 확인하려면 별도의 `EpsonScanTester` 프로젝트에서 `dotnet run` 하세요.

## 0. 파일 한눈에 보기 (뭐가 뭔지)

| 파일 | 한마디로 | 꼭 필요? |
|------|----------|----------|
| `Infrastructure/EpsonScanClient.cs` | **부품번호 받는 함수.** `ScanPartNo()` 부르면 스캔→OCR→부품번호 | **필수** |
| `Infrastructure/EpsonScanServerHost.cs` | **서버 켜는 함수.** `EnsureRunningAsync()` 부르면 Epson 서버 자동 실행 | 선택 |
| `Infrastructure/EpsonScanLabelService.cs` | 부품번호 받는 함수의 **비동기(async) 버전** (UI에 붙이기 좋음) | 선택 |
| `Infrastructure/EpsonScanOptions.cs` | 위 비동기 버전의 **설정값**(주소/DPI/언어 등) | 선택 |
| `Application/ILabelScanService.cs` | 비동기 버전의 **인터페이스**(나중에 스캐너 바뀌어도 호출부 유지) | 선택 |
| `Application/LabelScanResult.cs` | 비동기 버전이 돌려주는 **결과 묶음**(부품번호+상태+신뢰도) | 선택 |
| `README.md` | 이 설명서 | - |

가장 단순하게 가면 **`EpsonScanClient.cs` 하나**만 쓰면 됩니다.
"앱 켜질 때 서버도 자동으로" 하고 싶으면 **`EpsonScanServerHost.cs`**(서버 켜는 함수)를 추가로 쓰세요.
나머지는 더 깔끔한 구조가 필요할 때 쓰는 선택지입니다.

---

## 1. 왜 HTTP 서버 방식인가

스캐너 제어(WIA)와 Epson OCR 엔진은 **32비트(x86) 전용**입니다.
반면 IO Inspector 본체는 64비트입니다.
하나의 프로세스 안에서는 32비트와 64비트를 섞어 올릴 수 없습니다.

그래서 Epson Scan 기능을 **별도 프로세스(작은 HTTP 서버)** 로 떼어 두고,
IO Inspector 는 그 서버에 HTTP 로 요청만 보냅니다. 이렇게 하면:

- 비트니스 충돌이 사라집니다. (HTTP 경계가 32/64비트를 분리)
- 본체 코드는 스캐너 SDK 에 직접 의존하지 않습니다. (느슨한 결합)
- 서버를 따로 켜두거나, 재시작하거나, 다른 PC 에 둘 수도 있습니다.
- 기존 VisionWorker 를 별도 프로세스로 두는 구조와 같은 발상입니다.

요약하면, **"스캐너 일은 32비트 서버가, 검사 일은 64비트 본체가"** 하고
둘은 HTTP 로만 대화합니다. 그 대화 한 줄이 "스캔해서 부품번호 줘" 입니다.

---

## 2. 준비된 함수 (이미 만들어 둠)

### (A) 부품번호 받는 함수 - 핵심
`Infrastructure/EpsonScanClient.cs` 의 정적 함수. 서버가 떠 있으면 한 줄로 끝납니다.

```csharp
// 권장: 상태까지 함께 받기
EpsonScanClient.ScanPartNoResult r = EpsonScanClient.ScanPartNo();
//   r.Outcome    : Ok | LowQuality | NotFound | ScanError
//   r.PartNo     : 부품번호 (실패/미검출이면 "")
//   r.Confidence : 0.0 ~ 1.0
//   r.Message    : 저품질 사유 / 오류 메시지

// 초단순: 문자열만, 실패/저품질이면 "" 반환
string partNo = EpsonScanClient.ScanPartNoOrEmpty();
```

> 이 함수는 동기(blocking)입니다. WPF UI 스레드에서 직접 부르면 멈출 수 있으니
> `await Task.Run(() => EpsonScanClient.ScanPartNo())` 처럼 워커 스레드에서 호출하세요.
> 또는 비동기 버전(아래 B)을 쓰면 됩니다.

### (B) 비동기 버전 (선택)
UI 와 자연스럽게 쓰려면 `Infrastructure/EpsonScanLabelService.cs` 의
`await scanService.ScanPartNoAsync(token)` 을 쓰면 됩니다. 인터페이스(`ILabelScanService`)로
주입해 두면 나중에 스캐너 방식이 바뀌어도 호출부는 그대로입니다.
간단히 쓸 거면 (A) 만으로 충분합니다.

### (C) 서버 실행 헬퍼 (선택) - 시점은 개발자가 결정
`Infrastructure/EpsonScanServerHost.cs`. 어디서든 한 번 호출하면
서버가 없으면 띄우고, 이미 있으면 그대로 씁니다.

```csharp
EpsonScanServerHost host = new EpsonScanServerHost(new EpsonScanServerOptions
{
    ExecutablePath = @"...\EpsonScan\EpsonScanApi.exe",
    BaseUrl = "http://127.0.0.1:8000"
});
await host.EnsureRunningAsync(CancellationToken.None); // 준비될 때까지 대기
// 앱 종료 시 host.Dispose(); (우리가 띄운 경우에만 종료됨)
```

이 헬퍼를 **언제 호출할지는 강제하지 않습니다.** 아래 3번이 그 결정입니다.

---

## 2.5 메모리 안전 기본값 (ocrOnly / card) - 중요

서버가 **32비트**라 큰 이미지에서 메모리 부족(OutOfMemory)이 나기 쉽다. 그래서 이 샘플은 기본을 이렇게 잡았다:

- **PDF 생성 건너뛰기**: 요청 URL 에 `?ocrOnly=true` 를 붙인다. 서버가 검색가능 PDF(iText) 조립을 건너뛰고 OCR + part_no 만 한다. **DPI(정확도)는 그대로**, 메모리만 아낀다. (part_no 만 필요하니 PDF 파일은 불필요)
- **카드 추출 끄기**: `card` 를 **명시적으로 `null`** 로 보낸다. (안 보내면 서버가 기본값(켜짐)으로 채워서 카드 추출이 돌다 메모리 부족이 날 수 있음)

코드 반영:
- `EpsonScanClient` : `/scan-to-pdf?ocrOnly=true` + `card = (object)null` 로 이미 설정됨.
- `EpsonScanLabelService` : `EpsonScanOptions.OcrOnly`(기본 true), `UseCardExtraction`(기본 false) 로 제어.

검색가능 PDF 파일이 정말 필요하면 `OcrOnly=false` 로 두되, 32비트 메모리 여유를 확인해야 한다.

서버/사이드카의 튜닝값(품질 임계값, part_no 정규식 등)을 바꾸는 방법은
서버 프로젝트의 `20260720/CONFIG.md` 참고.

---

## 3. 개발자가 정해야 할 것 ①: 서버 실행 시점

서버는 부품번호 함수를 부르기 전에 떠 있기만 하면 됩니다. 후보:

| 방식 | 설명 | 추천 상황 |
|------|------|-----------|
| 앱 시작 시 (`App.OnStartup`) | `EnsureRunningAsync()` 를 한 번 호출, 종료 시 `Dispose()` | 스캔을 자주 쓰는 경우. 첫 스캔이 빠름 |
| 첫 스캔 직전 (지연 실행) | 스캔 함수 부르기 직전에 `EnsureRunningAsync()` | 스캔을 가끔만 쓰는 경우 |
| 외부에서 수동/서비스로 상시 구동 | 앱은 실행에 관여 안 함. HTTP 호출만 | 운영 환경, 여러 앱이 공유 |

세 방식 모두 `EpsonScanClient` 호출 코드는 똑같습니다.
헬퍼(C)는 "이미 떠 있으면 재사용, 우리가 띄운 것만 종료"라 어느 방식과도 충돌하지 않습니다.

---

## 4. 개발자가 정해야 할 것 ②: 부품번호 함수를 어디에 배치할까

"스캔 → 부품번호"를 검사 로직의 **InputCode(품번)를 채우는 자리**에 끼우면 됩니다.
그 뒤는 기존 `InspectionWorkflowService.RunInspection(inputCode)` 가 전부 처리합니다.

```
스캔(part_no)  →  InputCode 채움  →  RunInspection(inputCode)
                                       1) DB 에 품번 존재? (IPartRepository.GetByPartNo)
                                       2) 기준 이미지 존재?
                                       3) AI 추론: 기준이미지 vs 현재 프레임 일치?
                                       4) 측정/판정/이력 저장
```

배치 후보:

- **A안. 별도 트리거에서 채우기**: 스캔 결과를 InputCode 에 넣고, 검사 실행은 사람이/로직이 별도로.
  오인식 시 사람이 확인/수정할 여지가 있어 안전.
- **B안. 검사 시작 진입부에서 InputCode 가 비었을 때만 스캔**: 한 동작으로 스캔+검사.
  편하지만 OCR 오인식이 바로 검사로 직행하므로 저품질 처리(아래 5번)를 꼭 같이.

어느 쪽이든 "함수 한 번 불러 part_no 받기"는 동일하고, 결과를 InputCode 에 넣는 위치만 다릅니다.

---

## 5. low_quality / 실패일 때 반환값 처리

`"0"` 같은 sentinel 은 쓰지 않았습니다. 부품번호가 숫자일 수 있어 정상값과 구분이 안 되기 때문입니다.
대신 상태로 구분합니다.

- `Ok` : 부품번호 있음 + 신뢰도 충분 → 그대로 검사 진행
- `LowQuality` : 부품번호는 있는데 신뢰도가 낮음 → **값은 주되** 작업자 확인 권장
- `NotFound` : 스캔은 됐지만 부품번호를 못 뽑음 → 재스캔 또는 수동 입력
- `ScanError` : 용지 없음 / 오프라인 / 서버 미기동 → 메시지(`r.Message`) 표시 후 재시도

문자열만 쓰는 `ScanPartNoOrEmpty()` 는 `Ok` 가 아니면 모두 `""` 를 돌려주므로
`if (string.IsNullOrEmpty(partNo))` 한 줄로 분기할 수 있습니다.

> 참고: Epson 서버는 status 가 `low_quality` 여도 `ocr.part_no` 를 채워 줄 때가 많습니다.
> 그래서 무조건 버리지 말고 "값 + 저품질 플래그"로 다루는 편이 실무에서 유용합니다.

---

## 6. 파일 위치

| 파일 | 옮겨 넣을 위치 | 비고 |
|------|----------------|------|
| `Infrastructure/EpsonScanClient.cs` | `...Infrastructure/Services/` | **핵심.** POST → 부품번호 (동기) |
| `Infrastructure/EpsonScanLabelService.cs` | `...Infrastructure/Services/` | 비동기 버전(선택) |
| `Infrastructure/EpsonScanOptions.cs` | `...Infrastructure/Services/` | 위 비동기 버전 설정 |
| `Infrastructure/EpsonScanServerHost.cs` | `...Infrastructure/Services/` | 서버 실행 헬퍼(선택) |
| `Application/ILabelScanService.cs` | `...Application/Interfaces/` | 비동기 버전 인터페이스(선택) |
| `Application/LabelScanResult.cs` | `...Application/Interfaces/` | 비동기 버전 DTO(선택) |

`System.Text.Json` 은 Infrastructure 에 이미 참조돼 있고, `System.Net.Http` 는 net472 프레임워크 어셈블리입니다.
(없으면 Infrastructure.csproj 에 `<Reference Include="System.Net.Http" />` 추가)

---

## 7. 사전 조건

- Epson Scan API 서버 실행 (실행 시점은 3번에서 결정). 기본 주소 `http://127.0.0.1:8000`.
- 스캐너(EPSON ES-C320W) WIA 드라이버, OCR 엔진(Epson 번들 또는 Tesseract 폴백)이 해당 PC 에 설치.
- 연결 확인: 브라우저로 `http://127.0.0.1:8000/health`, `http://127.0.0.1:8000/scanners`.
- Epson 서버는 x86 이지만 HTTP 호출만 하므로 본체 비트니스와 무관합니다.

## 8. 32비트 Epson 서버 빌드/실행 (먼저 해야 할 일)

> 이 코드들은 모두 별도의 **Epson Scan API (C#)** 프로젝트(`epson_scan_api_cs`)에서 가져온 호출부입니다.
> Epson 서버 자체와 IO Inspector 는 **서로 다른 프로젝트라 따로 빌드**합니다. 한 솔루션으로 합치지 마세요.
> 둘은 빌드 시점이 아니라 **실행 시점에 HTTP 로만** 연결됩니다.

### 비트니스 정리
- Epson 서버: `.NET 8 (net8.0-windows)`, **x86(32비트)**. csproj 에 `<PlatformTarget>x86</PlatformTarget>` 가
  이미 들어 있어 그냥 빌드하면 32비트로 나옵니다. (파이썬판의 32비트 venv 같은 별도 준비 불필요)
- IO Inspector: `net472`, 64비트. Epson 코드를 **참조하지 않고** HTTP 호출만 합니다.
- 그래서 둘의 비트니스가 달라도 문제 없습니다.

### 순서
1. **Epson 서버부터** 빌드/실행해서 스캐너가 잡히는지 확인합니다.
2. 그다음 IO Inspector 에서 `EpsonScanClient` 로 호출합니다.

### 빌드 & 실행 (개발 PC)
```cmd
cd epson_scan_api_cs
dotnet build
dotnet run
:: 또는 빌드 산출물 직접 실행
bin\Debug\net8.0-windows\EpsonScanApi.exe
```
- 확인: 브라우저로 `http://127.0.0.1:8000/swagger`, `/health`, `/scanners`.
- `/scanners` 응답에 스캐너가 보이면 준비 완료.

### 배포 (운영 PC)
```cmd
dotnet publish EpsonScanApi.csproj -c Release -r win-x86 --self-contained false -o publish
```
- `--self-contained false` : 대상 PC 에 **ASP.NET Core 8 런타임(x86)** 설치 필요(용량 작음).
- `--self-contained true` : 런타임 포함(설치 불필요, 용량 큼).
- 산출물 폴더를 원하는 위치에 두고 `EpsonScanApi.exe` 실행. (서버 헬퍼 C 의 `ExecutablePath` 에 이 경로 지정)

### 대상 PC 사전 설치
- 스캐너 **EPSON ES-C320W WIA 드라이버** (Epson Scan 2).
- OCR 엔진: **Epson Scan OCR Component Pro**(ScanSmart 번들). 없으면 **Tesseract 5.x**(+`kor`,`eng`)로 자동 폴백.
- 설정은 `appsettings.json` 또는 환경변수: `ScanOutputDir`(저장폴더), `ScanApiHost`, `ScanApiPort`.

---

## 9. Epson 서버 함수(엔드포인트) 레퍼런스 - 각 작업별 호출/전달/반환

서버 주소 기준 `http://127.0.0.1:8000`. 우리 흐름에서 실제로 쓰는 건 보통 **`POST /scan-to-pdf` 하나**면 됩니다.
나머지는 단계별로 쪼개 쓰고 싶을 때만 사용합니다.

### 가장 많이 쓰는 것: 한 번에 스캔→부품번호
| 작업 | 호출 | 넘기는 값(JSON body) | 돌려받는 값(주요 키) |
|------|------|----------------------|----------------------|
| 스캔+OCR 한 방에 | `POST /scan-to-pdf` | `scan{device_id,dpi,mode,source,fmt}`, `card{dpi,debug}`(선택), `pdf{lang,use_processed,engine}` | `status`, `pdf_path`, `ocr.part_no`, `ocr.quality.{confidence,ok,reason}`, `ocr.text` |

요청 예시:
```jsonc
POST /scan-to-pdf
{
  "scan": { "device_id": null, "dpi": 300, "mode": "gray", "source": "flatbed", "fmt": "png" },
  "card": { "dpi": 300, "debug": false },          // 라벨 자동 크롭(선택)
  "pdf":  { "lang": "kor+eng", "use_processed": true, "engine": "auto" }
}
```
→ `EpsonScanClient.ScanPartNo()` 가 바로 이 요청을 보내고 `ocr.part_no` 를 꺼내 줍니다.
넘길 값을 바꾸려면 `EpsonScanClient.ScanPartNo()` 안의 payload(또는 비동기판 `EpsonScanOptions`)를 수정하세요.

### 상태 확인용 (값 안 넘김)
| 작업 | 호출 | 반환 |
|------|------|------|
| 서버/엔진 상태 | `GET /health` | `status`, `engines`, `output_dir` (서버 헬퍼가 준비 확인에 사용) |
| 연결된 스캐너 목록 | `GET /scanners` | `scanners[]` (`id`, `name`) |
| 사용 가능한 OCR 언어 | `GET /languages` | `languages`, `engines` |

### 단계별로 쪼개 쓸 때 (선택)
| 작업 | 호출 | 넘기는 값 | 반환 |
|------|------|-----------|------|
| 스캔만 | `POST /scan` | `{device_id,dpi,mode,source,fmt}` | job 객체(`id`, `status`, `image_path`) |
| 작업 목록/상세 | `GET /jobs` , `GET /jobs/{id}` | (없음) | job 목록 / 상세 |
| 작업 삭제 | `DELETE /jobs/{id}` | (없음) | `deleted` |
| 라벨 자동 크롭 | `POST /jobs/{id}/extract-card` | `{dpi,debug}` | job(`card_path`) |
| 전처리 | `POST /jobs/{id}/preprocess` | `{grayscale,binarize,threshold,deskew,rotate,...}` | job(`processed_path`) |
| 영역 가리기 | `POST /jobs/{id}/redact` | `{rects:[[x,y,w,h]],fill}` | job(`redacted_path`) |
| 검색가능 PDF 생성 | `POST /jobs/{id}/pdf` | `{lang,use_processed,engine}` | job(`pdf_path`, `ocr.part_no`, `ocr.quality`) |
| OCR 파싱 필드 조회 | `GET /jobs/{id}/fields` | (없음) | `fields`(part_no 포함), `lines`, `text` |
| 결과 파일 다운로드 | `GET /jobs/{id}/download/{kind}` | (없음) | 파일. `kind` = image\|processed\|card\|redacted\|pdf |

단계 쪼개기는 `스캔 → (크롭) → (전처리) → (가리기) → PDF` 순서로 **직전 결과 위에 누적**됩니다.
부품번호만 필요하면 굳이 쪼갤 필요 없이 `POST /scan-to-pdf` 하나로 끝냅니다.

### body 값 의미 (자주 바꾸는 것)
- `mode`: `color` | `gray` | `bw`. 라벨 텍스트 OCR 은 `gray` 권장.
- `source`: `flatbed` | `feeder`(ADF 급지). 장치가 ADF 전용이면 자동으로 급지 강제.
- `fmt`: `bmp` | `png` | `jpeg`.
- `lang`: OCR 언어. 한+영 라벨은 `kor+eng`.
- `engine`: `auto`(권장) | `epson` | `tesseract`.
- `device_id`: `null` 이면 첫 번째 스캐너 자동 선택. 특정 장치는 `/scanners` 의 `id` 사용.

---

## 10. 주의사항 / 자주 막히는 곳 (실제로 겪은 것들)

### (1) "카드 추출 실패: Insufficient memory / OutOfMemoryException"
- 원인: 서버가 32비트라 메모리 한계가 낮은데, 카드 추출(라벨 크롭+기울기보정)이 큰 스캔 이미지를 여러 번 복사하다 한계를 넘음. (Epson DLL이 32비트라 서버를 64비트로 못 바꿈)
- **함정:** 서버는 요청에 `card` 를 **안 보내면 기본값(켜짐)으로 채웁니다.** 그래서 "card 를 뺐는데도" 카드 추출이 돌아감. 끄려면 반드시 `"card": null` 을 **명시적으로** 보내야 함.
- 해결: 부품번호만 필요하면 `"card": null` 로 카드 추출을 끔(엔진이 자체 기울기보정을 함). 카드 추출을 꼭 쓰려면 (a) 스캔 DPI를 낮추거나 흑백으로, 또는 (b) `CardExtractor.cs` 의 `Rgba32` 왕복 복사를 없애 메모리를 줄임.

### (2) .NET 버전은 "같이 컴파일할 때만" 맞추면 됨 (실행은 무관)
- 버전 맞추기는 **소스를 한 프로젝트로 같이 컴파일할 때만** 신경 쓰는 것. 이미 만들어진 exe 를 **실행만** 하는 건 버전과 무관.
- 서버는 `.NET 8` 고정(최신 라이브러리 사용, 4.7.2 다운그레이드 불가). 실무자는 4.7.2 로 IO Inspector 만 개발하고, 서버는 빌드된 exe 를 실행만 함. 둘은 별도 프로세스라 안 부딪힘.
- `EpsonScanServerHost`(net472)가 `.NET 8` 서버 exe 를 `Process.Start` 로 실행 가능. (실행은 버전 무관)
- 대상 PC: **self-contained 배포면 아무것도 설치 안 해도** 됨. 아니면 `.NET 8` 런타임만 한 번 설치(4.7.2 와 충돌 없이 공존).

### (3) 동기 함수를 UI 스레드에서 직접 부르면 멈춤(데드락)
- `EpsonScanClient.ScanPartNo()` 는 동기(blocking)임. WPF UI 스레드에서 바로 부르면 화면이 멈출 수 있음.
- `await ...Async()`(비동기 버전) 또는 `await Task.Run(() => EpsonScanClient.ScanPartNo())` 로 워커 스레드에서 호출.

### (4) part_no 가 이상하게 나옴 (예: `iI-12020`)
- 1 / I(대문자 i) / l(소문자 L) / i 처럼 **닮은 글자를 OCR이 헷갈림.** 추출 코드 버그가 아니라 인식 정확도 문제.
- 응답의 `ocr.text`(전체 텍스트)로 실제 라벨과 대조. 개선: DPI 올리기(예: 400), 라벨을 평평하고 똑바로, 흐리면 `color` 모드.
- 부품번호 형식이 고정이면(예: 항상 숫자로 시작) 보정 규칙을 추가할 수도 있으나, Python 버전과 동작을 맞춰야 함.

### (5) [수정 완료] PDF 생성 시 "Pdf indirect object belongs to other PDF document"
- 원인: `PdfFont` 를 static 으로 캐싱해 여러 PDF 문서에 재사용. iText7 에서 `PdfFont` 는 만들어진 문서에 묶여서 재사용하면 터짐.
- 수정: 문서와 무관한 `FontProgram` 만 캐싱하고 `PdfFont` 는 문서마다 새로 생성. (`OcrEpson.cs` 의 `LoadKoreanFont`, 이미 반영됨)

---

## 부록: 서버가 돌려주는 응답에서 읽는 값
- `status` : `done` | `low_quality` | `error`
- `ocr.part_no` (없으면 `ocr.fields.part_no`) : 부품번호
- `ocr.part_no_sub` : 괄호 보조번호(있을 때)
- `ocr.quality.confidence` (0~1), `ocr.quality.ok`, `ocr.quality.reason` : 품질/신뢰도
- `pdf_path` : 생성된 검색가능 PDF(이력 보관용)
- `ocr.text` : OCR 전체 텍스트(수동 확인용)
