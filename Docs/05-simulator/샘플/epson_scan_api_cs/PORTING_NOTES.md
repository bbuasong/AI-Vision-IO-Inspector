# C# 포팅 주의사항 / 우려사항 (Python `epson_scan_api` → C# `epson_scan_api_cs`)

Python 버전을 기준(reference)으로 C#으로 컨버전하면서 발견·수정한 항목과, 앞으로 검증·주의가 필요한 사항을 정리한다.
**원칙: Python이 정답. C#은 Python과 동일한 로직·결과를 내는 것을 목표로 한다.**

---

## 0. 환경/검증 한계

- 개발 보조 환경(Linux 샌드박스)에서는 **`dotnet` 빌드와 Windows 전용 네이티브 OmniPage DLL(`KernelAPI.dll`) 실행이 불가**하다.
  따라서 C# 변경은 로직 차원에서 Python과 대조했고, **실제 빌드·OCR 인식 결과는 Windows 로컬에서 반드시 확인**해야 한다.
- 검증 체크리스트
  - [ ] `dotnet build` 성공
  - [ ] 정상 요청으로 `scan-to-pdf` 동작 (그레이/카드크롭/PDF)
  - [ ] `preprocess: null`로 Epson 엔진 인식 품질 확인
  - [ ] `part_no` / `part_no_sub` 값이 Python과 동일하게 추출되는지 확인

---

## 1. 전처리 (`Services/Preprocessor.cs`)

Python `preprocess.py`와 픽셀 단위로 맞추기 위해 수정한 항목. 라이브러리(PIL ↔ ImageSharp) 관례 차이가 핵심.

- **회전 방향**: PIL `rotate(양수)`=반시계, ImageSharp `Rotate(양수)`=시계. 기존 C#이 부호를 뒤집어(`Rotate(-rotate)`) 오히려 반대로 돌았음.
  → `Rotate(rotate)`로 수정. 노출되는 모서리는 흰색(`BackgroundColor(White)`), 보간은 PIL 기본값과 맞춰 `NearestNeighbor`.
  - 주의: 90/180/270은 방향 영향 없음(180은 부호 무관). 임의 각도에서만 차이.
- **그레이스케일 계수**: PIL `convert("L")`=BT.601, ImageSharp `Grayscale()` 기본=BT.709.
  → `Grayscale(GrayscaleMode.Bt601)`로 맞춤. (Otsu 임계값·이진화 결과가 미세하게 달라지던 원인)
- **binarize 후 resize 누락**: 기존 C#은 이진화 분기에서 곧장 `return` 해 `resize_maxdim`을 건너뜀.
  → 이진화 후에도 리사이즈 적용. PIL은 1-bit 이미지를 리사이즈할 때 강제로 NEAREST를 쓰므로 `NearestNeighbor` 사용.
- **autocontrast**: 기존 C#은 평균 1채널 + 근사 누적 방식이라 PIL과 달랐음.
  → PIL `ImageOps.autocontrast(cutoff=1)`를 정확히 포팅(채널별 히스토그램, 양끝 cutoff 제거, 첫/마지막 비어있지 않은 bin, `int(i*scale+offset)` LUT).
- **deskew**: 각도 탐색·적용을 같은 라이브러리 안에서 하므로 방향이 자체 일관 → 수정 불필요(결과 동일).

### ⚠️ 우려사항
- ImageSharp의 해상도 단위(`ResolutionUnits`)와 DPI 메타데이터 처리는 PNG/BMP 저장 시 px/inch ↔ px/meter 환산이 얽힐 수 있다. **저장된 파일의 DPI가 실제 300으로 태깅되는지** 확인 권장(OCR 정확도에 직결).

---

## 2. 요청 모델 / Swagger 기본 폼 (`Models/Requests.cs`, `Program.cs`)

- **원인**: FastAPI(Python)는 Swagger 예시를 모델 기본값으로 채워 `binarize:"none"` 등 올바른 값이 들어가지만, C#의 Swashbuckle은 타입 플레이스홀더(`"string"`, `0`)로 채운다.
  → `"binarize":"string"` 같은 값이 그대로 제출되면 "none이 아님"으로 판정돼 **Otsu 이진화가 걸려 이미지가 새까맣게** 나왔다. (Python도 같은 JSON이면 동일하게 깨짐 — 계산 버그 아님, 입력 값 문제.)
