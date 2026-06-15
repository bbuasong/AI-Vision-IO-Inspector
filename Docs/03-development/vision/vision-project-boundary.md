# AI/Camera Vision Project Boundary

## 2026-05-30 추가 구현

카메라 촬영 요청도 AI 추론과 같은 방향으로 UI 스레드에서 분리했습니다.

```text
VisionCameraService
  -> VisionCameraCoordinator
    -> VisionCameraCaptureWorker(ViewType별 1개)
      -> IVisionCameraCaptureExecutor.ExecuteCapture
        -> 현재 ConfiguredCameraService
        -> 향후 IMV/RTSP 직접 구현
```

`CaptureAll`은 Top/Front/Back/Left/Right/Thickness Worker에 촬영 요청을 배분하고 결과를 모읍니다. 현재 실제 SDK가 아직 없으므로 내부 촬영은 기존 `ConfiguredCameraService`를 통해 시뮬레이션/파일 소스를 사용합니다. 다만 Worker 경계는 이미 만들어졌기 때문에 실제 SDK가 들어오면 `ExecuteCapture` 안쪽 구현만 장비별 구현으로 교체하는 방향을 우선 검토합니다.

아직 남은 큰 작업은 실제 `StartGrabbing/GetFrame/ReleaseFrame/StopGrabbing` 호출, 연속 미리보기 Worker, pixel-to-mm 보정, VLAD 결과 파싱입니다. 날짜별 추적은 상위 `Docs/03-development/open-items.md`, Vision 상세 체크리스트는 `vision-implementation-checklist.md`에서 관리합니다.

## 2026-05-30 적용 기준

AI와 카메라 영상 처리는 현재 솔루션 안의 `AI.Vision.IOInspector.Vision` 프로젝트에서 전담합니다. 기존 WPF 화면, ViewModel, DB Repository, 이력 저장 로직은 직접 수정하지 않고 `Application` 계층 인터페이스만 통해 연결합니다.

## 프로젝트 책임

| 프로젝트 | 책임 |
| --- | --- |
| `AI.Vision.IOInspector.App` | 화면, 사용자 입력, ViewModel 조립 |
| `AI.Vision.IOInspector.Application` | 검사 순서, 측정값 비교, 판정, 통계 |
| `AI.Vision.IOInspector.Infrastructure` | SQLite, 파일 저장, 기준 이미지, 공통 로컬 인프라 |
| `AI.Vision.IOInspector.Vision` | 카메라 연결, 영상 프레임 수신, 보정, AI 추론, 측정값 추출 |

## 연결 구조

App은 `VisionRuntimeFactory`를 통해 다음 구현체를 생성합니다.

```text
VisionRuntimeFactory
  -> VisionCameraService : ICameraService
  -> VisionAiInferenceService : IAiInferenceService
```

현재 `VisionCameraService`는 기존 `ConfiguredCameraService`를 감싼 Bridge입니다. 실제 장비 연결이 구현되면 이 프로젝트 안에서 Direct SDK, RTSP, NVR 수신 구현으로 교체합니다.

현재 `VisionAiInferenceService`는 `IVisionInferenceEngine`을 호출하고, 그 결과를 기존 `AiInferenceResult`로 변환합니다. AI 담당자는 우선 `IVisionInferenceEngine` 구현체를 새로 만들면 됩니다.

## 실행 뼈대와 Thread 구조

기존 VLAD/IMV 코드는 카메라 프레임 수신과 화면 갱신을 UI Thread와 분리했습니다. 현재 프로젝트도 같은 방향으로 가되, WPF/MVVM 구조에 맞게 다음 뼈대를 둡니다.

```text
WPF UI / ViewModel
  -> InspectionWorkflowService
    -> VisionCameraService
      -> VisionCameraCoordinator
        -> future IMV/RTSP camera receive workers
    -> VisionAiInferenceService
      -> VisionInferenceWorker thread
        -> IVisionInferenceEngine
```

현재 `VisionInferenceWorker`는 전용 background thread에서 AI 추론 요청을 처리합니다. `InspectionWorkflowService`는 동기 방식으로 결과를 기다리지만, 실제 모델 실행은 UI Thread 밖에서 수행됩니다. 다음 내부 작업은 `VisionCameraCoordinator` 아래에 ViewType별 `VisionCameraReceiveWorker`를 추가해 최신 프레임 캐시를 유지하는 것입니다.

현재 카메라 Coordinator는 기존 `ConfiguredCameraService`를 감싼 상태입니다. 즉, 아직 IMV SDK의 `StartGrabbing/GetFrame/ReleaseFrame`을 직접 호출하지는 않습니다. 이 부분은 `ImvCamera` 폴더의 Adapter 뼈대에 구현합니다.

