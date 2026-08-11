# 설정(config) 바꾸는 법 - 서버 / 사이드카 / 테스터

코드에 박혀 있던 튜닝값들을 설정 파일로 뺐다. **재빌드 없이** 설정 파일만 고치고 재시작하면 바뀐다.
(단, 값이 코드 파일이 아니라 "실행되는 exe 옆의 설정 파일"에 있어야 먹는다. 아래 주의 참고.)

---

## 1. 서버 설정 - `appsettings.json`

서버 exe 와 같은 폴더의 `appsettings.json` 을 메모장으로 열어 고친다. 서버 재시작하면 적용.

### 기존 항목
| 키 | 뜻 | 기본값 |
|----|----|--------|
| `ScanOutputDir` | 스캔/PDF 결과 저장 폴더. 없는 드라이브면 에러 → 존재하는 경로로. | `D:\epson_scans` |
| `ScanApiHost` | 서버 바인드 주소. 다른 PC 접속 허용은 `0.0.0.0` | `127.0.0.1` |
| `ScanApiPort` | 서버 포트 | `8000` |
| `RapidSidecarUrl` | RapidOCR 사이드카 주소 | `http://127.0.0.1:8011` |
| `RapidSidecarExe` / `RapidPythonExe` / `RapidSidecarScript` | 사이드카 실행 경로 | - |

### 새로 추가한 튜닝 항목 (`Tuning` 섹션)
```jsonc
"Tuning": {
  "Ocr": {
    "MinLetters": 40,          // 이 글자 수 미만이면 품질 불량 판정
    "MinRatio": 0.80,          // 정상글자 비율 이 값 미만이면 품질 불량 (신뢰도)
    "RequiredAnchors": ["EA"], // 라벨에 반드시 있어야 하는 문자열(없으면 품질 불량)
    "PartNoPattern": "[A-Za-z0-9]+(?:[-‐-―][A-Za-z0-9]+)*",  // 엡손 부품번호 정규식
    "PartNoBrackets": "()[]{}<>«»...",  // 괄호번호(sub) 인식용 괄호 문자들
    "DefaultDpi": 300          // DPI 정보 없는 이미지의 기본 DPI
  },
  "Card": {
    "MarginFrac": 0.04,        // 라벨 크롭 시 여백 비율
    "InkCap": 150,             // 잉크(글자) 이진화 임계 상한
    "DeskewDim": 1100,         // 기울기 보정 계산용 축소 크기
    "DeskewRange": 20,         // 기울기 탐색 범위(±도)
    "DeskewCoarse": 1.0,       // 1차 탐색 간격(도)
    "DeskewFine": 0.2,         // 2차 미세 탐색 간격(도)
    "BarcodeBandMinCount": 8   // 바코드 밴드로 볼 최소 얇은막대 수(OCR 잡음 필터)
  }
}
```
- **품질 임계값을 낮추고 싶다** → `MinLetters`, `MinRatio` 낮추기 (예: 라벨 글자가 적으면 40 → 20)
- **부품번호가 이상하게 잘린다** → 지금은 엡손 정규식이 아니라 **RapidOCR 정규식**(아래 2번)이 결과를 결정하는 경우가 많음. 2번을 먼저 확인.
- **자주 오는 오탐 문자열** → RequiredAnchors 를 실제 라벨에 맞게. (기본 "EA")
- 값을 안 넣거나 섹션이 없으면 **코드 기본값**(위 표)으로 동작한다.

---

## 2. RapidOCR 사이드카 설정 - `rapid_sidecar.config.json`

사이드카 실행 파일(rapid_sidecar.exe 또는 .py) **옆에** `rapid_sidecar.config.json` 을 둔다.

```jsonc
{
  "part_no_pattern": "[A-Z0-9]+(?:-[A-Z0-9]+)*",  // 부품번호 정규식(대문자로 정규화된 문자열 대상)
  "noise": ["AI","AL","2EA","EA","IT","RCV","RH","WORKING","미","검","수","검수"], // 무시할 오탐 토큰
  "min_len": 6,        // part_no 최소 길이
  "min_digits": 4,     // 최소 숫자 개수
  "top_frac": 0.40     // 이미지 상단 몇 %(0.40=40%)를 부품번호 후보 영역으로
}
```

### 부품번호 선택 순서
상단 영역(`top_frac`) 안에서 **위→아래, 왼→오** 순으로 첫 부품번호 후보를 고른다(= 제일 위/왼쪽).
괄호번호 `(숫자)` 는 부품번호와 별개로 따로 뽑기만 하고, 선택 기준으로는 쓰지 않는다.
(부품번호가 아래쪽/괄호 근처에 있는 라벨이면 이 순서를 바꿔야 할 수 있음.)

### ★ S018-16070D 처럼 앞글자 떨어지던 문제
기존 정규식 `[0-9][A-Z0-9]{2,}...` 은 **숫자로 시작**을 강제해서, `S018-16070D` → `018-16070D`(S 탈락)였다.
`part_no_pattern` 을 `[A-Z0-9]+(?:-[A-Z0-9]+)*` (맨 앞 글자 허용)으로 바꿔 해결. (config 기본값이 이미 수정본)

### ★★ 주의: 사이드카는 exe 로 도는 경우가 많다
`OcrRapid.cs` 는 `rapid_sidecar.exe` 가 있으면 **.py 대신 .exe 를 먼저 실행**한다.
그래서 **exe 가 있으면 .py / config 수정이 반영 안 될 수 있다.** 반영하려면 둘 중 하나:
1. `rapid_sidecar.py` 로 exe 를 **다시 빌드**(PyInstaller), 또는
2. `rapid_sidecar.exe` 를 지우거나 이름 변경 → 서버가 번들 파이썬으로 `.py` 를 직접 실행
   (그러면 `.py` + `rapid_sidecar.config.json` 이 바로 먹음)
- config 파일은 코드가 아니라 **읽는 파일**이라, exe 를 config 읽는 버전으로 빌드해두면 이후엔 config 만 고쳐도 됨.

---

## 3. 테스터(EpsonScanTester) 설정

테스터는 별도 설정 파일이 아니라 `Program.cs` 안의 값이다. 자주 바꾸는 것:

| 항목 | 위치(Program.cs) | 설명 |
|------|------------------|------|
| 서버 주소 | 실행 인자 `args[0]` 또는 기본 `http://127.0.0.1:8000` | `dotnet run -- http://…` 로도 지정 가능 |
| DPI / mode / source / fmt | `requestBody.scan` | 스캔 해상도/모드 등 |
| PDF 생략 여부 | URL `?ocrOnly=true` | 붙이면 PDF 없이 OCR+part_no (메모리 절약) |
| 카드 크롭 | `card = (object)null` | null 이면 카드 추출(메모리 큼) 건너뜀 |

테스터 값은 코드라서, 바꾸면 `dotnet run`(자동 재빌드)으로 다시 실행하면 된다.

---

## 4. 어디를 고쳐야 먹나 (제일 헷갈리는 부분)

- `appsettings.json` / `rapid_sidecar.config.json` 은 **실행되는 exe 와 같은 폴더**의 것을 고쳐야 한다.
  프로젝트 루트만 고치면 이미 빌드된 exe 옆 복사본은 안 바뀐다.
- 프로젝트 루트를 고친 뒤 **다시 빌드/publish** 하면 모든 출력 폴더로 복사된다(가장 확실).
- 서버 켤 때 콘솔에 뜨는 `[Scan API] 출력 디렉토리: ...` 로 실제 읽은 값을 확인할 수 있다.
