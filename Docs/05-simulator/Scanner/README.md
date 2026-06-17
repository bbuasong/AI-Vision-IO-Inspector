# Scanner OCR Sample

## 목적

EPSON ES-C320W로 스캔한 용지 이미지에서 `검수` 글자 옆 최상단 코드 예: `31S7-12020`를 OCR로 읽어 `ListBox`에 추가하는 WPF .NET 9.0 MVVM 샘플입니다.

기존 바코드 디코딩 샘플과 목적이 다릅니다. 이 샘플은 바코드 값을 읽는 것이 아니라, 스캔 이미지 안의 인쇄된 텍스트 코드를 읽습니다.

## 기본 동작

```text
Start Scan
  -> WIA DeviceManager에서 EPSON ES-C320W 자동 검색
  -> 자동 감지 / 회색조 / 300 dpi / PNG 조건 적용 시도
  -> Raw PNG 스캔 이미지 저장
  -> 0/90/180/270도 후보 이미지 OCR
  -> 검수 상단 코드 형식 추출
  -> 글자가 정방향인 최종 PNG 저장
  -> ListBox 추가
```

## 스캔 설정

기본값은 `Services/ScanSettings.cs`에서 관리합니다.

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
- 정방향 최종 이미지: `실행폴더\Scans`
- OCR 임시 후보 이미지: `실행폴더\Scans\Temp`

## 테스트 기능

스캐너가 연결되지 않은 상태에서도 `Read Image File` 버튼으로 기존 이미지 파일을 선택해 OCR 흐름만 검증할 수 있습니다.

제공된 `바코드.jpg` 테스트 이미지에서는 `31S7-12020` 코드 추출을 확인했습니다.

## 빌드

```powershell
dotnet build .\ScannerSample.sln
```
