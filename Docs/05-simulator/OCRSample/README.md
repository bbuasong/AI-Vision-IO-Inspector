# OCRSample

EPSON ES-C320W를 USB로 직접 스캔하고 Epson OCR로 결과를 표시하는 64비트 WPF(.NET Framework 4.7.2) 샘플입니다.

## 실행 구조

    OCRSample.exe (x64 WPF)
        ├─ USB / WIA로 ES-C320W 직접 스캔
        └─ EpsonScanApi.exe --ocr-file (x86 로컬 OCR 작업자)
               └─ Epson OmniPage KernelAPI.dll

HTTP 포트를 열거나 서버 주소에 연결하지 않습니다. x86 작업자는 스캔 이미지 파일 경로를 인자로 받아 OCR 결과 JSON을 기록한 뒤 종료합니다.

## 준비

1. EPSON ES-C320W WIA 드라이버와 Epson Scan OCR Component Pro를 설치합니다.
2. x86 OCR 작업자 게시본이 다음 위치에 있어야 합니다.

       C:\SVN_LinkGenesis\FA_HDX\AI-Vision IO Inspector\Docs\05-simulator\epson_ocr_api_260721\epson_scan_api_cs\bin\LocalOcrWorker\EpsonScanApi.exe

3. OCRSample을 x64로 빌드한 뒤 실행합니다.

## 사용

1. **USB 스캐너 새로고침**으로 ES-C320W를 확인합니다.
2. 라벨을 ADF에 올립니다.
3. 필요하면 DPI, 색상 모드, OCR 언어를 조정합니다.
4. **스캔 및 OCR 실행**을 누릅니다.

앱은 Scans 폴더에 PNG를 저장하고, 그 이미지를 로컬 x86 OCR 작업자에게 전달합니다. OCR 원문은 화면의 **OCR 원문** TextBox에 표시되고, 부품번호·신뢰도·스캔 이미지 경로도 함께 표시됩니다.

## 실제 검증 결과

ES-C320W에서 300 DPI 회색조로 직접 스캔한 이미지에 대해 Epson OmniPage OCR을 실행했습니다.

- OCR 엔진: Epson OmniPage
- 품질 신뢰도: 98%
- 인식된 예시: FORK ASSY-2400 A, KR0224 주캐스케이드코리아, 2650013577640002 06/30

라벨의 형식이 기존 부품번호 규칙과 맞지 않으면 OCR 원문은 표시되지만 part_no는 비어 있을 수 있습니다. 이 경우 화면의 부품번호 입력란에서 값을 보정할 수 있습니다.
