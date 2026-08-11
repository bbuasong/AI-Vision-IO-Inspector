# 요청 기능 실현가능성

결론: **요청한 5개 기능 전부 구현 가능.** Epson Ocrsys.dll(32비트) 하나에
스캔·OCR·저장 함수가 모두 들어 있고, 디스어셈블로 호출규약·인자수까지 확인됨.

## 기능별 매핑

| # | 요청 기능 | 구현 방법 | 상태 |
|---|---|---|---|
| 1 | 스캐너 연결/탐지 | WIA(pywin32) 또는 TWAIN으로 장치 열거 → `GET /scanners` | 됨 (scan_diagnose.py로 확인) |
| 2 | 스캔 진행 목록 | **서버가 직접 작업 레지스트리 관리** (큐/스캔중/OCR중/완료) → `GET /jobs` | 됨 (아래 설명) |
| 3 | 스캔 등록 | `POST /scan` → 작업 생성 + WIA/TWAIN/Ynd 스캔 트리거 | 됨 |
| 4 | OCR 진행 | 지금: Tesseract(kor 데이터 필요) / 후속: EsOCR·Ocrsys DLL | 됨 (Tesseract 즉시) |
| 5 | 파일 저장 | 이미지 + 텍스트/PDF 저장. `YndSaveResult`로 PDF/RTF/TXT 직접 출력 가능 | 됨 |

## 중요 포인트

**[2] "스캔 진행 목록"은 스캐너가 주는 게 아님.**
프린터 스풀러와 달리 스캐너에는 폴링 가능한 작업 큐가 없습니다. 따라서
이 기능은 **우리 서버가 만든 스캔 작업을 추적**하는 형태로 구현합니다:

```
POST /scan      -> job 생성 {id, status: "queued", created_at}
                   백그라운드에서 scanning -> ocr -> done 으로 상태 갱신
GET  /jobs       -> 전체 작업 목록 + 상태
GET  /jobs/{id}  -> 개별 작업 상세 + 결과 경로
```

저장소는 초기엔 인메모리 dict, 영속화가 필요하면 SQLite 한 파일로 충분.

## Ocrsys.dll 전체 파이프라인 (디스어셈블 확인, 전부 stdcall)

```
YndInit(1)            엔진 초기화 -> 핸들
YndCertifyLicense(2)  라이선스
YndSelectSource(1)    스캐너(TWAIN 소스) 선택
YndScanImage(2)       스캔 1매  /  YndScanImageADF(3)  ADF 연속
   또는
YndLoadImage(5)       파일에서 이미지 로드
YndLayoutRecog(4)     레이아웃 분석
YndRecog(4)           문자 인식
YndGetResult(2) / YndGetResultLine(6)   결과 텍스트
YndSaveResult(5)      TXT/RTF/PDF 저장
YndEnd(0)             정리
```

즉 Epson DLL만으로 스캔→OCR→검색가능 PDF까지 한 번에 가능.
다만 일부 인자 의미(버퍼 구조체 등)는 실 PC에서 반환값 보며 확정 필요.

## 권장 진행 순서

1. **지금 동작시키기 (Tesseract 경로)**
   - `kor.traineddata` 설치 → 스캔(WIA) + OCR(Tesseract) + 저장. 완전 동작.
2. **스캐너 탐지 확정**
   - `python scan_diagnose.py` → WIA/TWAIN 중 내 스캐너가 잡히는 쪽 확인.
3. **작업 큐(/jobs) 추가** → 기능 [2] 완성.
4. **Epson DLL OCR로 교체(선택)**
   - YndInit/Recog/GetResult 인자 확정 후 Tesseract 대체. 한국어 품질↑, PDF 출력.

## 의존성 (requirements)

- `fastapi`, `uvicorn`, `pillow`  (공통)
- `pywin32`  (WIA 스캔) — 이미 포함
- `pytesseract` + Tesseract 본체 + `kor.traineddata`  (OCR 폴백)
- `twain`  (TWAIN 경로 쓸 경우만; 선택)
