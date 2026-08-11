# 빌드 & 실행 가이드 (전체)

라벨 스캔 → 부품번호 시스템의 구성요소별 **빌드 / 실행 / 배포** 방법 총정리.
(튜닝값 바꾸는 법은 같은 폴더 `CONFIG.md`, 변경 내역은 `README.md` 참고)

## 구성요소 한눈에

| 이름 | 무엇 | 런타임 | 역할 |
|------|------|--------|------|
| Epson Scan API | `epson_scan_api_cs` 프로젝트 | .NET 8, **x86** | 스캔 + OCR + (선택)PDF. HTTP 서버 |
| RapidOCR 사이드카 | `rapid_sidecar.py` → `rapid_sidecar.exe` | Python(빌드시)/exe | 부품번호 보조 인식. 서버가 자동 기동 |
| EpsonScanTester | `EpsonScanTester` 프로젝트 | .NET 4.7.2 | 동작 확인용 콘솔 테스터 |
| 병합용 샘플 | `SAMPLE_EpsonScanIntegration` | .NET 4.7.2 | IO Inspector 에 넣을 참고 코드 |

**동작 흐름:** IO Inspector(또는 테스터) → HTTP → Epson Scan API(x86) → 스캐너/엡손 OCR + RapidOCR 사이드카 → part_no 반환.

---

## 1. Epson Scan API 서버 (핵심)

### 사전 준비
- 개발 PC: **.NET 8 SDK**
- 스캔 PC: EPSON ES-C320W WIA 드라이버, 엡손 OCR 엔진(또는 Tesseract 폴백)

### 개발 중 실행 (제일 간단)
```
cd epson_scan_api_cs
dotnet run
```
- 브라우저로 `http://127.0.0.1:8000/swagger`, `/health`, `/scanners` 확인.
- 콘솔에 `[Scan API] 출력 디렉토리: ...` 로 실제 설정값 확인 가능.

### 배포용 빌드 (실무자 PC에 줄 것) - self-contained 권장
32비트 exe 라 32비트 .NET 런타임이 필요한데, self-contained 로 만들면 **런타임까지 exe 폴더에 포함**되어 그 PC에 아무것도 설치 안 해도 됨.
```
dotnet publish -c Release -r win-x86 --self-contained true -o publish_x86
```
- 결과: `publish_x86\` 폴더 (안에 `EpsonScanApi.exe` + 런타임 + `appsettings.json`)
- 이 폴더째로 실무자 PC에 복사.

### 주의
- 서버 exe 를 그냥 더블클릭했는데 바로 꺼지면 → 대개 (a) 32비트 런타임 없음(→ self-contained 로), (b) 포트 8000 이미 사용중, (c) `appsettings.json` 의 `ScanOutputDir` 가 없는 드라이브. cmd 에서 실행하면 에러가 보임.
- 설정 변경은 **실행되는 exe 옆 `appsettings.json`** 을 고친다. (CONFIG.md 1번)

---

## 2. RapidOCR 사이드카 (부품번호 보조 인식)

서버가 x86 이라 .NET 인프로세스로 RapidOCR(ONNX)을 못 써서, **별도 프로세스**로 띄워 HTTP로 부품번호를 받는다. 서버(`OcrRapid.cs`)가 앱 폴더의 `rapid_sidecar.exe`(있으면) 또는 번들 파이썬 + `rapid_sidecar.py` 를 **자동 기동**한다.

### 빌드 (개발 PC에서 1회) - `build_sidecar.bat`
`rapid_sidecar.py` 옆에서 실행:
```
build_sidecar.bat
```
이 배치가 하는 일:
1. `pyinstaller`, `rapidocr-onnxruntime` 설치
2. PyInstaller 와 충돌하는 낡은 `pathlib` 백포트 제거 (있으면)
3. `--onefile` 로 `dist\rapid_sidecar.exe` 생성
4. 실패하면 `[BUILD FAILED]` 로 멈춤(엉뚱한 "Done" 방지)
5. `rapid_sidecar.config.json` 을 `dist\` 로 함께 복사

산출물:
- `dist\rapid_sidecar.exe`  ← 실제 쓸 것
- `build\`, `rapid_sidecar.spec`  ← PyInstaller 임시/설정 파일. 무시/삭제 OK
- `dist\rapid_sidecar.config.json`  ← 튜닝 설정

> 빌드 중 "pathlib ... incompatible" 이 또 나오면: `conda remove pathlib` 로 지운 뒤 다시 빌드.
> 실행 중 cv2/numpy 에러가 나면 이미 `--collect-all cv2` 가 들어있으니 대개 괜찮음.

### 배포 / 사용
`rapid_sidecar.exe` 와 `rapid_sidecar.config.json` 을 **`EpsonScanApi.exe` 와 같은 폴더**에 둔다.
- 서버가 켜지면 사이드카를 자동으로 띄운다(직접 실행 불필요).
- 상태 확인: `GET /health` 의 `engines.rapidocr` 항목, 또는 `http://127.0.0.1:8011/health`.
- 설정 변경: `rapid_sidecar.config.json` (정규식/노이즈/필터). **exe 로 도는 경우, config 파일만 고치면 반영됨**(코드가 아니라 읽는 파일이라). 단 파이썬 코드(_extract 로직 등)를 바꾸면 exe 를 다시 빌드해야 함. (CONFIG.md 2번)

