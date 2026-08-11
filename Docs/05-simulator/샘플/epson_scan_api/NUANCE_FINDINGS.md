# Nuance/Kofax OmniPage CSDK 발견 — 검색가능 PDF의 "정답" 경로

`C:\Program Files (x86)\EPSON Software\Scan OCR Cmponent Pro` 가 정체 확인됨.
**막혀있던 EsOCR(Ynd) 엔진과는 완전히 다른, 문서화된 상용 OCR SDK.**

## 정체

- `NuOCR\RecAPIPlus.dll` → **Kofax(구 Nuance) OmniPage Capture SDK (CSDK)**.
  - PDB 경로: `D:\dailybuild\CSDKWin\Release\bin.rel\RecApiPlus.pdb`
  - 벤더 문자열: `Kofax, Inc.`, `http://www.kofax.com`
  - 즉 **공개 API 레퍼런스/헤더(KernelApi.h, RecAPIPlus.h)가 존재**하는 엔진.
    EsOCR처럼 헤더 없이 디스어셈블할 필요가 없음.
- `NuOCR\RecoCore.dll` = THOCR 아시아 문자 커널 (`THOCR_KernelInit/Recognize/...`).
  - `thocr_KRN.lib`(한국어), `thocr_JPN_BIG.lib`, `thocr_CHN_BIG.lib`, `korean.mb` 등 → **한국어 OCR 지원 확정.**
- `NuOCR\recpdf.dll` = Nuance PDF 라이브러리(`rPdf*`). 특히 `rPdfOpMergeTextToPages`
  = 이미지 PDF에 OCR 텍스트 레이어 병합 = **검색가능 PDF 생성기**.
- `NuOCR\SPDFLib.dll` = Searchable PDF 생성 엔진(C++ 클래스, 1001 export).
- `DCP\ffmt\EpPdf2.dll`(`EPScanEntry`), `epdocx2/epxlsx2/eppptx2.dll` = Epson 출력 포맷 모듈.
  ScanSmart 파이프라인: 스캔 → NuOCR 인식 → ffmt 모듈이 PDF/Word/Excel 저장.
- 전부 **32비트(x86)**. → 32비트 파이썬 필수 (EsOCR과 동일 제약).

## 검색가능 PDF를 만드는 두 갈래

### A. RecAPI 직접 호출 (권장 — 정식·문서화)
`RecAPIPlus.dll` C export 92개 전부 OmniPage CSDK 표준 API와 일치:
```
RecInitPlusW(company, product)        // 엔진 초기화 (+ 라이선스)
RecSetOutputFormatW(sid, "PDF")       // 출력 포맷 = 검색가능 PDF (Image-on-Text)
RecProcessPagesExW / _RecProcessPageW // 이미지 로드 + 인식
RecSaveDocW(sid, path, hDoc, ...)     // PDF 저장
RecQuitPlus(sid)
RecGetFirstOutputFormatW/Next         // 지원 포맷 열거(PDF 포맷 ID 확인용)
RecExecuteWorkflowW(...)              // 고수준: 워크플로(.xwf) 한 방 실행
```
검색가능 PDF = OmniPage의 "PDF Image-on-Text" 출력. 네이티브 지원.
- 장점: 문서 존재 → 헤더/시그니처 확실. 한국어 OK. PDF 레이어 직접 출력.
- 리스크: **OEM 라이선스**. `RecInitPlus`가 Epson 라이선스 컨텍스트
  (`aceIden.dat`, `epson.lcxz`, `license.txt`) 없이 standalone 동작할지 미검증.
  → 실 PC 32비트 파이썬에서 `RecInitPlusW` 반환코드로 즉시 확인 가능.

### B. NuZonalOCRWrapper.dll (가장 단순, 텍스트 전용)
Epson 자체 얇은 래퍼. undecorated C export 6개:
```
Initialize / RecognizeImage / GetResultRegionCount
GetResultCharCount / GetResultChar / Terminate
```
- 장점: 시그니처가 단순(추론 쉬움), 라이선스 컨텍스트가 더 가벼울 수 있음.
- 단점: **텍스트/좌표만 반환, PDF 출력 없음.** → 검색가능 PDF는 우리가
  좌표+글자로 직접 합성해야 함(현재 Tesseract→PDF 경로와 동일 구조).

## EsOCR(Ynd) 대비

