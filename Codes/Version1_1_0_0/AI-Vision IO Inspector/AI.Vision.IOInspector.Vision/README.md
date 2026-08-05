# AI.Vision.IOInspector.Vision

카메라 수신, VLAD SDK 연결, AI 추론, 측정값 변환을 담당하는 전용 프로젝트입니다. WPF UI와 ViewModel은 `ICameraService`, `IAiInferenceService`만 사용하고, 실제 Vision 구현은 이 프로젝트 안에서 교체합니다.

## 현재 기준

| 항목 | 값 |
| --- | --- |
| 기준일 | 2026-06-22 |
| Target Framework | .NET Framework 4.7.2 |
| Platform | x64 |
| 설정 기준 | `CFG\Config.json` |
| 모델 기준 | `RuntimeData\Models\VLAD` 또는 `AI_VISION_VLAD_MODEL_PATH` |
| 실행 방식 | WPF 프로세스 내부 + `VisionInferenceWorker` 전용 스레드 |

## 주요 흐름

```text
WPF UI / ViewModel
  -> InspectionWorkflowService
    -> VisionCameraService
      -> VisionCameraCoordinator
        -> ConfiguredCameraService (RTSP/File/NVR)
        -> ImvCameraDevice (Direct SDK 후보)
    -> VisionAiInferenceService
      -> VisionInferenceWorker
        -> VladVisionInferenceEngine
          -> VladCamModeRuntime / VladSdkSession
          -> VLAD_Inference_Mat
          -> VLAD_Custom_InferenceData_V1
```

## AI 담당자 진입점

| 목적 | 파일 |
| --- | --- |
| Vision 서비스 생성 | `VisionRuntimeFactory.cs` |
| VLAD CAM 모드 초기화 | `LegacyVlad/VladCamModeRuntime.cs` |
| 공유 VLAD_ID 관리 | `LegacyVlad/VladSdkSession.cs` |
| 기존 VLAD_Ops 함수명 호환 | `LegacyVlad/VLAD_Ops_Ai.cs` |
| VLAD_SDK.dll P/Invoke | `LegacyVlad/VladNativeMethods.cs` |
| RTSP Thread/Callback 호환 | `LegacyVlad/VLAD_Ops_RTSP.cs` |
| VLAD 등록/추론 | `Engines/VladVisionInferenceEngine.cs` |
| 추론 스레드 | `Threading/VisionInferenceWorker.cs` |
| OpenCV Mat 변환 | `LegacyVlad/OpenCvSharpMatImage.cs` |
| detectData 해석 | `LegacyVlad/VladInferenceResultParser.cs` |
| detectText/bbox -> 측정값 변환 | `Services/VladMeasurementMapper.cs` |
| pixel-mm 보정값 | `Services/MeasurementCalibrationService.cs`, `CFG/Calibration.json` |
| Direct SDK 카메라 제어 후보 | `ImvCamera/ImvCameraDevice.cs` |

## 현재 상태

- `VLAD_Ops_Ai_Env_Start`는 WPF 앱 시작 시 `VisionRuntimeFactory.InitializeVladRuntimeOnStartup` 경로에서 호출됩니다.
- `VladSdkSession`이 `VladId`를 재사용합니다.
- 검사 추론은 UI 스레드가 아니라 `VisionInferenceWorker` 전용 스레드에서 수행합니다.
- C# 단계에서 기준 이미지 유무만으로 검사를 하드 차단하지 않습니다. 기준 이미지가 없으면 안내 후 사용자가 계속 진행할 수 있습니다.
- 치수값을 확정할 수 없으면 기준값으로 위장하지 않고 실패/미측정 상태를 반환해야 합니다.

## 남은 확인

- AI 담당자가 제공할 최종 모델은 `checkpoint/ckpt/pipeline.config`만으로는 부족할 수 있습니다. VLAD_SDK가 읽는 최종 export 모델 구조 확인이 필요합니다.
- `detectData` 안에 길이/너비/높이/두께, 카메라별 Pass/Fail이 어떤 형식으로 들어오는지 최종 스키마 확인이 필요합니다.
- 6대 카메라 동시 스트리밍은 실제 장비에서 장시간 부하 테스트가 필요합니다.
- RTSP Thread 종료 API는 원본 코드에도 명확하지 않습니다. SDK 담당자에게 Client unregister/stop 함수 존재 여부를 확인해야 합니다.
