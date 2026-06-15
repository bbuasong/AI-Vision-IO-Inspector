# AI.Vision.IOInspector.Vision

카메라 수신, VLAD SDK 연결, AI 추론, 측정값 변환을 담당하는 전용 프로젝트입니다.
WPF UI와 ViewModel은 `ICameraService`, `IAiInferenceService`만 사용하고, 실제 Vision 구현은 이 프로젝트 안에서 교체합니다.

## 주요 흐름

```text
WPF UI / ViewModel
  -> InspectionWorkflowService
    -> VisionCameraService
      -> VisionCameraCoordinator
        -> ConfiguredCameraService (RTSP/File/NVR)
        -> ImvCameraDevice (Direct SDK)
    -> VisionAiInferenceService
      -> VisionInferenceWorker
        -> VladVisionInferenceEngine
          -> VladSdkSession
          -> VLAD_Inference_Mat
```

## AI 담당자 진입점

| 목적 | 파일 |
| --- | --- |
| VLAD 등록/추론 시작 | `Engines/VladVisionInferenceEngine.cs` |
| 기존 VLAD_Ops 함수명 호환 | `LegacyVlad/VLAD_Ops_Ai.cs`, `LegacyVlad/VladFunctionAdapter.cs` |
| VLAD_SDK.dll P/Invoke | `LegacyVlad/VladNativeMethods.cs` |
| 공유 VLAD_ID 관리 | `LegacyVlad/VladSdkSession.cs` |
| RTSP Thread/Callback 호환 | `LegacyVlad/VLAD_Ops_RTSP.cs` |
| OpenCV Mat 변환 | `LegacyVlad/OpenCvSharpMatImage.cs` |
| detectData 해석 | `LegacyVlad/VladInferenceResultParser.cs` |
| detectText/bbox -> 측정값 변환 | `Services/VladMeasurementMapper.cs` |
| pixel-mm 보정값 | `Services/MeasurementCalibrationService.cs`, `CFG/Calibration.json` |
| Direct SDK 카메라 제어 | `ImvCamera/ImvCameraDevice.cs` |

## 2026-06-12 기준 상태

- `VLAD_Ops_Ai_Env_Start`는 원본 VLAD_Ops와 같은 분기 구조로 유지합니다.
- 카메라 RTSP 보조 Thread는 원본 VLAD_Ops처럼 이미 등록된 `Vlad_id`가 있을 때만 callback을 등록합니다. 기준 이미지 저장/카메라 캡처 중에는 VLAD 등록을 새로 만들지 않습니다.
- C# 단계에서 checkpoint-only 모델 구조를 먼저 차단하지 않고, SDK 등록 결과를 우선합니다. 모델 구조 문제는 `Debug.WriteLine` 진단으로 남깁니다.
- `VLAD_Warm_Up`, `VLAD_Unregistration`을 P/Invoke와 호환 어댑터에 노출했습니다.
- `detectText`에 길이/너비/높이/두께 값이 있으면 우선 사용합니다.
- bbox만 있고 보정값이 있으면 pixel 값을 mm로 변환합니다.
- 치수값을 확정할 수 없으면 기준값으로 위장하지 않고 `MeasurementUnavailable` 또는 `CalibrationMissing` 상태와 0값을 남겨 NG 판정이 나도록 합니다.

## 남은 확인

- AI 담당자가 제공할 최종 모델은 `checkpoint/ckpt/pipeline.config`만으로는 부족합니다. VLAD_SDK가 읽는 `nets_model.json + saved_model\saved_model.pb` 또는 `model.onnx/model.pt/model.t7` 구조가 필요합니다.
- VLAD detectData 안에 길이/너비/높이/두께가 어떤 형식으로 들어오는지 최종 스키마 확인이 필요합니다.
- 6대 카메라 동시 스트리밍은 실제 장비에서 장시간 부하 테스트가 필요합니다.
- RTSP Thread 종료 API는 원본 코드에도 명확하지 않습니다. SDK 담당자에게 Client unregister/stop 함수 존재 여부를 확인해야 합니다.
