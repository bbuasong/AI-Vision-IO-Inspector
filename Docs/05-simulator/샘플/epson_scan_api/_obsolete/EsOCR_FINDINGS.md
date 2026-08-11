# Epson OCR DLL 리버싱 결과

기존 `ocr.py`가 OCR을 못 했던 **3가지 실제 원인**과, DLL 디스어셈블로 복원한 시그니처 기록.

## 왜 안 됐나

1. **함수 이름이 전부 가짜.** 코드는 `OcrInit2 / OcrMemInit2 / OcrLoadDic2 / OcrExecuteDoc2 / OcrEnd2`를 호출했는데, `Ocrsys.dll`에 이런 export는 **하나도 없음**. `dll.OcrInit2` 접근 순간 `AttributeError` → `except`에서 조용히 Tesseract로 폴백. "DLL이 안 먹히는" 것처럼 보인 진짜 이유.
2. **DLL이 32비트(x86, machine 0x14c).** 64비트 파이썬에선 `ctypes.WinDLL` 로드 자체가 실패(`WinError 193`). → **32비트 파이썬 필수.**
3. **언어 코드 추정 오류.** `0x0412`(한국어 LCID) 등은 이 엔진과 무관. 실제 사전은 `ocrlib/dic/Ydrec{KO,EN,JA,...}.ptn` 기반 2글자 코드.

## 실제 export

### Ocrsys.dll — `Ynd*` 계열 (96개, 저수준 엔진)
```
YndInit / YndInitLimited / YndCertifyLicense
YndLoadImage / YndSetImage / YndScanImage / YndScanImageADF / YndSelectSource
YndLayoutRecog / YndLayoutRecog2 / YndRecog / YndRecog2 / YndRecogEx
YndGetResult / YndGetResultEx / YndGetResultLine / YndGetResultLineCount
YndSaveResult / YndSaveImage / YndSaveLayout
YndSetEnv / YndGetEnv / YndLoadUsrOcrDic / YndEnd / YndFreeResult ...
```
COM 서버이기도 함(`DllRegisterServer`, `DllGetClassObject` export). `ocrsys.ini`가
레지스트리 프로필 `Software\EPSON\EPSON Scan\EsOCR\1.0`를 가리킴.

### EsOCR.dll — `Esocr_*` 계열 (10개, 고수준 래퍼)
전부 **`__stdcall`**. C++ 전역 객체(`this = 0x101787d8`)를 감싼 얇은 thunk.
인자 개수는 `ret imm16`으로 **확정**, 타입은 디스어셈블 기반(일부 추정).

| export | ret | 인자수 | 복원 시그니처 | 신뢰도 |
|---|---|---|---|---|
| `Esocr_Init` | 4 | 1 | `int Esocr_Init(void* p)` — 멤버 Init로 tail-jmp | 높음(개수) |
| `Esocr_End` | 4 | 1 | `int Esocr_End(void* p)` | 높음(개수) |
| `Esocr_GetVer` | 4 | 1 | `int Esocr_GetVer(WORD* pVer)` — `*pVer=0x0100` 기록 | **높음** |
| `Esocr_SetHandle` | 4 | 1 | `int Esocr_SetHandle(HANDLE h)` — 전역 `0x101787c4`에 저장 | **높음** |
| `Esocr_GetLangInfo` | 8 | 2 | `int Esocr_GetLangInfo(void* out16, int idx)` — out 16바이트 0초기화 후 채움 | 높음 |
| `Esocr_GetRotateAngle` | 12 | 3 | `int (..., ..., ...)` | 개수만 |
| `Esocr_GetRotateAngleW` | 12 | 3 | wide 변형 | 개수만 |
| `Esocr_GetRotateAngleMem` | 16 | 4 | 메모리버퍼 변형 | 개수만 |
| `Esocr_SetRecogFile` | 20 | 5 | `int (a1, WORD angle, LPCWSTR str, a4, a5)` | 중간 |
| `Esocr_SetPDFInfo` | 36 | 9 | `int (a1, WORD* xy, a3, a4, WORD quality0_100, a6, a7, a8, a9)` | 중간 |

#### 디스어셈블에서 확정된 세부
- **`Esocr_SetRecogFile`**: 2번째 인자(WORD)를 `0/90/180/270`으로 정규화 → **회전각**. 3번째 인자는 `*(WORD*)arg==0`로 빈 문자열 검사 → **와이드 문자열(LPCWSTR)**. `a1/a4/a5`(입력 이미지 경로·콜백·플래그 후보)는 **미확정**.
- **`Esocr_SetPDFInfo`**: 5번째 인자(WORD)를 `min(v,100)` 후 `100-v` → **품질/압축률 0~100**. 2번째 인자는 WORD 2개(`[+0],[+2]`)를 읽음 → 해상도 x/y 또는 크기 추정.
- **`Esocr_SetHandle`이 핵심**: EsOCR는 독립 동작이 아니라 외부 핸들을 받아 동작. 그 핸들은 **`Ocrsys.dll`의 OCR 엔진 핸들**일 가능성이 높음. 즉 정상 흐름은:
  ```
  (Ocrsys) Ynd 엔진 초기화 → 핸들 획득
        → Esocr_SetHandle(핸들)
        → Esocr_SetRecogFile(...) / Esocr_SetPDFInfo(...)
        → 인식 실행 → 결과/PDF 저장
        → Esocr_End
  ```
- 주의: EsOCR.dll은 USER32/GDI/OLE/gdiplus 등 GUI 의존이 많음 → 원래 앱에서 분리된 모듈이라 일부 컨텍스트(메시지 루프/COM init)를 기대할 수 있음.

## 다음 단계 (실 PC에서)
1. `python diagnose.py` → 32비트 + EsOCR `loaded=True` 확인.
2. `Esocr_GetVer` 반환값으로 호출규약 정상 동작 확증(스택 깨짐 없는지).
3. `a1/a4/a5` 의미 확정: x64dbg/Ghidra로 멤버 `0x10002150` 추적하거나,
   Epson Scan2 실행 중 API 모니터로 실제 인자 캡처가 가장 빠름.
4. 확정 후 `ocr.py::_run_epson_ocr` 구현(현재 `NotImplementedError`).

그 전까지는 **Tesseract 경로가 실사용 엔진**(`kor` traineddata 필요).