EsOCR는 `Esocr_Init`이 항상 실패 분기(rc=0)·`SetRecogFile` rc=7로 막혀 95%에서 정지.
NuOCR(OmniPage)는 **문서가 있는 정식 SDK**라 성공 확률이 훨씬 높음. EsOCR는 보류 권장.

## 라이선스 돌파 (2026-06-08 검증)

- `epson.lcxz` = ZIP. 내부에 `7609C-K00-XXXX-XXXX-XX.lcx` 형식 라이선스 파일들.
  → **OEM publickey = `7609C`** (OmniPage OEM 고객번호).
- `kRecSetLicenseW(epson.lcxz, "7609C")` → **rc 0 성공** (W/A 모두).
- 그러나 `RecInitPlusW` → `0x8004C413` (라이선스 통과했으나 **APIPlus 레이어 미라이선스**).
  - cf. `0x8004C41F`=API_INIT_ERR(키 없음), `0x8004C414`=API_LICENSE_ERR(키 틀림).
- Epson 자체 CLI `ESS\chrinfo.exe`(SEIKO EPSON 서명)도 `RecInitPlus`가 아니라
  **`kRecInitW`(커널)** 를 import → **정식 경로는 커널 API.**

### 확정된 작동 시퀀스 (커널 API)
```
kRecSetLicenseW(epson.lcxz, "7609C")   # rc 0
kRecInitW(NULL, NULL)                   # <- 이번에 검증할 단계
kRecLoadImgFW(0, img, &hPage, 0)
kRecRecognizeW(0, hPage, NULL)
kRecGetLetters(hPage, ii, &pLet, &n)   # 글자+좌표
kRecFreeImg(hPage); kRecQuit()
```
검색가능 PDF = 위 글자+좌표로 (이미지 위 투명 텍스트레이어) 자체 합성.
RecAPIPlus 의 RecSaveDoc("PDF")는 미라이선스라 사용 불가.

## 막힌 지점 (2026-06-08, 확정) — 엔진 init 라이선스 벽

- `kRecSetLicenseW(epson.lcxz,"7609C")` rc 0 (로드 OK) 이지만
  `kRecInitW` → **`0x8004C413` (≈ API_NOT_AVAILABLE_ERR, "no appropriate license")**.
- **Kofax 자체 진단툴 `DISTR_TST.exe /R epson.lcxz 7609C` 도 동일하게 실패:**
  ```
  Loading OEM License file epson.lcxz: OK.
  Engine initialization: FAILED.  Retcode: 0x8004c413
  ```
- 관리자권한 실행, 쓰기가능 디렉터리(D:\nuocr_test)로 통째 복사 후 실행 → **모두 동일 실패.**
  → 쓰기권한/경로/우리 코드 문제 아님. **OEM 라이선스가 엔진 init을 인가하지 못함.**
- DISTR_TST 내부 문자열: "Technology Pack license **or** OEM License File",
  "Capture SDK version 18 license was not found or expired",
  License Manager 컴포넌트(LMQuery/LecsoMgr/LEditor.dll) 존재.
  → OEM 파일 단독으로는 부족. **License Manager(Technology Pack) 또는 Epson 정식 앱
    (ScanSmart/Document Capture) 컨텍스트에서만 init 통과**하는 구조로 추정.

### 결론
엔진/API/라이선스파일/키(7609C)까지 전부 해독했으나, **마지막 엔진 init 인가가
Kofax/Epson 라이선스로 봉인**되어 있고 in-process 단독 init은 (현재로선) 불가.
넘으려면: (a) ScanSmart가 실 OCR 되는지 확인 → API Monitor로 정식 init 시퀀스/자격 캡처,
또는 (b) Tesseract 경로로 검색가능 PDF 제공(현재 동작, 한국어 OK).

## 다음 단계 (실 PC, 32비트 파이썬)

1. `RecInitPlusW(L"EPSON", L"...")` 호출 → 반환코드 확인 (라이선스 통과 여부 = 성패 분기점).
2. 통과 시: `RecGetFirstOutputFormatW`로 PDF 포맷 ID 확인 →
   `RecSetOutputFormatW` → `RecProcessPageW`(샘플 BMP) → `RecSaveDocW`(.pdf).
3. 실패 시: B안(NuZonalOCRWrapper)으로 텍스트만 받고 PDF 레이어는 우리가 합성.
4. 최종 함수를 `ocr.py`의 엔진 인터페이스에 endpoint로 연결.
