# Barcode Scanner Sample

## 목적

Epson ES-C320W 또는 외부 스캐너 입력으로 받은 바코드 문자열을 WPF `ListBox`에 표시하는 간단한 샘플입니다.

## 현재 동작 방식

- `Start Reading` 버튼을 누르면 바코드 입력창이 활성화되고 포커스가 이동합니다.
- 입력창에 포커스를 둔 상태에서 바코드 값을 입력하고 `Enter`를 누르면 ListBox에 추가됩니다.
- 바코드 스캐너가 키보드 입력처럼 값을 보내는 경우 그대로 테스트할 수 있습니다.
- `ListBox 초기화` 버튼을 누르면 스캔 목록이 비워집니다.

## Epson ES-C320W 관련 주의

ES-C320W는 일반 USB 바코드 리더처럼 텍스트를 바로 보내는 장비가 아니라 문서/이미지 스캐너로 동작할 수 있습니다.
이 경우에는 다음 흐름이 추가로 필요합니다.

```text
Epson Scan 또는 WIA/TWAIN으로 이미지 취득
  -> 이미지에서 바코드 디코딩
  -> MainViewModel.CurrentBarcode에 문자열 전달
  -> Enter 또는 AddBarcodeCommand 실행
```

NuGet 패키지를 사용할 수 있는 경우에는 `ZXing.Net` 같은 바코드 디코더를 붙여 이미지 디코딩 서비스를 추가하면 됩니다.

## 빌드

```powershell
dotnet build .\BarcodeScannerSample.csproj
```
