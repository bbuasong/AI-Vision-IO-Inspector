# Barcode Scanner Sample

## 목적

Epson ES-C320W 또는 외부 스캐너 입력으로 받은 바코드 문자열을 WPF `ListBox`에 표시하는 간단한 샘플입니다.

## 현재 동작 방식

- `Start Reading` 버튼을 누르면 Windows WIA 장치 목록에서 `EPSON ES-C320W`를 자동으로 찾아 스캔합니다.
- 스캔 이미지는 실행 폴더의 `Scans` 폴더에 PNG 파일로 저장됩니다.
- 저장된 PNG 이미지를 ZXing으로 디코딩하고, 읽은 바코드 값을 ListBox에 추가합니다.
- `Decode Image File` 버튼으로 이미 저장된 JPG/PNG/BMP/TIF 파일도 같은 ZXing 경로로 테스트할 수 있습니다.
- 입력창에 직접 바코드 값을 입력하고 `Enter`를 누르면 수동으로 ListBox에 추가할 수 있습니다.
- `ListBox 초기화` 버튼을 누르면 스캔 목록이 비워집니다.

## 기본 스캔 설정

기본 설정은 `Services/ScanSettings.cs`에서 관리합니다.

| 항목 | 기본값 |
| --- | --- |
| 대상 장치 | `EPSON ES-C320W` |
| 페이지 크기 | Auto Detect |
| 스캔 모드 | Grayscale |
| 해상도 | 300 dpi |
| 저장 형식 | PNG |

WIA 드라이버가 속성 변경을 허용하지 않는 항목은 드라이버 기본값으로 스캔될 수 있습니다.

## 처리 흐름

```text
Start Reading
  -> WIA DeviceManager에서 EPSON ES-C320W 자동 검색
  -> 300 dpi / 회색조 설정 적용 시도
  -> PNG 스캔
  -> Scans 폴더에 파일 저장
  -> ZXing 디코딩
  -> ListBox 추가
```

첨부 테스트 이미지 `바코드.jpg`는 ZXing 검증에서 `2650006854240001`로 디코딩되었습니다.

## 빌드

```powershell
dotnet build .\BarcodeScannerSample.sln
```
