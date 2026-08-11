# VLAD_Ops 구현 차이 분석

## 결론

2026-06-12 기준 현재 프로젝트는 기존 `VLAD_Ops`의 함수명과 주요 진입점을 대부분 따라갈 수 있게 구성되어 있습니다.
다만 원본 WinForms 프로그램을 그대로 이식한 상태는 아니며, WPF/MVVM 구조에 맞춰 UI 표시와 검사 워크플로우는 분리되어 있습니다.

현재 검사 흐름은 다음과 같습니다.

```text
검사 시작 버튼
  -> MainWindowViewModel.ExecuteRunInspection
  -> InspectionWorkflowService.RunInspection
  -> ICameraService.CaptureAll
  -> IAiInferenceService.Inspect
  -> VisionInferenceWorker
  -> VladVisionInferenceEngine
  -> VladSdkSession.EnsureStarted
  -> VLAD_Ops_Ai_Env_Start
  -> VLAD_Inference_Mat
  -> MeasurementService / JudgmentService
  -> 이력 저장 및 UI 갱신
```

`ApplyInspectionPartContext`, `LoadCapturedImages`, `LoadInspectionMeasurements`, `LoadEvents`, `RefreshHistory`, `RefreshStatistics`, `RefreshCameraStatuses`는 AI로 넘어가는 지점이 아닙니다. 검사 완료 후 결과를 화면과 이력에 다시 로드하는 UI 반영 단계입니다.

## 원본과 맞춘 부분

| 원본 VLAD_Ops 항목 | 현재 위치 | 상태 |
| --- | --- | --- |
| `VLAD_Ops_Ai_Env_Start` | `LegacyVlad/VLAD_Ops_Ai.cs` | 기존 분기 구조 유지 |
| `VLAD_Custom_ID_Generate` | `LegacyVlad/VladNativeMethods.cs` | P/Invoke 선언 |
| `VLAD_Custom_Registration` | `LegacyVlad/VladNativeMethods.cs` | P/Invoke 선언 |
| `VLAD_Inference_Mat` | `LegacyVlad/VLAD_Ops_Ai.cs`, `VladVisionInferenceEngine` | 호출 경로 구현 |
| `VLAD_InferenceData_V1_Draw` | `VladInferenceResultParser` | 호출 경로 구현 |
| `VLAD_InferenceData_V2_Draw` | `VladInferenceResultParser` | 호출 경로 구현 |
| `VLAD_InferenceData_Get_Valid_Count` | `VladInferenceResultParser`, `VLAD_Ops_RTSP` | 호출 경로 구현 |
| `VLAD_Rtsp_Info_Client_Registration` | `LegacyVlad/VLAD_Ops_RTSP.cs` | Thread/Callback 진입점 구현 |
| `VLAD_Warm_Up` | `VladNativeMethods`, `VladSdkSession` | 2026-06-12 추가 |
| `VLAD_Unregistration` | `VladNativeMethods`, `VladSdkSession` | 2026-06-12 추가 |
| `MVSDK_Net`/`OpenCvSharp` 참조 | `AI.Vision.IOInspector.Vision.csproj` | Visual Studio에서 확인 가능 |

## 원본과 다른 부분

| 항목 | 원본 VLAD_Ops | 현재 프로젝트 | 이유 |
| --- | --- | --- | --- |
| UI 표시 | RTSP callback에서 WinForms PictureBox 직접 갱신 | WPF ViewModel이 서비스 결과를 표시 | UI Thread 안정성과 MVVM 유지 |
| VLAD_ID 관리 | static/global 필드 사용 | `VladSdkSession` 공유 객체 사용 | 원본처럼 등록된 핸들을 보관하고 필요한 곳에 전달 |
| RTSP Thread 시작 | 이미 만들어진 `Vlad_id`를 ThreadParam으로 전달 | `CurrentVladId`가 있을 때만 callback 등록 | 기준 이미지 저장 중 VLAD 등록을 새로 만들지 않음 |
| 모델 경로 선검사 | C#에서 선차단 없음 | 진단 로그만 남김 | 원본 흐름 유지, 과도한 예외 제거 |
| 측정값 없을 때 | 원본은 측정값 업무가 명확하지 않음 | 기준값 fallback 금지, 0값으로 NG 유도 | 실제 측정 실패를 OK처럼 보이지 않게 하기 위함 |
| RTSP Thread 종료 | 명확한 stop API 확인 어려움 | 시작만 구현, 종료는 미확정 | SDK 담당자 확인 필요 |

## 모델 경로 분석

메일로 받은 `test2_20240508_2_checkpoint` 폴더에는 다음 파일이 있습니다.

```text
checkpoint
ckpt-0.data-00000-of-00001
ckpt-0.index
pipeline.config
```

원본 `VLAD_SDK - Rev3`의 `Get_Model_Selection` 흐름은 다음 구조를 찾습니다.

```text
nets_model.json + saved_model\saved_model.pb
model.onnx
model.pt
model.t7
```

즉 현재 받은 checkpoint-only 폴더는 학습/export 중간 산출물로 보이며, VLAD_SDK가 곧바로 추론 모델로 읽는 구조는 아닙니다.
현재 코드는 이 문제를 C#에서 먼저 막지 않고 `Debug.WriteLine` 진단으로만 남긴 뒤, SDK 등록 결과를 따릅니다.

## 검사 시작 오류의 대표 원인

1. `MODEL` 경로가 비어 있음
   - 앱 설정 오류입니다. `CFG/Config.json` 또는 `AI_VISION_VLAD_MODEL_PATH`를 설정해야 합니다.

2. `MODEL` 경로가 checkpoint-only 구조임
   - C#은 더 이상 선차단하지 않지만, VLAD_SDK 내부 등록이 실패할 가능성이 높습니다.
   - AI 담당자가 추론 모델 구조로 export해야 합니다.

3. 네이티브 DLL 누락
   - `VLAD_SDK.dll`뿐 아니라 `plugins`, OpenCV, TensorFlow/ONNX/VLC 계열 DLL이 함께 있어야 합니다.

4. RTSP 인증/권한 문제
   - 카메라/NVR 프레임 수신이 되지 않으면 검사 이미지가 없거나 최신 영상이 갱신되지 않습니다.

5. detectData 치수 스키마 미확정
   - AI가 길이/너비/높이/두께를 어떤 형식으로 반환하는지 정해지지 않으면 측정값은 `MeasurementUnavailable` 또는 `CalibrationMissing`으로 남습니다.

## AI 담당자가 우선 볼 파일

- `LegacyVlad/VLAD_Ops_Ai.cs`
- `LegacyVlad/VLAD_Ops_RTSP.cs`
- `LegacyVlad/VladNativeMethods.cs`
- `LegacyVlad/VladSdkSession.cs`
- `Engines/VladVisionInferenceEngine.cs`
- `Services/VladMeasurementMapper.cs`
- `Models/VisionInspectionInput.cs`
- `Models/VisionInspectionOutput.cs`

## 아직 부족한 부분

| 항목 | 상태 | 다음 작업 |
| --- | --- | --- |
| 최종 추론 모델 | 외부정보필요 | checkpoint를 SavedModel/ONNX/PT/T7로 export |
| 치수 반환 스키마 | 외부정보필요 | AI 담당자가 detectText 또는 TLV 구조 확정 |
| 카메라별 보정값 | 내부작업+외부확인 | `Calibration.json` 실제 mm/pixel 입력 |
| RTSP Thread stop | 외부정보필요 | SDK 종료 API 존재 여부 확인 |
| 6채널 부하 검증 | 검증필요 | 실제 장비로 장시간 테스트 |
