# VLAD 기존 코드 기반 1~7 항목 추적표

## 2026-05-30 검토 결론

1~7 항목 중 상당수는 `VLAD Source`에서 근거를 얻을 수 있습니다. 다만 기존 코드는 WinForms, static 전역 상태, 단일 카메라 중심 샘플, 하드코딩 DLL 경로가 섞여 있어서 그대로 복사하면 6대 카메라 운영과 WPF/MVVM 구조에 맞지 않습니다.

현재 방향은 새 구조를 유지하되, 기존 담당자가 아는 이름을 `Compat` 계층에 남겨서 기존 코드와 현재 코드가 서로 추적되도록 하는 것입니다.

## 1~7 항목별 판단

| 번호 | 항목 | VLAD 기존 코드에서 얻을 수 있는 정보 | 현재 프로젝트 반영 위치 | 남은 작업 |
| ---: | --- | --- | --- | --- |
| 1 | 카메라 연결 구조 | `Camera_Control.Open_Cam`, `VLAD_Ops_imvCam_IMV_Open`에서 Enum/CreateHandle/Open/Start 순서 확인 가능 | `ImvCameraDevice`, `ImvCameraManager`, `Camera_Control` 호환 클래스 | 실제 DC-T3145G/R SDK DLL 연결 |
| 2 | 6대 카메라와 위치 매핑 | 기존 `cam_name`, `key` 기반 처리 흐름은 참고 가능하지만 6방향 고정 구조는 없음 | `CameraChannelConfig`, `ImageViewType`, 옵션 UI | Serial/IP/DeviceUserId를 Top/Front/Back/Left/Right/Thickness에 현장 매핑 |
| 3 | 연속 영상 수신 | `VLAD_Ops_imvCam_Thread`, `IMV_GetFrame`, `IMV_ReleaseFrame`, `Camera_Control.onGetFrame`에서 thread/callback 흐름 확인 가능 | `VisionCameraCaptureWorker`, 향후 `VisionCameraReceiveWorker` | 최신 프레임 유지형 receive worker 추가 |
| 4 | 트리거/캡처 | IMV 샘플의 `TriggerMode`, `TriggerSource`, `TriggerSoftware`, `Line1` 설정 참고 가능 | `ImvFunctionAdapter.IMV_SetEnumFeatureSymbol`, `IMV_ExecuteCommandFeature` | 현장 트리거 방식 결정 후 실제 SDK 호출 구현 |
| 5 | AI/VLAD 등록과 추론 | `VLAD_Ops_Ai_Env_Start`, `VLAD_Registration`, `VLAD_Ops_Inference_Registration`, `VLAD_Inference_Mat` 순서 확인 가능 | `VLAD_Ops_Ai_Compat`, `VladFunctionAdapter`, `VladRuntimeContext` | 실제 VLAD_SDK.dll x64 호출과 모델 경로 검증 |
| 6 | AI 결과 파싱 | `VLAD_InferenceData_V1_Draw`, `VLAD_InferenceData_V2_Draw`, `VLAD_InferenceData_Get_Valid_Count`, class string/color 함수 확인 가능 | `VladNativeMethods`, `VladRuntimeContext` 파서 자리 | detect data를 `VisionInspectionOutput.Measurements`로 변환 |
| 7 | 측정값/단위/보정 | 기존 코드는 bbox/mask/class 결과 처리 중심이며 pixel-to-mm 보정 로직은 확인되지 않음 | `VisionMeasurementValue.RawPixelValue`, Application 단위 변환 | mmPerPixel, homography, 보정판 절차 신규 설계 필요 |

## 기존 이름을 유지한 현재 파일

| 기존 담당자가 찾을 이름 | 현재 파일 |
| --- | --- |
| `VLAD_Ops_Ai_Env_Start` | `LegacyVlad/VLAD_Ops_Ai_Compat.cs` |
| `VLAD_Registration` | `LegacyVlad/VladFunctionAdapter.cs`, `LegacyVlad/VladNativeMethods.cs` |
| `VLAD_Ops_Inference_Registration` | `LegacyVlad/VladFunctionAdapter.cs`, `LegacyVlad/VladNativeMethods.cs` |
| `VLAD_Inference_Mat` | `LegacyVlad/VladFunctionAdapter.cs`, `LegacyVlad/VladRuntimeContext.cs` |
| `VLAD_InferenceData_V1_Draw` | `LegacyVlad/VladFunctionAdapter.cs`, `LegacyVlad/VladNativeMethods.cs` |
| `VLAD_InferenceData_V2_Draw` | `LegacyVlad/VladFunctionAdapter.cs`, `LegacyVlad/VladNativeMethods.cs` |
| `VLAD_Ops_imvCam_Thread` | `ImvCamera/VLAD_Ops_imvCam.cs` |
| `VLAD_Ops_imvCam_IMV_Open` | `ImvCamera/VLAD_Ops_imvCam.cs` |
| `Cam_Proc` | `ImvCamera/VLAD_Ops_imvCam.cs` |
| `Open_Cam` | `ImvCamera/Camera_Control.cs` |
| `Close_Cam` | `ImvCamera/Camera_Control.cs` |
| `Is_Open` | `ImvCamera/Camera_Control.cs` |

## 담당자에게 설명할 기준

- 기존 코드를 그대로 복사하는 방식은 피합니다. 6대 카메라, WPF UI, DB 기준정보, 이력 저장 구조와 충돌할 수 있습니다.
- 기존 함수명은 검색 가능하게 유지합니다. AI/카메라 담당자는 기존 함수명을 검색해서 현재 구현 위치로 들어오면 됩니다.
- 실제 SDK 호출은 `ImvCameraDevice`, 실제 VLAD 호출은 `VladRuntimeContext` 안에 넣습니다.
- 앱과의 연결은 `ICameraService`, `IAiInferenceService`를 통해 유지합니다. Vision 프로젝트에서 WPF 화면을 직접 만지지 않습니다.
