# AI-Vision IO Inspector

HD현대사이트솔루션 입고 검사 업무를 위한 C# WPF MVVM 애플리케이션입니다. 부품 기준정보, 기준 이미지, RTSP 카메라 캡처, VLAD AI 추론, 검사 이력 저장을 한 화면 흐름으로 묶는 것이 목표입니다.

## 현재 기준

| 항목 | 현재 값 |
| --- | --- |
| 기준일 | 2026-08-11 |
| IDE | Visual Studio 2022 |
| 메인 앱 | WPF MVVM |
| Target Framework | .NET Framework 4.7.2 |
| Platform | x64 전용 (`win-x64`, `SelfContained=false`) |
| Assembly Version | 1.1.0.0 (`Directory.Build.props`) |
| DB | SQLite, `DB\DataBase.db` |
| 앱 설정 | `CFG\Config.json` (`LAST_UI=CUSTOM`, `LAST_USER=HD`) |
| VLAD 런타임 설정 | `CFG\VladRuntimeSettings.json` |
| VLAD 모델 경로 | `RuntimeData\Models\VLAD\Ex_Weight` |
| Native 의존성 | `Native\VLAD` |
| 검사 통과 점수 | `INSPECTION_PASS_SCORE_THRESHOLD = 95.00` |
| 유사도 검색 기준 | `SINGLE_PART_SIMILARITY_THRESHOLD = 99.00` |

## 솔루션 위치

```text
Codes\AI-Vision IO Inspector\AI.Vision.IOInspector.sln
```

프로젝트는 5개입니다.

| 프로젝트 | 역할 |
| --- | --- |
| `AI.Vision.IOInspector.App` | WPF UI, ViewModel, 팝업, RTSP 라이브 화면, OCR/부품등록 화면 제어 |
| `AI.Vision.IOInspector.Application` | 검사 워크플로우, 판정, 측정부 비교, 통계 서비스 |
| `AI.Vision.IOInspector.Domain` | Part, PartImage, MeasurementRegion, Inspection 등 핵심 모델과 파일명 정책 |
| `AI.Vision.IOInspector.Infrastructure` | SQLite, 기준 이미지 파일 저장, OCR, 카메라 설정, 이력 보존 |
| `AI.Vision.IOInspector.Vision` | VLAD SDK 연동, MAT JSON 요청/결과 파싱, RTSP callback 프레임 캐시 |

`AI.Vision.IOInspector.VisionWorker`는 2026-08-05 커밋 `e09cc04`에서 제거했습니다.

## 빌드

```bash
dotnet build "C:\SVN_LinkGenesis\FA_HDX\AI-Vision IO Inspector\Codes\AI-Vision IO Inspector\AI.Vision.IOInspector.sln" -c Debug -p:Platform=x64
```

```bash
dotnet build "C:\SVN_LinkGenesis\FA_HDX\AI-Vision IO Inspector\Codes\AI-Vision IO Inspector\AI.Vision.IOInspector.sln" -c Release -p:Platform=x64
```

실행/배포 기준 출력 폴더는 다음 형태입니다.

```text
Codes\AI-Vision IO Inspector\AI.Vision.IOInspector.App\bin\x64\Release\net472
```

이 폴더만 배포해도 동작할 수 있도록 빌드 시 `CFG`, `DB`, `Native\VLAD`, `RuntimeData\Models`가 출력 폴더에 복사됩니다. 단, CUDA/cuDNN/VC++ Runtime처럼 PC에 설치되거나 별도 배치가 필요한 외부 런타임은 배포 전 확인해야 합니다(`open-items.md` O-002).

Release 구성은 `Directory.Build.props`에서 `DebugType=none`이므로 PDB를 생성하지 않습니다.

## 카메라 구성

`CFG\Config.json`의 `CUSTOM.HD.CAMS` 기준으로 6채널 모두 RTSP이며 전부 활성 상태입니다.

| 키 | View | 해상도 | FPS |
| --- | --- | --- | --- |
| `CAM0` | Top | 2592x1944 | 30 |
| `CAM1` | Front | 1920x1080 | 30 |
| `CAM2` | Back | 1920x1080 | 30 |
| `CAM3` | Left | 1920x1080 | 30 |
| `CAM4` | Right | 1920x1080 | 30 |
| `CAM5` | Thickness | 2592x1944 | 30 |

검사 UI의 라이브 화면은 `RtspVideoHost`가 LibVLC로 직접 렌더링하고, 검사 캡처는 VLAD RTSP callback이 캐시한 최신 프레임을 사용합니다.

## 앱 시작 흐름

```text
App 시작
  -> AppBootstrapper
  -> NativeDependencyLoader              (Native\VLAD, Epson OCR, LibVLC 경로 준비)
  -> VisionRuntimeFactory.BeginInitializeVladRuntimeOnStartup()   (백그라운드 예약)
  -> SQLite 기준정보/이력 저장소 생성
  -> VisionRuntimeFactory                (카메라 서비스, AI 추론 서비스 생성)
  -> MainWindowViewModel 주입
       LocalReferenceImageFileService
       VladImageMergeService
       WpfReferenceCoordinateImageService
       EpsonEsC320wOcrService
```