- **수정**:
  - 각 요청 모델에 `[DefaultValue]`를 달아 Swagger 폼이 올바른 기본값으로 채워지게 함.
  - 각 모델에 `Normalize()` 추가 — 플레이스홀더/오타/범위 밖 값을 문서화된 기본값으로 보정.
    - 핵심: `binarize`가 `none|otsu|fixed`가 아니면 **`none`** 으로 처리(실수로 이진화 방지).
    - `mode/source/fmt/engine/fill` 화이트리스트 검증, `dpi<=0→300`, `threshold` 0~255 클램프, 잘못된 redact rect(`[0]`) 제거.
  - 6개 핸들러(`/scan`, `/preprocess`, `/redact`, `/extract-card`, `/pdf`, `/scan-to-pdf`) 진입부에서 `Normalize()` 호출.

### ⚠️ 우려사항
- 이 `Normalize()`는 **Python에는 없는 방어 로직**이다(Python은 `binarize:"string"`이 오면 그대로 Otsu를 건다). 즉 "잘못된 입력"에 대한 동작은 Python과 다르다.
  - 정상 입력(`none/otsu/fixed`)에서는 양쪽 결과가 동일하므로 실용상 문제 없음. 다만 "엄격한 1:1 로직 동치"를 원한다면 이 보정이 의도된 발산임을 인지할 것.

---

## 3. 핸들러 동작 (`Program.cs`)

모든 POST 엔드포인트를 Python `main.py`와 동일하게 정렬했다.

- **`/scan-to-pdf` 오류 전파**: 기존 C#은 카드/전처리/덮기 단계 오류를 `catch {}`로 **조용히 무시**하고 계속 진행했음. Python은 각 단계 실패 시 예외를 던져 **전체 중단**.
  → C#도 단계별 실패를 `카드 추출 실패`/`전처리 실패`/`영역 덮기 실패` 500 응답으로 전파, 소스 이미지 없으면 409. 카드 단계 `CardLog`도 기록.
- 단계 기본값(card ON, preprocess/redact OFF)·소스 선택(`OcrSourcePath` = Python `_base`: 가공 체인 최신본→없으면 원본)·상태 전이는 Python과 일치.

### ⚠️ 우려사항
- **카드 추출(`CardExtractor`) 엔진 차이**: Python `extract-card`는 OpenCV 외부 스크립트(`CARD_EXTRACT_PY`) 우선 → 실패 시 PIL 폴백 구조다. C#은 인프로세스 단일 경로(자체 엔진)만 있다. 핸들러 로직이 아니라 검출 엔진 구현 차이라 둠. 결과(각도/크롭)가 미세하게 다를 수 있음.

---

## 4. OCR 엔진 (`Services/OcrEpson.cs`, `Services/OcrEngine.cs`)

- C# `OcrEpson`은 Python `ocr_epson.py`의 충실한 포팅임을 줄 단위로 확인(엔진 init·라이선스·**한국어 단독 언어(0x7A)**·letter 파싱·`_is_cjk_junk`·바코드 밴드 필터·줄 좌표 재정렬·품질평가).
- 디스패처 `OcrEngine`도 Python `ocr_engine.py`와 동일(auto→Epson, 실패 시 Tesseract 폴백).
- **수정한 유일한 로직 차이 — `NormalizeImage`**: 기존 C#은 항상 L8 그레이로 변환. Python `_normalize`는 `if mode not in ("L","RGB")`로 **이미 RGB(24bit)면 컬러 유지**.
  → C#도 `Image.Identify`로 24bit면 RGB 유지, 그 외(RGBA/팔레트/1bit 등)는 L8 변환하도록 맞춤.

