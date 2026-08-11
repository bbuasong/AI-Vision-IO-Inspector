# Scanner OCR Sample

## 목적

EPSON ES-C320W로 스캔한 용지 이미지에서 `검수` 글자 옆 최상단 코드 예: `31S7-12020`를 OCR로 읽어 API/엔진별 결과 그리드에 추가하는 WPF .NET Framework 4.7.2 MVVM 샘플입니다.

기존 바코드 디코딩 샘플과 목적이 다릅니다. 이 샘플은 바코드 값을 읽는 것이 아니라, 스캔 이미지 안의 인쇄된 텍스트 코드를 읽습니다.

## 기본 동작

```text
Start Scan
  -> WIA DeviceManager에서 EPSON ES-C320W 자동 검색
  -> 자동 감지 / 회색조 / 300 dpi / PNG 조건 적용 시도
  -> Raw PNG 스캔 이미지 저장
  -> 흰 페이지 스캔이면 어두운 글자/바코드 영역 기준으로 라벨 crop
  -> 어두운 배경 사진이면 밝은 라벨 영역 기준으로 라벨 crop
  -> 0/90/180/270도 후보 이미지 OCR
  -> Sdcb.PaddleOCR / Windows 내장 OCR / C# Epson API별 OCR 실행
     - Sdcb.PaddleOCR, Windows 내장: Enhanced -> PartNo -> Label -> Raw 순서
     - C# Epson API: Raw -> Label -> PartNo -> Enhanced 순서
  -> 검수 상단 코드 형식 추출
  -> 글자가 정방향인 최종 PNG 저장
  -> 결과 그리드 추가
```

현재 표시되는 OCR 채널은 3개입니다.

| 표시명 | 구현 위치 | 호출 방식 |
| --- | --- | --- |
| Sdcb.PaddleOCR | `Services/Ocr/Paddle` | Scanner 프로세스 내부 직접 실행 |
| Windows 내장 | `Services/Ocr/WindowsBuiltIn` | Scanner 프로세스 내부 직접 실행 |
| C# Epson API | `../epson_scan_api_cs` | HTTP `http://127.0.0.1:8001/ocr-image` |

OCR 추출 규칙은 `31S7-12020`처럼 왼쪽 코드에 문자와 숫자가 함께 있고, 오른쪽 값이 숫자 중심인 형식을 우선합니다.
`AOU-LSLT`처럼 OCR이 주변 글자나 노이즈를 영문-영문 코드처럼 잘못 읽은 값은 유효 코드로 인정하지 않습니다.
실제 스캔에서 `31 S7-12020`처럼 왼쪽 코드 내부에 공백이 들어오면 `31S7-12020`으로 정규화합니다.

## 스캔 설정

기본값은 `Services/Scanning/ScanSettings.cs`에서 관리합니다.

| 항목 | 기본값 |
| --- | --- |
| 대상 장치 | `EPSON ES-C320W` |
| 페이지 크기 | Auto Detect |
| 스캔 모드 | Grayscale |
| 해상도 | 300 dpi |
| 저장 형식 | PNG |

WIA 드라이버가 특정 속성 변경을 허용하지 않으면 해당 속성은 드라이버 기본값으로 동작할 수 있습니다.

## 출력 폴더

- Raw 스캔 이미지: `실행폴더\Scans\Raw`
- 라벨/품번/확대 전처리 이미지: `실행폴더\Scans\Processed`
- OCR 엔진별 실제 입력 이미지: `실행폴더\Scans\ApiInput\<엔진>\<단계>`
- OCR 임시 후보 이미지: `실행폴더\Scans\Temp`

## 테스트 기능

스캐너가 연결되지 않은 상태에서도 `Read Image File` 버튼으로 기존 이미지 파일을 선택해 OCR 흐름만 검증할 수 있습니다.

제공된 `바코드.jpg` 테스트 이미지에서는 `31S7-12020` 코드 추출을 확인했습니다.

## 빌드

```powershell
dotnet build .\ScannerSample.sln
```