VLAD 런타임 초기화는 `VladCamModeRuntime` → `VladSdkSession` → `VLAD_Ops_Ai_Env_Start` → `VLAD_Custom_Registration` 순으로 진행됩니다.

`Native\VLAD` 하위의 `OpenCvSharp.dll`, `MVSDK_Net.dll` 같은 관리 DLL은 .NET Framework 기본 탐색 경로에 자동 포함되지 않습니다. `NativeDependencyLoader`가 앱 시작 시 해당 경로를 `AssemblyResolve`, `PATH`, `SetDllDirectory`에 등록합니다.

## 검사 흐름

`InspectionWorkflowService.RunInspection()` 기준입니다.

```text
1. 입력 품번으로 기준정보 조회          (없으면 ERROR 저장 후 종료)
2. 측정부 등록 품목이면 Thickness 기준 이미지를 coordinate 이미지로 대체
3. 활성 카메라의 최신 프레임 캡처
4. VLAD MAT API 검사 요청
5. MAT JSON 결과 파싱                   (viewJudge, score, dimensions, measurements)
6. AI 측정값을 DB 측정부 기준정보와 연결
7. OK/NG/ERROR 판정 후 이력/이미지/이벤트 저장
```

## VLAD MAT JSON 계약

현재 기준은 별도 Result 조회 API가 아니라, `VLAD_HD_Inference_Mat`에 전달한 JSON 버퍼를 DLL이 in-place로 갱신하는 방식입니다.

- Native 선언: `VLAD_HD_Inference_Mat(IntPtr fullImageVladId, IntPtr rawData, IntPtr requestJsonUtf8)`
- C# wrapper가 8192 byte UTF-8 고정 버퍼를 할당해 0으로 초기화하고, 호출 후 같은 버퍼를 문자열로 읽습니다.
- 요청 JSON에 결과 자리(`viewJudge`, `score`, `dimensions`, `measurements`)를 미리 포함합니다.
- 판정 기준은 JSON의 `scoreThreshold` 하나로 통일했습니다. 네이티브 `threshold` 인자는 전달하지 않습니다.
- 파싱 실패는 빈 검출이 아니라 즉시 검사 실패로 처리합니다.

상세 계약은 `Docs\03-development\20-during-development\vision\VLAD_HD_Inference_Mat수정-2026-08-07.md`와 같은 폴더의 `vlad-hd-api-v1.3-correction-2026-08-07.md`를 기준으로 봅니다.

## 문서 기준

`Docs\03-development\`는 읽는 시점 기준으로 세 폴더로 나눠 관리합니다.

| 폴더 | 언제 읽는가 | 주요 문서 |
| --- | --- | --- |
| `10-before-development\` | 작업 시작 전 | `current-program-status-2026-08-10.md`(현재 현황), `work-log.md`, `changelog.md`, `roadmap.md` |
| `20-during-development\` | 구현하는 동안 | `coding-rules.md`, `decisions.md`, `review-checklist.md`, `vision\`(VLAD SDK 계약) |
| `30-open-items\` | 다음 할 일을 정할 때 | `open-items.md`, `questions.md`, `task-board.md` |

각 폴더의 `README.md`에 파일 목록과 읽는 순서가 있습니다. 전체 안내는 `Docs\03-development\README.md`를 봅니다.

`Docs\AGENTS.md`는 AI 어시스턴트 작업 지침입니다.

## 남은 핵심 확인

현재 잔여 항목은 `Docs\03-development\30-open-items\open-items.md`에서 관리합니다. 우선순위 상위 항목은 다음과 같습니다.

- `O-032` 6개 RTSP URL을 VLC/ffmpeg로 각각 단독 검증한 로그 확인
- `O-033` 고장 카메라 포함 시 검사 진행 정책과 stale snapshot 재사용 여부 확정
- `O-034` 측정부 등록 품목의 Thickness 값, coordinate 기준 이미지, IndexNo 매핑 검증
- `O-001` `RuntimeData\Models\VLAD\Ex_Weight`가 VLAD_SDK가 직접 읽는 최종 추론 모델 구조인지 확인
- `O-002` `cudart64_110.dll`, `cudnn64_8.dll`, `cublas64_11.dll`, VC++ Runtime 배치/설치 확인
- `O-029` `VLAD_HD_ImageMerge` export가 포함된 DLL로 교체 후 실제 병합 이미지 생성 검증

## 저장소 상태 주의

2026-08-11 기준으로 작업 트리와 Git 인덱스가 어긋나 있습니다.

- `Codes\AI-Vision IO Inspector\`(현재 개발 소스)가 **untracked** 상태입니다.
- `Codes\Version1_0_0_0`, `Codes\Version1_1_0_0`, `Tests\`, `Docs\05-simulator`가 작업 트리에서 삭제된 상태로 남아 있습니다.
- 루트에 사용하지 않는 `AI.Vision.IOInspector.sln`(2026-06-22)이 untracked로 남아 있습니다. 실제 솔루션은 `Codes\AI-Vision IO Inspector\` 아래입니다.

커밋 전에 폴더 재구성 결과를 Git에 반영할지 결정해야 합니다.
