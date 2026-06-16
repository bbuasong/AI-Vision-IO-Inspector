# Vision 구현 체크리스트

## 2026-06-12 기준 Job Flow

```text
검사 시작
  -> MainWindowViewModel.ExecuteRunInspection
  -> InspectionWorkflowService.RunInspection
  -> ICameraService.CaptureAll
  -> IAiInferenceService.Inspect
  -> VisionInferenceWorker
  -> VladVisionInferenceEngine
  -> VladSdkSession.EnsureStarted
  -> VLAD_Ops_Ai_Env_Start
  -> VLAD_Inference_Mat
  -> VladInferenceResultParser
  -> VladMeasurementMapper
  -> MeasurementService / JudgmentService
  -> 이력 저장 및 UI 갱신
```

`ApplyInspectionPartContext`, `LoadCapturedImages`, `LoadInspectionMeasurements`, `LoadEvents`는 AI로 넘어가는 지점이 아니라, 검사 완료 후 결과를 UI에 반영하는 단계입니다.

## 완료

| 항목 | 상태 | 위치 |
| --- | --- | --- |
| Vision 전용 프로젝트 분리 | 완료 | `AI.Vision.IOInspector.Vision` |
| App과 Vision 연결 | 완료 | `VisionRuntimeFactory`, `VisionCameraService`, `VisionAiInferenceService` |
| 검사 추론 전용 Worker Thread | 완료 | `Threading/VisionInferenceWorker.cs` |
| 카메라 촬영 요청 Worker Thread | 완료 | `Threading/VisionCameraCaptureWorker.cs` |
| Direct SDK 카메라 래퍼 | 완료 | `ImvCamera/ImvCameraDevice.cs` |
| RTSP/NVR 설정 기반 URL 생성 | 완료 | `RtspUrlBuilder.Build` |
| 실제 프레임 수신 기준 연결 상태 판정 | 완료 | `ConfiguredCameraService`, `VisionCameraCoordinator` |
| `MVSDK_Net.dll` 참조 | 완료 | `AI.Vision.IOInspector.Vision.csproj` |
| `OpenCvSharp*.dll` 참조 | 완료 | `AI.Vision.IOInspector.Vision.csproj` |
| `VLAD_SDK.dll`, `VLAD_Ctrl.dll`, `plugins` 배포 포함 | 완료 | `Native/VLAD`, App csproj Copy 설정 |
| `CFG/Config.json` 실행 폴더 복사 | 완료 | App csproj Copy 설정 |
| `VLAD_Ops_Ai_Env_Start` 호환 함수 | 완료 | `LegacyVlad/VLAD_Ops_Ai.cs` |
| `VLAD_Custom_Registration` P/Invoke | 완료 | `LegacyVlad/VladNativeMethods.cs` |
| `VLAD_Inference_Mat` 호출 경로 | 완료 | `Engines/VladVisionInferenceEngine.cs` |
| `VLAD_InferenceData_V1_Draw`, `V2_Draw` 호출 경로 | 완료 | `LegacyVlad/VladInferenceResultParser.cs` |
| `VLAD_Rtsp_Info_Client_Registration` 호환 Thread | 보강 완료 | `LegacyVlad/VLAD_Ops_RTSP.cs` |
| `VLAD_Warm_Up`, `VLAD_Unregistration` 노출 | 완료 | `VladNativeMethods`, `VLAD_Ops_Ai`, `VladSdkSession` |
| 카메라/추론 공유 VLAD_ID 보관 | 완료 | `LegacyVlad/VladSdkSession.cs` |
| 모델 경로 진단 | 완료 | `LegacyVlad/VladModelPathInspector.cs` |
| detectText/bbox -> 측정값 매핑 | 부분완료 | `Services/VladMeasurementMapper.cs` |
| pixel-mm 보정 파일 구조 | 부분완료 | `CFG/Calibration.json`, `MeasurementCalibrationService.cs` |

## 이번에 정리한 과도한 예외 처리

