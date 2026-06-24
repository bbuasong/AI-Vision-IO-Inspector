# Vision Implementation Checklist

기준일: 2026-06-22

Vision 영역은 카메라 수신, VLAD SDK 초기화, AI 추론 결과 변환, 측정값 보정까지 담당합니다. UI/DB 로직을 직접 수정하지 않고 Application 인터페이스 뒤에서 교체할 수 있어야 합니다.

## 현재 구현 흐름

```text
App 시작
  -> RuntimeAssemblyResolver.Register
  -> VisionRuntimeFactory.InitializeVladRuntimeOnStartup
  -> VladCamModeRuntime.EnsureLoaded
  -> VladSdkSession.EnsureStarted
  -> VLAD_Ops_Ai_Env_Start
  -> VLAD_Custom_Registration

검사 시작
  -> InspectionWorkflowService
  -> VisionCameraService.CaptureAll
  -> VisionAiInferenceService.Inspect
  -> VisionInferenceWorker.Inspect
  -> VladVisionInferenceEngine.Inspect
  -> VLAD_Inference_Mat / VLAD_Custom_InferenceData_V1
  -> VladInferenceResultParser
  -> VladMeasurementMapper
  -> MeasurementService / JudgmentService
```

`AI.Vision.IOInspector.VisionWorker` 프로젝트는 남아 있지만, 2026-06-22 기준 WPF 기본 실행 경로에서는 사용하지 않습니다. 진단/레거시 용도로 유지할지 삭제할지는 AI 담당자 확인 후 결정합니다.

## 구현 상태

| ID | 항목 | 상태 | 근거/파일 |
| --- | --- | --- | --- |
| V-001 | CAM 모드 명시 초기화 | 완료 | `VladCamModeRuntime`, `VladSdkSession`, `VLAD_Ops_Ai_Env_Start` |
| V-002 | `VladId` 재사용 | 완료 | `VladSdkSession.EnsureStarted`, `VladCamModeState` |
| V-003 | `CFG\Config.json` 설정 기준 | 완료 | `VladVisionSettings`, `CameraConfigurationStore` |
| V-004 | `RuntimeData\Camera\camera-config.json` 제거 | 완료 | `open-items.md` C-006 |
| V-005 | RTSP/카메라 서비스 경계 | 부분완료 | `VisionCameraService`, `VisionCameraCoordinator`, 실제 6채널 장시간 검증 필요 |
| V-006 | AI 추론 전용 스레드 | 완료 | `VisionInferenceWorker` |
| V-007 | VLAD 추론 호출 | 부분완료 | `VladVisionInferenceEngine`, 최종 모델/런타임 검증 필요 |
| V-008 | 결과 파싱 | 부분완료 | `VladInferenceResultParser`, 실제 `detectData` 스키마 필요 |
| V-009 | 측정값 변환 | 부분완료 | `VladMeasurementMapper`, `MeasurementCalibrationService`, pixel-mm 보정 필요 |
| V-010 | 기준 이미지 인셋/판정 테두리 | 완료-검증필요 | `RtspVideoHost`, `ImageSlotViewModel`, 실제 UI 재확인 필요 |
| V-011 | 기준 이미지 저장 | 완료-검증필요 | 현재 카메라 캡처 기반 저장, 실제 6채널 확인 필요 |
| V-012 | 검사 이미지 History 저장 | 완료-검증필요 | `DB\History\yyyyMMdd\HH\분류코드` 구조 |
| V-013 | Native DLL 탐색 | 완료 | `RuntimeAssemblyResolver`, `NativeDependencyLoader` |
| V-014 | 배포 출력 복사 | 완료-검증필요 | `App.csproj` `CopyAppRuntimeFoldersToOutput` |
| V-015 | 외부 CUDA/cuDNN/VC Runtime | 미완료 | 배포 PC 확인 필요 |

## AI 담당자 확인 필요

| ID | 질문 | 이유 |
| --- | --- | --- |
| QV-001 | VLAD 최종 모델 폴더 구조는 무엇인가? | checkpoint-only 구조로는 `VLAD_Custom_Registration` 성공을 보장할 수 없습니다. |
| QV-002 | `VLAD_Custom_InferenceData_V1`의 `detectText`, TLV, classList 구조는 어떻게 해석해야 하는가? | 측정값과 카메라별 Pass/Fail 변환에 필요합니다. |
| QV-003 | `VLAD_Rtsp_Info_Client_Registration`을 현재 흐름에서 반드시 호출해야 하는 시점은 언제인가? | Sample_VLAD_SDK 흐름과 현재 캡처/검사 흐름을 맞추기 위해 필요합니다. |
| QV-004 | RTSP Thread 종료/해제 API가 있는가? | 검사 반복/프로그램 종료 시 리소스 누수 방지에 필요합니다. |
| QV-005 | GPU ID 기본값은 0인가 1인가? | Sample/메일/현장 GPU 구성 기준을 통일해야 합니다. |

## 실제 장비 검증 체크

- 6대 카메라가 `CFG\Config.json` 순서대로 매핑되는지 확인.
- Top/Front/Back/Left/Right/Thickness 기준 이미지 저장이 실제 현재 프레임으로 저장되는지 확인.
- 기준 이미지가 없는 품목에서 안내 메시지 후 계속 진행/등록 흐름이 맞는지 확인.
- 검사 시작 100회 반복 시 앱 종료, 스레드 누수, 파일 잠금이 없는지 확인.
- 검사 후 `DB\History` 파일명과 SQLite History 레코드가 같은 검사 시간을 가리키는지 확인.
- `DB\Logs\vlad-startup.log`, `vlad-registration.log`, `vlad-rtsp.log`가 실제 문제 분석에 충분한지 확인.

## 제거/정리된 항목

| 날짜 | 내용 |
| --- | --- |
| 2026-06-19 | `VLAD_Ops_Ai_Compat`, `VladFunctionAdapter`, `VladRuntimeContext` 제거 |
| 2026-06-22 | `ProcessIsolatedAiInferenceService`, `VladStartupInitializationService` 제거 |
| 2026-06-22 | `SampleVladSdkRuntime`, `SimulatedVisionInferenceEngine`, `VisionCameraReceiveWorker` 등 미사용 Vision 코드 제거 |
| 2026-06-22 | WPF 기본 흐름을 `VisionWorker.exe` 격리 프로세스가 아닌 `VisionInferenceWorker` 스레드 기준으로 문서 정리 |
