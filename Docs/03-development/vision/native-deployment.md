# Native / VLAD Deployment

기준일: 2026-06-22

이 문서는 `AI.Vision.IOInspector.App`를 개발툴 없이 실행 PC에 배포할 때 필요한 네이티브/VLAD 런타임 기준을 정리합니다.

## 현재 빌드 기준

| 항목 | 값 |
| --- | --- |
| IDE | Visual Studio 2022 |
| Target Framework | .NET Framework 4.7.2 |
| Platform | x64 전용 |
| RuntimeIdentifier | win-x64 |
| 메인 실행 파일 | `AI.Vision.IOInspector.App.exe` |
| 출력 폴더 | `AI.Vision.IOInspector.App\bin\x64\<Debug|Release>\net472` |

`Directory.Build.props`에서 x64를 고정하고, `App.csproj`에서 런타임 폴더를 출력 폴더로 복사합니다.

## 배포 폴더 구조

Release 기준으로 다음 폴더를 한 묶음으로 배포합니다.

```text
AI.Vision.IOInspector.App.exe
AI.Vision.IOInspector.*.dll
CFG\
  Config.json
  HD_BackupConfig.json
DB\
  DataBase.db
  Image\
  History\
  Logs\
Native\
  VLAD\
    VLAD_SDK.dll
    VLAD_Ctrl.dll
    MVSDK_Net.dll
    OpenCvSharp.dll
    OpenCvSharp.Blob.dll
    libvlc.dll
    libvlccore.dll
    plugins\
RuntimeData\
  Models\
    VLAD\
      Ex_Weight\
```

`DB\History`와 `DB\Logs`는 비어 있어도 빌드 출력에서 생성합니다. 실제 검사 이미지와 로그는 실행 중 이 폴더 아래에 쌓입니다.

## DLL 탐색 방식

.NET Framework는 기본적으로 실행 파일 하위 폴더의 관리 DLL을 자동 검색하지 않습니다. 그래서 앱 시작 시 `RuntimeAssemblyResolver`가 다음 작업을 수행합니다.

- `Native\VLAD`를 `AssemblyResolve` 대상 경로로 등록
- `Native\VLAD`를 `PATH` 앞쪽에 추가
- `SetDllDirectory(Native\VLAD)` 호출

이 작업으로 `OpenCvSharp.dll`, `MVSDK_Net.dll`처럼 `Native\VLAD` 하위에 있는 관리 DLL 로드 오류를 줄입니다.

## VLAD 초기화 흐름

현재 WPF 기본 실행 흐름은 별도 `VisionWorker.exe`가 아니라 WPF 프로세스 안에서 초기화합니다.

```text
AppBootstrapper
  -> VisionRuntimeFactory.InitializeVladRuntimeOnStartup
  -> VladCamModeRuntime.EnsureLoaded
  -> VladSdkSession.EnsureStarted
  -> VLAD_Ops_Ai_Env_Start
  -> VLAD_Custom_Registration
```

검사 추론은 UI 스레드가 아니라 `VisionInferenceWorker` 전용 스레드에서 수행합니다.

```text
VisionAiInferenceService
  -> VisionInferenceWorker
  -> VladVisionInferenceEngine
  -> VLAD_Inference_Mat
  -> VLAD_Custom_InferenceData_V1
```

## 외부 필수 런타임

`Native\VLAD`에 모든 외부 런타임이 포함되어 있다는 보장은 없습니다. 배포 PC에서 다음 항목을 확인합니다.

| 항목 | 확인 방법 | 비고 |
| --- | --- | --- |
| .NET Framework 4.7.2 | Windows 기능/설치 프로그램 확인 | 앱 실행 필수 |
| VC++ Runtime | Visual C++ Redistributable x64 설치 확인 | OpenCV/VLC/TensorFlow 계열 DLL에서 필요 가능 |
| CUDA Runtime 11.0 | `where cudart64_110.dll` | TensorFlow GPU 런타임에서 필요 가능 |
| cuDNN 8.x | `where cudnn64_8.dll` | `cudart64_110.dll` 로드 후 다음 단계에서 필요 가능 |
| cuBLAS 11.x | `where cublas64_11.dll` | TensorFlow GPU 런타임에서 필요 가능 |

`AI_VISION_CUDNN_PATH`, `CUDNN_PATH`, `AI_VISION_VLAD_MODEL_PATH`, `AI_VISION_VLAD_GPU` 환경변수로 일부 경로/설정을 override할 수 있습니다.

## Config.json 기준

현재 카메라/모델 설정의 기준 파일은 `CFG\Config.json`입니다.

- 이전 옵션 UI 전용 설정은 `CFG\HD_BackupConfig.json`에 보존합니다.
- `RuntimeData\Camera\camera-config.json`은 더 이상 런타임 기준으로 사용하지 않습니다.
- `MODEL` 값이 상대 경로이면 프로젝트/실행 루트 기준으로 해석합니다.
- `AI_VISION_VLAD_MODEL_PATH` 환경변수가 있으면 `Config.json`보다 우선합니다.

## 주의할 점

- 현재 in-process VLAD 초기화는 디버깅이 쉽지만, 네이티브 SDK가 fail-fast하면 WPF 앱 전체가 종료될 수 있습니다.
- 최종 모델/런타임 배치 전에는 `VLAD_Custom_Registration` 성공 여부를 단정하면 안 됩니다.
- `RuntimeData\Models\VLAD\Ex_Weight`가 checkpoint-only 구조이면 VLAD_SDK가 바로 추론하지 못할 수 있습니다. AI 담당자에게 최종 export 모델 구조를 확인해야 합니다.
- `Native\VLAD`와 `RuntimeData\Models`는 대용량입니다. GitHub에는 Git LFS 또는 별도 배포 패키지 사용 여부를 결정해야 합니다.

## 배포 전 체크리스트

1. `dotnet build ... -c Release -p:Platform=x64` 성공.
2. Release 출력 폴더에 `CFG`, `DB`, `Native\VLAD`, `RuntimeData\Models`가 존재.
3. 클린 PC에서 .NET Framework 4.7.2와 VC++ Runtime 설치 확인.
4. GPU 사용 시 CUDA/cuDNN/cuBLAS DLL 탐색 확인.
5. `CFG\Config.json`의 RTSP URL, 모델 경로, Site 값 확인.
6. 앱 시작 후 `DB\Logs\vlad-startup.log` 또는 디버그 출력에서 `VladId` 반환 여부 확인.
7. 6채널 영상 수신, 기준 이미지 저장, 검사 시작, History 이미지 저장까지 실제 장비로 확인.
