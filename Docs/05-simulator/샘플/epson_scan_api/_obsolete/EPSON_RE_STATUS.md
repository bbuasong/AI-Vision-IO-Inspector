# Epson EsOCR 검색가능 PDF — 리버싱 현황 (백로그용)

순수 Epson DLL로 검색가능 PDF를 만들려던 시도의 전체 분석 기록.
약 95% 도달했으나 마지막 런타임 초기화 벽에서 멈춤. 디버거로 재개 가능.

## 확정된 사실 (전부 검증됨)

- DLL: `C:\Program Files (x86)\epson\ESCNOCR\EsOCR.dll`, `Ocrsys.dll` — **32비트(x86)**.
  64비트 파이썬에선 로드 불가(WinError 193). **32비트 파이썬 필수.**
- EsOCR.dll export(10개) 전부 `__stdcall`. 시그니처(리버싱):
  - `Esocr_GetVer(WORD* pVer)` → 0, *pVer=0x0100  ✅검증
  - `Esocr_Init(ignored)` → 내부 워커가 ocrsys.ini→레지스트리 읽고 Ydocrd.dll 로드. **항상 rc=0(실패분기)**
  - `Esocr_SetHandle(h)` → 전역 0x101787c4에 저장. **사실상 미사용**(읽는 코드 없음)
  - `Esocr_GetLangInfo(void* out16, int idx)` → init 후 데이터 채움
  - `Esocr_SetPDFInfo(char* out_path, WORD* pXY, WORD w3, WORD w4, WORD quality0_100, _, _, char* a8, char* a9)`
    → arg1=출력경로(ANSI, 필수 non-NULL, +0x8da에 복사), pXY=2 WORD 포인터(NULL이면 크래시), quality. ✅rc=0 성공
  - `Esocr_SetRecogFile(LPCWSTR input, WORD angle, void* pMode, a4, a5)`
    → **이게 OCR실행+PDF출력 함수**. pMode = ≥4바이트 0버퍼(word[0]=0이어야 인식경로). ✅호출 도달
  - `Esocr_End(ignored)`
- 설정은 **레지스트리**에서 읽음: ocrsys.ini의 `[General]ProfileName` = `Software\EPSON\EPSON Scan\EsOCR\1.0`
  → `HKCU\<ProfileName>\General\ProgramDir` (=ocrlib 경로). **이미 Epson Scan이 채워둠.** ✅
- 인식 워커가 이미지 포맷 DLL(bmp/jpg/png/tif/pcx/dt4 _mem.dll)을 동적 로드해 포맷 판별 →
  실제 OCR → `mem_pdf.dll!WritePDF`로 PDF 기록. 호출 지점까지 전부 도달.
- 에러코드 매핑(0x10001b20): 내부 0x6f~0x74 → **공개 rc=7**. 0x34f0은 이미지상태(104~201)→내부코드.

## 막힌 지점

- `Esocr_Init` 이 항상 rc=0 (성공이면 1/-1 반환하는데 내부 워커 0x6e20이 예외분기 0x1a07로 빠짐).
- 그 결과 `SetRecogFile`이 **모든 입력 이미지에서 일정하게 rc=7**(이미지처리 에러) 반환.
  - 사진(16MP JPG), 실제 스캔 BMP, 그레이/이진/축소 변환본 전부 동일.
- 코어 DLL 25개는 단독 LoadLibrary **전부 성공** → DLL 로드 문제 아님.
- 레지스트리·비트·경로 모두 정상. → **런타임 초기화 상태 문제**로 판단.
  EsOCR.dll이 Epson Scan/ScanSmart 앱에서 추출된 모듈이라, 앱이 제공하는
  실행 컨텍스트(메시지루프/COM/엔진 핸들 등) 없이는 완전 초기화가 안 되는 것으로 추정.

## 재개 방법 (디버거)

1. 32비트 파이썬 + x64dbg(32비트) 또는 WinDbg로 `python epson_ocr_probe.py ...` 실행.
2. `EsOCR.dll` 베이스 + 0x6e20(Init 워커)에 BP. 어느 호출에서 eax≠0(예외/에러)로
   0x1a07 분기를 타는지 추적 → 그게 Init 실패 근본원인.
3. `0x34f0` 호출 시 인자(이미지 status)를 확인 → rc=7의 정확한 내부코드(0x6f~0x74) 식별.
4. 또는 API Monitor로 Epson ScanSmart의 "검색가능 PDF" 실행 중 EsOCR_* 호출 인자를
   캡처해 정답 시퀀스와 비교.

## 도구 (이 폴더)

- `epson_ocr_probe.py` — EsOCR 파이프라인 호출 하니스(현재 v8, 이미지변환 스윕)
- `reg_check.py` — 레지스트리 프로필 점검/생성
- `core_check.py` — 코어 DLL 단독 로드 점검
- `diagnose.py` — 비트/DLL/Tesseract 종합 진단
