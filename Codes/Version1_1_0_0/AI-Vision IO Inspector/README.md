# AI-Vision IO Inspector

HD현대사이트솔루션 입고 검사 업무를 위한 C# WPF MVVM 애플리케이션입니다. 부품 기준정보, 기준 이미지, 카메라 캡처, VLAD AI 추론, 검사 이력 저장을 한 화면 흐름으로 묶는 것이 목표입니다.

## 현재 기준

| 항목 | 현재 값 |
| --- | --- |
| 기준일 | 2026-06-22 |
| IDE | Visual Studio 2022 |
| 메인 앱 | WPF MVVM |
| Target Framework | .NET Framework 4.7.2 |
| Platform | x64 전용 |
| DB | SQLite, `DB\DataBase.db` |
| 카메라 설정 | `CFG\Config.json` |
| 기준 이미지 | `DB\Image` |
| 검사 이미지/로그 | `DB\History`, `DB\Logs` |
| VLAD 런타임 | `Native\VLAD`, `RuntimeData\Models\VLAD` |

## 솔루션 위치

```text
Tests\AI-Vision IO Inspector\AI.Vision.IOInspector.sln
```

주요 프로젝트는 다음과 같습니다.

| 프로젝트 | 역할 |
| --- | --- |
| `AI.Vision.IOInspector.App` | WPF UI, ViewModel, 앱 조립 |
| `AI.Vision.IOInspector.Application` | 검사 흐름, 기준값 비교, 통계 서비스 |
| `AI.Vision.IOInspector.Domain` | Part, Measurement, Inspection 등 핵심 모델 |
| `AI.Vision.IOInspector.Infrastructure` | SQLite, 파일 저장, 기준 이미지 관리, Native 경로 설정 |
| `AI.Vision.IOInspector.Vision` | 카메라 수신, VLAD SDK 연결, AI 추론, 측정값 변환 |

## 빌드

```powershell
dotnet build "C:\SVN_LinkGenesis\FA_HDX\AI-Vision IO Inspector\Tests\AI-Vision IO Inspector\AI.Vision.IOInspector.sln" -c Debug -p:Platform=x64
dotnet build "C:\SVN_LinkGenesis\FA_HDX\AI-Vision IO Inspector\Tests\AI-Vision IO Inspector\AI.Vision.IOInspector.sln" -c Release -p:Platform=x64
```

실행/배포 기준 출력 폴더는 다음 형태입니다.

```text
Tests\AI-Vision IO Inspector\AI.Vision.IOInspector.App\bin\x64\Release\net472
```

이 폴더만 배포해도 동작할 수 있도록 빌드 시 `CFG`, `DB`, `Native\VLAD`, `RuntimeData\Models`가 출력 폴더에 복사됩니다. 단, CUDA/cuDNN/VC++ Runtime처럼 PC에 설치되거나 별도 배치가 필요한 외부 런타임은 배포 전 확인해야 합니다.

## 현재 Vision 흐름

```text
App 시작
  -> RuntimeAssemblyResolver.Register
  -> VisionRuntimeFactory.InitializeVladRuntimeOnStartup
  -> VladCamModeRuntime.EnsureLoaded
  -> VladSdkSession.EnsureStarted
  -> VLAD_Ops_Ai_Env_Start
  -> VLAD_Custom_Registration

검사 시작
  -> MainWindowViewModel
  -> InspectionWorkflowService
  -> ICameraService.CaptureAll
  -> VisionAiInferenceService
  -> VisionInferenceWorker
  -> VladVisionInferenceEngine
  -> VLAD_Inference_Mat / VLAD_Custom_InferenceData_V1
  -> MeasurementService / JudgmentService
  -> SQLite History 저장
```

`Native\VLAD` 하위의 `OpenCvSharp.dll`, `MVSDK_Net.dll` 같은 관리 DLL은 .NET Framework 기본 탐색 경로에 자동 포함되지 않습니다. `RuntimeAssemblyResolver`가 앱 시작 시 `Native\VLAD`를 `AssemblyResolve`, `PATH`, `SetDllDirectory`에 등록합니다.

## 남은 핵심 확인

- `RuntimeData\Models\VLAD\Ex_Weight`가 VLAD_SDK가 직접 읽을 수 있는 최종 추론 모델 구조인지 AI 담당자 확인이 필요합니다.
- `cudart64_110.dll`, `cudnn64_8.dll`, `cublas64_11.dll`, VC++ Runtime 배치 또는 설치가 필요합니다.
- 실제 6대 카메라/NVR 환경에서 장시간 스트리밍, 캡처, 검사 시작/종료 안정성 검증이 필요합니다.
- VLAD `detectData`/`detectText`에서 길이/너비/높이/두께와 카메라별 Pass/Fail을 받는 최종 구조체 스키마 확정이 필요합니다.

자세한 최신 구조와 잔여 항목은 `Docs\03-development\project-structure-2026-06-22.md`, `Docs\03-development\open-items.md`를 기준으로 봅니다.