### config 요약 (rapid_sidecar.config.json)
- `part_no_pattern` : 부품번호 정규식(앞글자 글자/숫자 모두 허용 → S018-16070D OK)
- `noise` : 무시할 오탐 토큰
- `min_len` / `min_digits` : 최소 길이 / 최소 숫자 개수
- `top_frac` : 상단 몇 %를 부품번호 후보 영역으로
- 부품번호 선택 = 상단 영역에서 위→아래/왼→오 첫 후보(제일 위/왼쪽).

---

## 3. EpsonScanTester (동작 확인용 콘솔)

서버가 떠 있는 상태에서, 스캔→부품번호가 나오는지 빠르게 확인.
```
cd EpsonScanTester
dotnet run
```
- 서버 주소를 바꾸려면: `dotnet run -- http://127.0.0.1:8000`
- `?ocrOnly=true` + `card:null` 로 요청하도록 되어 있어 32비트 메모리 문제를 피함.
- 출력: 상태 / part_no / 신뢰도 / OCR 전체 텍스트.
- **더블클릭하면 창이 바로 닫히니** 반드시 터미널에서 `dotnet run` 으로.
- net472 라 빌드 시 .NET Framework 4.7.2 타게팅 팩 필요(보통 Visual Studio 에 포함). 없으면 VS 에서 F5.

---

## 4. 병합용 샘플 코드 (SAMPLE_EpsonScanIntegration)

IO Inspector(4.7.2)에 **복사해 넣는 참고 코드**. 폴더 자체는 빌드/실행하지 않음.
- 핵심: `EpsonScanClient.cs` (`ScanPartNo()` 한 번에 스캔→part_no)
- 서버 켜는 헬퍼(선택): `EpsonScanServerHost.cs`
- 비동기/인터페이스판(선택): `EpsonScanLabelService.cs` + `EpsonScanOptions.cs` + `ILabelScanService.cs` + `LabelScanResult.cs`
- 기본값이 `ocrOnly=true`, `card=null`(메모리 안전)로 맞춰져 있음.
- 자세한 병합 순서/배치는 그 폴더의 `README.md` 참고.

---

## 5. 실무자 PC 최종 배치 (예시)

한 폴더에 아래를 함께 둔다(서버가 사이드카를 같은 폴더에서 찾음):
```
{설치폴더}\
  EpsonScanApi.exe            (+ self-contained 런타임 파일들)
  appsettings.json            (서버 설정 - ScanOutputDir 등)
  rapid_sidecar.exe           (부품번호 보조 인식)
  rapid_sidecar.config.json   (사이드카 튜닝값)
```
실행: `EpsonScanApi.exe` 하나만 켜면 사이드카까지 자동 기동.
확인: `http://127.0.0.1:8000/swagger` 또는 `/health`.

### 대상 PC 사전 설치
- EPSON ES-C320W WIA 드라이버
- 엡손 OCR 엔진(ScanSmart 번들) 또는 Tesseract
- (self-contained 로 배포하면 .NET 설치는 불필요)

---

## 6. 문제 생기면

| 증상 | 원인/해결 |
|------|-----------|
| exe 더블클릭하자마자 꺼짐 | 32비트 런타임 없음 → self-contained, 또는 포트 사용중, 또는 ScanOutputDir 경로. cmd 실행으로 에러 확인 |
| "카드 추출/영역 덮기: Insufficient memory" | 32비트 메모리. 요청에 `card:null`, `?ocrOnly=true` (테스터/샘플엔 이미 적용) |
| "OCR/PDF 실패: Insufficient memory" | PDF 생성 단계 메모리. `?ocrOnly=true` 로 PDF 생략(part_no는 그대로) |
| part_no 앞글자 떨어짐(S018→018) | 사이드카 정규식. config `part_no_pattern` 확인 + exe 재빌드/교체 |
| 설정 바꿔도 안 먹음 | 실행되는 exe 옆의 설정 파일을 고쳐야 함. 서버 콘솔 로그로 실제 값 확인 |
| 빌드 시 pathlib 에러 | `conda remove pathlib` 또는 `pip uninstall pathlib` 후 재빌드 |
