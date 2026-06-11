# AI.Vision.IOInspector.Vision

이 프로젝트는 카메라 수신, VLAD SDK 연결, AI 추론, 측정값 변환을 담당하는 전용 영역입니다.
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
```

## AI 담당자 주요 진입점

| 목적 | 파일 |
| --- | --- |
| VLAD 등록/추론 시작 | `Engines/VladVisionInferenceEngine.cs` |
| 기존 VLAD_Ops 함수명 호환 | `LegacyVlad/VLAD_Ops_Ai.cs` |
| VLAD_SDK.dll P/Invoke | `LegacyVlad/VladNativeMethods.cs` |
| OpenCV Mat 변환 | `LegacyVlad/OpenCvSharpMatImage.cs` |
| detectData 해석 | `LegacyVlad/VladInferenceResultParser.cs` |
| detectText/bbox -> 측정값 변환 | `Services/VladMeasurementMapper.cs` |
| 픽셀-mm 보정값 | `Services/MeasurementCalibrationService.cs`, `CFG/Calibration.json` |
| Direct SDK 카메라 제어 | `ImvCamera/ImvCameraDevice.cs` |

## 현재 구현 상태

- `MVSDK_Net.dll`, `OpenCvSharp*.dll`, `VLAD_SDK.dll`, `VLAD_Ctrl.dll`, `MVSDKmd.dll` 참조와 배포 복사가 설정되어 있습니다.
- `VLAD_Ops_Ai_Env_Start`로 VLAD를 등록하고, 촬영 이미지를 OpenCV Mat로 읽어 `VLAD_Inference_Mat`을 호출합니다.
- `VLAD_InferenceData_V1_Draw`, `V2_Draw`, `VLAD_InferenceData_Get_Valid_Count`, `VLAD_Get_Class_Str`를 이용해 결과를 해석합니다.
- VLAD가 치수값을 직접 주면 `detectText`에서 길이/너비/높이/두께 값을 읽습니다.
- VLAD가 bbox만 주면 `CFG/Calibration.json`의 mm/pixel 보정값으로 mm 값을 계산합니다.
- 보정값이 없으면 임의 계산하지 않고 기준값 fallback과 `CalibrationMissing` 상태를 남깁니다.

## 남은 외부 검증

- 실제 설치 PC에서 `CFG/Config.json`의 `MODEL` 경로가 존재해야 합니다.
- AI 담당자가 실제 반환하는 detectData/detectText 치수 포맷을 확정해야 합니다.
- Top/Front/Back/Left/Right/Thickness별 mm/pixel 보정값을 현장에서 입력해야 합니다.
- 6대 카메라 동시 연속 스트리밍은 실제 장비로 장시간 부하 테스트가 필요합니다.

자세한 항목은 `Docs/vision-implementation-checklist.md`를 기준으로 관리합니다.
