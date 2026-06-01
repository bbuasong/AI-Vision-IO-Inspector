# AI.Vision.IOInspector.Vision

Vision 담당자가 우선 읽어야 하는 문서는 `Docs` 폴더에 모아두었습니다.

```text
Docs/README.md
Docs/vision-project-boundary.md
Docs/vlad-imv-conversion-guide.md
Docs/camera-ai-integration.md
Docs/native-deployment.md
Docs/vision-implementation-checklist.md
```

이 프로젝트는 카메라, 영상, AI 구현 작업을 모아두는 전용 영역입니다.

WPF 앱은 아래 애플리케이션 인터페이스를 통해서만 Vision 프로젝트와 통신합니다.

```text
ICameraService
IAiInferenceService
```

AI 또는 카메라 담당자는 실제 카메라 프레임 수신, 보정, 이미지 전처리, 모델 추론, 측정값 추출을 이 프로젝트 안에서 구현합니다. 일반적인 AI/카메라 작업 때문에 앱, ViewModel, DB 저장소, 이력 저장, CSV 로직을 직접 수정하지 않는 구조를 목표로 합니다.

현재 연결 구조는 다음과 같습니다.

- `VisionRuntimeFactory`는 앱에서 사용할 카메라 서비스와 AI 서비스를 생성합니다.
- `VisionCameraService`는 `VisionCameraCoordinator`를 호출하며, 이 클래스가 향후 직접 SDK/RTSP/NVR 작업자의 중심이 됩니다.
- `VisionAiInferenceService`는 추론 요청을 전용 백그라운드 스레드인 `VisionInferenceWorker`로 전달합니다.
- `SimulatedVisionInferenceEngine`은 실제 AI 모델이 연결되기 전까지 현재 검사 시뮬레이션 흐름을 유지합니다.
- `LegacyVlad`에는 `VLAD_Registration`, `VLAD_Inference_Mat`처럼 기존 VLAD 함수명으로 찾을 수 있는 어댑터 뼈대가 있습니다.
- `ImvCamera`에는 `OpenDevice`, `StartGrabbing`, `GetFrame`, `ReleaseFrame`, `StopGrabbing`처럼 기존 IMV 흐름과 대응되는 어댑터 뼈대가 있습니다.
- `VladFunctionAdapter`와 `ImvFunctionAdapter`는 기존 함수명을 검색할 수 있게 유지하면서 작업 위치를 새 프로젝트 구조로 연결합니다.

중요 원칙은 다음과 같습니다.

- 측정값을 반환할 때는 가능한 한 단위를 명시합니다.
- 애플리케이션 계층은 `mm`, `cm`, `m` 단위를 DB 기준값 단위로 변환한 뒤 판정합니다.
- 함수명과 SDK 이름은 기존 담당자가 검색하기 쉽도록 유지하되, 설명 주석은 한국어로 작성합니다.

스레드 기준 구조는 다음과 같습니다.

```text
WPF UI / ViewModel
  -> InspectionWorkflowService
    -> VisionCameraService
      -> VisionCameraCoordinator
        -> 향후 IMV/RTSP 카메라 수신 작업자
    -> VisionAiInferenceService
      -> VisionInferenceWorker 스레드
        -> IVisionInferenceEngine
```

현재 카메라 조율 클래스는 아직 실제 장비 SDK를 직접 호출하지 않고 기존 설정 기반 시뮬레이션/파일 카메라 서비스를 사용합니다. AI 추론은 이미 스레드 경계가 분리되어 있으므로, 추후 무거운 모델 실행을 UI 스레드 밖에서 처리할 수 있습니다.