- 원본 VLAD_Ops는 C#에서 `Directory.Exists(modelPath)`나 SavedModel 구조를 먼저 검사해 실행을 막지 않습니다.
- 현재 코드도 checkpoint-only 여부로 먼저 throw하지 않도록 수정했습니다.
- 대신 `VladModelPathInspector.BuildDiagnosticMessage`가 문제 원인을 로그로 남기고, 실제 성공/실패는 `VLAD_Custom_Registration` 반환값과 SDK 내부 로딩 결과를 따릅니다.
- 단, `MODEL` 설정이 아예 비어 있는 경우는 앱 설정 오류이므로 기존처럼 사용자에게 설정 위치를 안내합니다.
- 기준 이미지 저장과 일반 RTSP 캡처 중에는 `VLAD_Ops_Ai_Env_Start`를 호출하지 않습니다. 원본 `VLAD_Ops_RTSP_Thread`처럼 이미 등록된 VLAD_ID가 있을 때만 RTSP callback을 등록합니다.

## 남은 미구현/외부 확인

| ID | 항목 | 상태 | 필요한 결정 |
| --- | --- | --- | --- |
| V-001 | 최종 VLAD 모델 export | 외부정보필요 | `checkpoint/ckpt/pipeline.config`를 VLAD_SDK 추론 모델 구조로 변환해야 합니다. |
| V-002 | detectData 치수 스키마 | 외부정보필요 | AI 담당자가 길이/너비/높이/두께 값을 어떤 필드/문자열로 반환할지 확정해야 합니다. |
| V-003 | pixel-mm 보정 절차 | 내부작업+외부확인 | 카메라별 mm/pixel 또는 보정판 절차를 정해야 합니다. |
| V-004 | RTSP Thread 종료 API | 외부정보필요 | VLAD SDK에 client unregister/stop API가 있는지 확인해야 합니다. |
| V-005 | 6채널 장시간 스트리밍 | 검증필요 | 실제 장비 6대 연결 후 CPU/메모리/핸들 누수 테스트가 필요합니다. |
| V-006 | 기준 이미지와 AI 비교 정책 | 외부정보필요 | 기준 이미지 자체를 AI가 직접 비교하는지, 현재 카메라 이미지에서 치수/불량만 판단하는지 확정해야 합니다. |
| V-007 | 두께 다중 측정 확장 | 범위확인 | 현재 UI/DB는 기본 1세트 중심입니다. 두께 복수 항목 요구가 확정되면 모델 확장이 필요합니다. |

## 현재 판정 방식

- VLAD 추론 실패 시 `IsSuccess=false`로 반환하고 검사 결과는 Error로 처리합니다.
- VLAD가 치수값을 직접 주면 해당 값을 사용합니다.
- bbox만 있으면 보정값이 있는 경우에만 mm 값으로 변환합니다.
- 치수값을 확정할 수 없으면 기준값을 채우지 않습니다. `MeasurementUnavailable` 또는 `CalibrationMissing`을 남기고 측정값은 0으로 전달해 NG가 나도록 합니다.

## 주의

- `test2_20240508_2_checkpoint`는 프로그램이 자동 생성하는 파일이 아닙니다.
- 현재 프로젝트에는 학습/export 코드가 없습니다. 기준 이미지를 저장한다고 checkpoint가 생성되지는 않습니다.
- `VLAD Source` 원본 폴더는 GitHub 업로드 대상에서 제외합니다.
- 벤더 DLL과 VLC `plugins`는 개발/배포에는 필요하지만, 라이선스와 용량 정책을 확인한 뒤 공유 방식을 정해야 합니다.

## 2026-06-16 Job Flow 보강

- 검사 시작 후 AI 추론은 WPF 본체가 아니라 AI.Vision.IOInspector.VisionWorker.exe 별도 프로세스에서 실행합니다.
- WPF 흐름은 MainWindowViewModel -> InspectionWorkflowService -> ICameraService.CaptureAll -> ProcessIsolatedAiInferenceService -> VisionWorker -> VladVisionInferenceEngine -> 응답 JSON -> MeasurementService/JudgmentService 순서입니다.
- 워커가 네이티브 오류로 종료되면 WPF 본체는 종료되지 않고 Error 결과와 Event 로그를 표시합니다.
- 기준 이미지 누락은 업무 규칙상 검사 전 확인합니다. 기준 이미지를 저장한 뒤 다시 검사 시작하면 됩니다.
- 실제 확인 결과 워커 초기화 경로에서 ExitCode=-1073740791과 cudart64_110.dll 경고가 확인되었습니다. 이는 앱 종료 방지와 별개로 VLAD 런타임 구성 확인이 필요한 남은 항목입니다.