## AI 담당자 구현 위치

| 위치 | 내용 |
| --- | --- |
| `AI.Vision.IOInspector.Vision/Engines/IVisionInferenceEngine.cs` | AI 추론 엔진 계약 |
| `AI.Vision.IOInspector.Vision/Engines/SimulatedVisionInferenceEngine.cs` | 현재 시뮬레이션 구현 |
| `AI.Vision.IOInspector.Vision/Models/VisionInspectionInput.cs` | 부품 기준정보와 촬영 이미지 입력 |
| `AI.Vision.IOInspector.Vision/Models/VisionInspectionOutput.cs` | AI 판정 결과와 측정값 출력 |
| `AI.Vision.IOInspector.Vision/Models/VisionMeasurementValue.cs` | 측정부별 측정값, 단위, raw pixel, 보정 정보 |
| `AI.Vision.IOInspector.Vision/Services/VisionAiInferenceService.cs` | Vision 출력값을 Application 결과로 변환 |
| `AI.Vision.IOInspector.Vision/Services/VisionCameraService.cs` | 카메라 서비스 경계 |
| `AI.Vision.IOInspector.Vision/Services/VisionCameraCoordinator.cs` | 6카메라 상태와 향후 수신 worker를 조율하는 중심 클래스 |
| `AI.Vision.IOInspector.Vision/Threading/VisionInferenceWorker.cs` | AI 추론 전용 background thread |
| `AI.Vision.IOInspector.Vision/LegacyVlad/*` | 기존 VLAD 함수명과 대응되는 Adapter 뼈대 |
| `AI.Vision.IOInspector.Vision/ImvCamera/*` | 기존 IMV 카메라 함수명과 대응되는 Adapter 뼈대 |

## 기존 VLAD/IMV 코드 대응표

| 기존 코드 | 새 위치 | 설명 |
| --- | --- | --- |
| `VLAD_Registration` | `LegacyVlad/VladRuntimeContext.Register` | VLAD 런타임 핸들 생성 |
| `VLAD_Ops_Inference_Registration` | `LegacyVlad/VladRuntimeContext.RegisterInferenceModel` | 모델/사이트/GPU 등록 |
| `VLAD_Inference_Mat` | `LegacyVlad/VladRuntimeContext.Inference` | OpenCV Mat 기반 추론 |
| `VLAD_InferenceData_*_Draw` | `LegacyVlad/VladRuntimeContext.Inference` 결과 변환 단계 | detect data, bbox, class, mask 변환 |
| `IMV_EnumDevices` | `ImvCamera/ImvCameraManager.EnumDevices` | 장비 검색 |
| `IMV_OpenDevice` | `ImvCamera/ImvCameraDevice.OpenDevice` | 카메라 열기 |
| `IMV_StartGrabbing` | `ImvCamera/ImvCameraDevice.StartGrabbing` | 프레임 수신 시작 |
| `IMV_GetFrame` | `ImvCamera/ImvCameraDevice.GetFrame` | 프레임 획득 |
| `IMV_ReleaseFrame` | `ImvCamera/ImvCameraDevice.ReleaseFrame` | 프레임 버퍼 반환 |
| `IMV_StopGrabbing` | `ImvCamera/ImvCameraDevice.StopGrabbing` | 프레임 수신 중지 |

## 측정값과 단위

AI 엔진은 측정값과 단위를 함께 반환해야 합니다.

```text
MeasurementRegionId
Value
Unit
RawPixelValue
CalibrationId
SourceImagePath
```

`Application`의 `MeasurementService`는 AI가 반환한 단위와 DB 기준 단위가 다르면 `mm`, `cm`, `m` 범위에서 변환 후 판정합니다. 단위가 비어 있으면 `mm`로 간주합니다.

## 현재 상태

- 실제 카메라 Direct SDK/RTSP 연결은 아직 구현 전입니다.
- 실제 AI/VLAD/ONNX 추론은 아직 구현 전입니다.
- App은 새 Vision 프로젝트를 통해 카메라/AI 서비스를 사용하도록 연결되었습니다.
- 기존 시뮬레이션 검사 흐름은 `SimulatedVisionInferenceEngine`으로 유지됩니다.
- AI 추론은 `VisionInferenceWorker` background thread를 통해 실행됩니다.
- 카메라 연속 수신 worker는 아직 구현 전이며, `VisionCameraCoordinator` 아래에 추가해야 합니다.