### ⚠️ 우려사항 (중요)
- **Epson 엔진에는 전처리(preprocess)를 적용하지 말 것.** Python 코드가 명시한다:
  - `ScanToPdfRequest`: `preprocess = None  # Epson 엔진엔 보통 불필요(이진화 금지)`
  - `_normalize`: `이진화 금지: 엔진이 함` — 엔진이 `kRecPreprocessImg`로 자체 전처리.
  - denoise(미디언)·이진화 등을 미리 걸면 잔글씨 획이 뭉개져 `31S7-12020 → 11-122` 같은 오인식이 발생. **engine=epson/auto일 땐 `preprocess: null` 권장.**
  - (가드를 코드로 강제하지는 않았다 — Python에 없는 발산이므로. 필요하면 추가 가능.)
- **언어 설정 변경 금지**: 활성 언어에 영어(0x00=ALL)를 추가하면 CJK가 오염돼 영문을 한글/한자로 오인식한다. 한국어 단독(0x7A) 유지.
- **바코드 → 한글 오인식**: 엔진이 바코드를 "가늘고 긴 막대"가 아니라 넓은 한글 글자(예: `쌔쌔뻬뻬`)로 토큰화하면 thin-bar 필터(`_thin`/`BarcodeBands`)를 빠져나간다. **Python·C# 동일 동작**(엔진 토큰화에 의존). 필요 시 "바코드 밴드 근처의 순수 한글 덩어리 제거" 같은 추가 필터를 양쪽에 동일 적용해야 함.

---

## 5. 부품번호 추출 `part_no` / `part_no_sub` (`ocr_epson.py`, `OcrEpson.cs`)

- **문제**: 기존 로직은 1번 줄에서 괄호 앞까지 토큰을 **무조건** 이어붙여, 오인식 한글(`미`)·도장 글자(`검수`)도 `part_no`에 포함됐다.
- **수정 (Python·C# 동일 규칙)**: 1번 줄에서 괄호 토큰을 만나면 거기까지를 `part_no_sub`로 두고(변경 없음), 그 앞 토큰 중 **부품번호다운 것만** `part_no`로 결합.
  - 부품번호 토큰 규칙: **영숫자(A–Z, a–z, 0–9) + 하이픈류(`-`,`‐`~`―`)만으로 구성 + 영숫자 최소 1개**.
  - 허용: `31S7-12020`, `ABCD-12`, `AB1234`, `ABCD`, `12345`(숫자/영문/조합 모두).
  - 제외: 한글(`미`,`검수`), 점 든 토큰(`s.p.a`), 순수 기호/하이픈(`---`), 괄호 토큰.

### ⚠️ 우려사항
- "영숫자 최소 1개"로 완화했기 때문에 **숫자 없는 영문 토큰도 허용**된다.
  → 만약 1번 줄에 영문 단어(예: `RH`)가 잡음으로 끼면 `part_no`에 포함될 수 있다. (보통 영문 단어는 2번 줄 이하라 실제 위험은 낮음.)
- **부품번호 형식이 더 정해져 있으면 규칙을 좁힐 수 있다.** 예:
  - 항상 하이픈 포함 → "하이픈 1개 이상" 조건 추가
  - 항상 N자리 / 특정 패턴 → 정규식으로 고정
  - 형식 확정 시 Python·C# **양쪽 동일하게** 반영할 것.
- 1번 줄 자체가 잡음만일 경우 `part_no`는 빈 문자열이 된다(잘못된 값을 넣느니 비움).

---

## 6. 파일별 변경 요약

| 파일 | 변경 |
|---|---|
| `Services/Preprocessor.cs` | 회전 부호/흰배경/NEAREST, BT.601 그레이, binarize 후 resize, autocontrast PIL 포팅 |
| `Models/Requests.cs` | `[DefaultValue]`, `Normalize()` 추가 |
| `Program.cs` | 핸들러에 `Normalize()` 호출, `/scan-to-pdf` 오류 전파·CardLog |
| `Services/OcrEpson.cs` | `NormalizeImage` RGB 유지, `part_no` 토큰 필터(`LooksLikePartNo`) |
| `../epson_scan_api/ocr_epson.py` | `part_no` 토큰 필터(`_looks_like_partno`) — C#과 동일 규칙으로 함께 수정 |
