# Epson Scan API

EPSON ES-C320W 스캐너 → 검색가능 PDF 파이프라인 (FastAPI).

스캔(수동) → 작업 목록 → (선택)라벨 추출 / 전처리 / 영역 덮기 → 검색가능 PDF 저장.

- 스캔: **WIA**(pywin32) — ES-C320W가 WIA로 인식됨
- OCR/PDF (기본): **Epson 번들 OmniPage 엔진**(Kofax/Nuance Capture SDK) — ScanSmart와 동일 엔진. 한국어.
- OCR/PDF (폴백): **Tesseract 5.x** — Epson 엔진 사용 불가 시 자동 폴백
- 라벨 추출: 자동 deskew(기울기 보정) + 라벨 영역 크롭 (기본 **PIL 인프로세스**, 추가 설치 불필요)
- 전처리: 그레이스케일·이진화·디스큐·대비·리사이즈·회전·노이즈 제거
- 영역 덮기(redaction): 특정 영역을 박스로 가림 (DPI/해상도 보존 → OCR 품질 영향 없음)
- OCR 결과: 검색가능 PDF + 줄/단어 단위 파싱 필드(bbox 포함) + 부품번호(part_no) + 품질 점수
- 자동 방향 보정 + 품질 평가: 거꾸로/흐림/엉뚱한 스캔이면 `status=low_quality`로 재스캔 유도

## 필요 환경 / 사전 준비

| 구분 | 요구사항 | 비고 |
|------|----------|------|
| OS | **Windows 10 / 11** | WIA·Epson 엔진 모두 Windows 전용 |
| Python | **32비트(x86) 3.9+** | Epson 엔진 DLL이 32비트라 필수. 64비트는 Tesseract 폴백만 가능 |
| 파이썬 패키지 | `pip install -r requirements.txt` | fastapi·uvicorn·pillow·pytesseract·pypdf·reportlab·pywin32 |
| 스캐너 드라이버 | **EPSON ES-C320W WIA 드라이버** | 아래 "스캐너 드라이버" 참고 |
| Epson OCR 엔진 | **Epson Scan OCR Component Pro** (ScanSmart 번들) | 아래 "Epson OCR 엔진" 참고 |
| (폴백) OCR | **Tesseract 5.x** + `kor`/`eng` 데이터 | 선택. Epson 엔진 불가 시 자동 폴백 |
| (선택) 원근보정 | **OpenCV (64비트 별도 venv)** | `requirements-card.txt`. 평판 스캔이면 불필요 |

### 스캐너 드라이버 (WIA)

스캔은 **WIA(Windows Image Acquisition)** 로 합니다 (`scanner_wia.py`, pywin32 COM).

- **EPSON ES-C320W** 용 드라이버를 설치하면 WIA 스캐너로 등록됩니다.
  - **Epson Scan 2** (스캐너 드라이버) — Epson 공식 다운로드. WIA 인터페이스 제공.
  - 또는 OS 표준 **WIA 드라이버** (Windows Update / 제조사 패키지).
- 설치 후 `GET /scanners` 로 인식 여부를 확인하세요 (Type==1 스캐너만 표시).
- 스캔은 수동/온디맨드: 용지를 올린 상태에서 호출해야 하며, 용지가 없으면 드라이버가
  에러(예: `0x80210003` ADF empty)를 반환하고 API가 그대로 정리해 전달합니다.
- ADF(급지)/평판(flatbed)은 `source` 로 선택하며, 장치가 ADF 전용이면 자동으로 급지를 강제합니다.

### Epson OCR 엔진 (기본 OCR/PDF)

기본 OCR/PDF는 Epson 번들 **OmniPage(Kofax/Nuance) CSDK** 를 직접 호출합니다 (`ocr_epson.py`).
별도 설치 없이, ScanSmart 설치 PC에 깔려 있는 아래 컴포넌트/파일을 그대로 사용합니다:

- 엔진 DLL: `C:\Program Files (x86)\EPSON Software\Scan OCR Cmponent Pro\NuOCR\KernelAPI.dll`
- 출력 포맷 모듈: `...\Scan OCR Cmponent Pro\DCP\` 및 `DCP\ffmt\`
- 라이선스: `...\NuOCR\epson.lcxz` (+ OEM 코드)

따라서 기본 엔진을 쓰려면 **Epson ScanSmart / Scan OCR Component Pro** 가 설치돼 있어야 하고,
서버를 **32비트 파이썬**으로 실행해야 합니다. 설치/경로가 없거나 64비트로 실행하면
엔진이 자동 비활성화되고 Tesseract로 폴백합니다 (`GET /health`·`GET /languages`로 상태 확인).

### Tesseract (폴백 OCR)

Epson 엔진을 못 쓰는 환경(64비트 실행, Epson 미설치 등)에서 검색가능 PDF를 만들려면:

- **Tesseract 5.x** (UB Mannheim 빌드 권장) 설치.
- `tessdata`에 `kor.traineddata`, `eng.traineddata` 배치.
- 설치 경로는 자동 탐색합니다 (`C:\Program Files\Tesseract-OCR\...` 등). 커스텀 경로는
  `TESSDATA_PREFIX` 환경변수로 지정 가능.

## ⚠️ 32비트 파이썬 필수 (Epson 엔진)

Epson OmniPage 엔진 DLL은 **32비트(x86)** 라서, 서버를 **32비트 파이썬**에서 실행해야
기본 엔진이 동작합니다 (`venv32`). 64비트로 실행하면 Epson 엔진은 자동 비활성화되고
Tesseract로 폴백합니다.

엔진은 ScanSmart의 정식 시퀀스를 그대로 재현합니다(리버싱으로 확정):
라이선스(`epson.lcxz` + OEM 코드) → `kRecInit` → 언어=한국어 → 엔진 전처리(`kRecPreprocessImg`)
→ 인식 → 글자+좌표로 투명 텍스트레이어 PDF 합성. 상세: `NUANCE_FINDINGS.md`.

## 설치

```bash
# 32비트 파이썬 가상환경(venv32)에서:
pip install -r requirements.txt
```

- (폴백용) **Tesseract 5.x**(UB Mannheim) + Korean 데이터 — 선택.
- (스캔용) Windows + pywin32. 스캐너 켜고 연결.

## 실행

```bash
python main.py          # 32비트 파이썬으로
# Swagger UI: http://localhost:8000/docs
# 엔진 상태 확인: GET /health  또는 GET /languages
```

### 환경변수

| 변수 | 기본값 | 설명 |
|------|--------|------|
| `SCAN_OUTPUT_DIR` | `D:\epson_scans` | 스캔/PDF 결과 저장 폴더 |
| `SCAN_API_HOST` | `127.0.0.1` | 바인드 주소. 다른 PC 접속 허용은 `0.0.0.0` |
| `SCAN_API_PORT` | `8000` | 서버 포트 |
| `CARD_EXTRACT_PY` | (없음) | OpenCV 라벨추출용 64비트 파이썬 경로. 미설정 시 PIL 인프로세스 사용 |
| `TESSDATA_PREFIX` | (자동탐색) | Tesseract `tessdata` 경로 직접 지정 |

## API 흐름

```
GET    /scanners                  # 연결된 스캐너 확인
GET    /languages                 # 사용 가능한 OCR 언어 + 엔진 상태
GET    /health                    # 엔진(Epson/Tesseract) 상태 + 출력 경로
POST   /scan                      # 종이 올리고 호출 -> 스캔, 작업 생성
GET    /jobs                      # 작업(스캔) 목록
GET    /jobs/{id}                 # 작업 상세
DELETE /jobs/{id}                 # 작업 삭제
POST   /jobs/{id}/extract-card    # (선택) 라벨 자동 검출+deskew+크롭
POST   /jobs/{id}/preprocess      # (선택) 전처리
POST   /jobs/{id}/redact          # (선택) 특정 영역 덮기  {"rects":[[x,y,w,h]], "fill":"white"}
POST   /jobs/{id}/pdf             # 검색가능 PDF 생성  {"lang":"kor","engine":"auto"}
GET    /jobs/{id}/fields          # OCR 파싱 필드(줄/단어 + bbox, part_no)
GET    /jobs/{id}/download/{kind} # kind: image|processed|card|redacted|pdf
```

가공 단계(extract-card / preprocess / redact)는 **선형 누적**입니다. 각 단계는 직전 결과 위에
적용되고, 마지막 결과로 OCR을 수행합니다.

한 번에:
```jsonc
POST /scan-to-pdf
{
  "scan":    {"mode":"gray","dpi":300,"source":"flatbed"},
  "card":    {"dpi":300},                                    // 기본 ON (라벨 deskew+크롭). 끄려면 "card":null
  "redact":  {"rects":[[100,200,300,60]], "fill":"white"},   // 선택
  "pdf":     {"lang":"kor", "engine":"auto"}                 // auto=Epson 우선
}
```

- 실행 순서: 스캔 → (카드 추출) → (전처리) → (덮기) → OCR/PDF.
- `engine`: `auto`(Epson→실패시 Tesseract) | `epson` | `tesseract`

## 라벨 추출 (extract-card)

평판 스캔본은 원근 왜곡이 거의 없고 '회전(skew)'만 있으므로, 기본적으로 **PIL 인프로세스**
(`card_pil.py`)로 자동 deskew + 라벨 영역 크롭을 수행합니다. 추가 설치나 별도 프로세스가
필요 없고, 32비트 OCR 서버 안에서 그대로 동작합니다.

원근 보정(perspective warp)까지 필요하면 **OpenCV(64비트)** 경로를 별도로 쓸 수 있습니다:

```bash
# 64비트 별도 환경에 OpenCV 설치
py -3-64 -m venv venv_card
venv_card\Scripts\pip install -r requirements-card.txt
# 그 파이썬 경로를 환경변수로 지정 (있으면 우선 사용, 실패 시 PIL로 자동 폴백)
set CARD_EXTRACT_PY=D:\...\venv_card\Scripts\python.exe
```

## OCR 결과 / 필드

PDF 생성(`/jobs/{id}/pdf`, `/scan-to-pdf`) 응답과 `/jobs/{id}/fields`에서 파싱 결과를 제공합니다.

- `text` — 줄바꿈 포함 전체 텍스트
- `lines` — `[{line, text, words:[{text, bbox:[x,y,w,h]}]}]`
- `fields` — `{"line_1_1":"...", ...}` (줄R 단어C) + `part_no`, `part_no_sub`
- `quality` — `{letters, valid_ratio, confidence, ok, reason}`
  - `ok=false`면 작업 `status=low_quality` (거꾸로/흐림/엉뚱한 스캔 의심 → 재스캔 유도)
  - Epson 엔진은 OCR 시 0/90/180/270 중 가장 잘 읽힌 방향을 자동 선택합니다.

## 영역 덮기 시 OCR 품질 유지 (중요)

자르기/덮기 단계에서 인식률이 떨어지지 않게:
- **해상도(픽셀)를 줄이지 마세요** — 다운스케일하면 글자가 뭉개짐.
- **DPI를 유지**하세요 — 윈도우 기본 자르기 툴은 DPI를 96으로 떨굼 → 인식 저하.
  (이 API의 `/redact`와 엔진 입력 정규화는 DPI를 보존/300 보정합니다.)
- **JPEG 저장 금지** — 압축 잡티가 OCR을 깎음. PNG/BMP 사용.

## 파일

- `main.py` — FastAPI 엔드포인트
- `scanner_wia.py` — WIA 스캐너 제어
- `card_pil.py` — 라벨 자동 추출 (deskew + 크롭). PIL 전용, 추가 설치 불필요 (기본 경로)
- `card_extract.py` — 라벨 추출 OpenCV 버전 (원근 보정까지, 64비트 별도 환경)
- `preprocess.py` — 이미지 전처리
- `redact.py` — 영역 덮기(DPI/해상도 보존)
- `ocr_engine.py` — 엔진 디스패처 (Epson 우선 → Tesseract 폴백)
- `ocr_epson.py` — Epson OmniPage 엔진 래퍼 (검색가능 PDF + 필드 파싱 + 품질/방향보정)
- `ocr_pdf.py` — Tesseract 검색가능 PDF (폴백)
- `jobs.py` — 인메모리 작업 레지스트리 (+JSON 영속화)
- `epson_nuocr.py` — Epson 엔진 단독 테스트 CLI (`python epson_nuocr.py <img> <out.pdf>`)
- `make_searchable_pdf.py` — Tesseract 단독 CLI (테스트용)
- `zone_dump.py` / `letter_dump.py` — Epson 엔진 인식 결과 디버그 덤프 (개발용)
- `requirements.txt` — OCR 서버 의존성 (32비트 venv)
- `requirements-card.txt` — OpenCV 라벨 추출 전용 의존성 (64비트 venv_card)
- `NUANCE_FINDINGS.md` — Epson 엔진 리버싱/통합 기록
- `_obsolete/` — 옛 EsOCR 진단·폐기 스크립트 (안 쓰면 폴더째 삭제 가능)
