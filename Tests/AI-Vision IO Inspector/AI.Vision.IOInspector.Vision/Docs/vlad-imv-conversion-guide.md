# VLAD/IMV 변환 가이드

## 2026-05-30 목적

이 문서는 기존 `VLAD Source`를 알고 있는 담당자가 현재 `AI.Vision.IOInspector.Vision` 프로젝트 안에서 어디를 구현해야 하는지 빠르게 찾기 위한 대응표입니다.

중요 원칙은 다음과 같습니다.

- 기존 WinForms UI, static 전역 상태, `D:\DevTools\...` 하드코딩 경로는 그대로 가져오지 않습니다.
- 기존 함수명은 `LegacyVlad`, `ImvCamera` Adapter에 남겨 검색 가능하게 합니다.
- 실제 앱과의 연결은 `ICameraService`, `IAiInferenceService` 인터페이스를 통해서만 합니다.
- 해상도, stride, pixel format, 측정 단위, raw pixel 값은 `VisionFrame`, `VisionMeasurementValue`에 명시적으로 담습니다.

## 기존 코드 위치

| 기존 자료 | 주요 역할 | 변환 대상 |
| --- | --- | --- |
| `VLAD_Ops_Ai.cs` | VLAD DLL P/Invoke, 모델 등록, 추론, 결과 draw/info | `LegacyVlad`, `VisionAiInferenceService`, `IVisionInferenceEngine` |
| `VLAD_SDK.cpp`, `VLAD_SDK.h` | C++ VLAD exported API 원형 | `VladNativeMethods`, `VladRuntimeContext` |
| `Camera_Control.cs` | IMV 카메라 open/start/callback/close 흐름 | `ImvCameraDevice`, `VisionCameraCoordinator` |
| `Camera/C#/IMV/BasicDemo` | receive thread, render thread, GetFrame/ReleaseFrame | 향후 `VisionCameraReceiveWorker` |
| `Camera/C#/IMV/*SoftwareTrigger*` | TriggerMode/TriggerSource/TriggerSoftware | `ImvFunctionAdapter.IMV_SetEnumFeatureSymbol`, `IMV_ExecuteCommandFeature` |
| `VLAD_SDK_Rtsp.*` | RTSP/NVR 스트림 수신 | 향후 `RtspCameraReceiveWorker` |

## 현재 Vision 프로젝트 구조

```text
AI.Vision.IOInspector.Vision
  VisionRuntimeFactory.cs
  Engines
    IVisionInferenceEngine.cs
    SimulatedVisionInferenceEngine.cs
  Services
    VisionCameraService.cs
    VisionCameraCoordinator.cs
    VisionAiInferenceService.cs
  Threading
    VisionInferenceWorker.cs
    VisionInferenceRequest.cs
  Models
    VisionFrame.cs
    VisionInspectionInput.cs
    VisionInspectionOutput.cs
    VisionMeasurementValue.cs
  LegacyVlad
    VLAD_Ops_Ai_Compat.cs
    VladFunctionAdapter.cs
    VladRuntimeContext.cs
    VladNativeMethods.cs
  ImvCamera
    VLAD_Ops_imvCam.cs
    Camera_Control.cs
    ImvFunctionAdapter.cs
    ImvCameraManager.cs
    ImvCameraDevice.cs
```

## 기존 함수명 대응표

| 기존 VLAD/IMV 함수 | 현재 Adapter | 실제 구현 위치 | 비고 |
| --- | --- | --- | --- |
| `VLAD_Ops_Ai_Env_Start` | `VLAD_Ops_Ai_Compat.VLAD_Ops_Ai_Env_Start` | `VladFunctionAdapter`, `VladRuntimeContext` | 기존 담당자 진입용 호환 함수 |
| `VLAD_Registration` | `VladFunctionAdapter.VLAD_Registration` | `VladRuntimeContext.Register` | `VladNativeMethods.VLAD_Registration` 호출 예정 |
| `VLAD_Ops_Inference_Registration` | `VladFunctionAdapter.VLAD_Ops_Inference_Registration` | `VladRuntimeContext.RegisterInferenceModel` | 모델 경로, site, gpu id 관리 |
| `VLAD_Inference_Mat` | `VladFunctionAdapter.VLAD_Inference_Mat` | `VladRuntimeContext.Inference` | OpenCV Mat 또는 대체 frame buffer 변환 필요 |
| `VLAD_InferenceData_Get_Valid_Count` | `VladFunctionAdapter.VLAD_InferenceData_Get_Valid_Count` | `VladRuntimeContext.Inference` 결과 파싱 단계 | detect data 구조 확정 필요 |
| `VLAD_InferenceData_V1_Draw` | `VladFunctionAdapter`, `VladNativeMethods` | `VladRuntimeContext.DrawInferenceDataV1` | bbox/class/mask/문자열 변환 |
| `VLAD_InferenceData_V2_Draw` | `VladFunctionAdapter`, `VladNativeMethods` | `VladRuntimeContext.DrawInferenceDataV2` | V2 메시지 포맷 확인 필요 |
| `VLAD_Ops_imvCam_Thread` | `VLAD_Ops_imvCam.VLAD_Ops_imvCam_Thread` | `VisionCameraCaptureWorker`, 향후 `VisionCameraReceiveWorker` | 기존 thread 구조 대응 |
| `VLAD_Ops_imvCam_IMV_Open` | `VLAD_Ops_imvCam.VLAD_Ops_imvCam_IMV_Open` | `ImvCameraDevice.OpenDevice`, `StartGrabbing` | 기존 open/start 순서 대응 |
| `Cam_Proc` | `VLAD_Ops_imvCam.Cam_Proc` | `VisionAiInferenceService`, `IVisionInferenceEngine` | 프레임별 AI 추론/집계 위치 |
| `Open_Cam` | `Camera_Control.Open_Cam` | `ImvCameraDevice.OpenDevice`, `StartGrabbing` | 기존 Camera_Control 대응 |
| `Close_Cam` | `Camera_Control.Close_Cam` | `ImvCameraDevice.StopGrabbing`, `CloseDevice` | 종료 순서 대응 |
| `Is_Open` | `Camera_Control.Is_Open` | `ImvCameraDevice.IsOpen` | 연결 상태 확인 |
| `IMV_EnumDevices` | `ImvFunctionAdapter.IMV_EnumDevices` | `ImvCameraManager.EnumDevices` | Serial/IP/DeviceUserID 기반 매칭 |
| `IMV_CreateHandle` | 구현 예정 | `ImvCameraDevice.OpenDevice` | .NET 9 호환 wrapper 결정 필요 |
| `IMV_Open` / `IMV_OpenDevice` | `ImvFunctionAdapter.IMV_OpenDevice` | `ImvCameraDevice.OpenDevice` | 연결 실패/타임아웃 기록 |
| `IMV_StartGrabbing` | `ImvFunctionAdapter.IMV_StartGrabbing` | `ImvCameraDevice.StartGrabbing` | callback 또는 polling 방식 선택 |
| `IMV_SetBufferCount` | `ImvFunctionAdapter.IMV_SetBufferCount` | `ImvCameraDevice.SetBufferCount` | 기존 예제는 8개 버퍼 사용 |
| `IMV_GetFrame` | `ImvFunctionAdapter.IMV_GetFrame` | `ImvCameraDevice.GetFrame` | `VisionFrame`으로 변환 |
| `IMV_ReleaseFrame` | `ImvFunctionAdapter.IMV_ReleaseFrame` | `ImvCameraDevice.ReleaseFrame` | 버퍼 소유권 해제 필수 |
| `IMV_StopGrabbing` | `ImvFunctionAdapter.IMV_StopGrabbing` | `ImvCameraDevice.StopGrabbing` | 종료 시 반드시 호출 |
| `IMV_Close` / `IMV_DestroyHandle` | `ImvFunctionAdapter.IMV_CloseDevice` | `ImvCameraDevice.CloseDevice` | 앱 종료/재연결 시 호출 |
| `IMV_SetEnumFeatureSymbol` | `ImvFunctionAdapter.IMV_SetEnumFeatureSymbol` | `ImvCameraDevice` 설정 메소드 추가 예정 | TriggerMode, PixelFormat 등 |
| `IMV_ExecuteCommandFeature` | `ImvFunctionAdapter.IMV_ExecuteCommandFeature` | `ImvCameraDevice` 설정 메소드 추가 예정 | TriggerSoftware 등 |

## 변환 순서

1. `VladNativeMethods`에 기존 `VLAD_Ops_Ai.cs`의 P/Invoke를 옮깁니다.
2. `VladRuntimeContext.Register`에서 `VLAD_Registration` 호출 결과를 `VladId`에 보관합니다.
3. `VladRuntimeContext.RegisterInferenceModel`에서 모델 경로, GPU 번호, custom info를 등록합니다.
4. 카메라에서 받은 `VisionFrame`을 VLAD가 요구하는 입력 형식으로 변환합니다.
5. `VladRuntimeContext.Inference`에서 `VLAD_Inference_Mat` 또는 파일 기반 추론을 호출합니다.
6. VLAD detect data를 `VisionInspectionOutput.Measurements`로 변환합니다.
7. 측정값은 `VisionMeasurementValue.Value`, 단위는 `VisionMeasurementValue.Unit`, raw pixel 값은 `RawPixelValue`에 넣습니다.
8. 앱의 `MeasurementService`가 DB 기준 단위로 변환하고 OK/NG를 판정합니다.

## 카메라 변환 순서

1. `ImvCameraManager.EnumDevices`에 `IMV_EnumDevices`를 구현합니다.
2. 검색된 카메라를 `CameraChannelConfig`의 `SerialNo`, `IpAddress`, `DeviceUserId`, `ViewType`과 매칭합니다.
3. `ImvCameraDevice.OpenDevice`에서 핸들 생성과 open을 수행합니다.
4. `StartGrabbing`에서 frame callback 또는 receive thread를 시작합니다.
5. `GetFrame`은 SDK frame을 `VisionFrame`으로 변환합니다.
6. `ReleaseFrame`은 SDK 버퍼 반환을 반드시 수행합니다.
7. `VisionCameraCoordinator`는 Top/Front/Back/Left/Right/Thickness 6채널 상태를 모읍니다.

## 해상도와 단위 기준

현재 카메라 사양 기준입니다.

| View 후보 | 모델 | 기본 해상도 | 비고 |
| --- | --- | ---: | --- |
| Top | DC-T3145G | 2448 x 2048 | Global Shutter 후보 |
| Thickness | DC-T3145G | 2448 x 2048 | Global Shutter 후보 |
| Front/Back/Left/Right | DC-T3145R | 2592 x 1944 | Rolling Shutter 후보 |

AI/Vision 내부 기준 단위는 `mm`를 우선 사용합니다. AI가 `cm` 또는 `m`으로 값을 반환하면 Application 계층에서 `mm/cm/m` 범위 변환을 수행합니다. pixel 값만 있는 경우에는 `CalibrationId`, `RawPixelValue`, `mmPerPixel` 또는 homography 보정값이 필요합니다.

## 2026-05-31 현재 미구현 항목

상위 추적 ID는 `Docs/03-development/open-items.md`를 기준으로 한다.

| 항목 | 상태 | 담당자가 구현할 위치 | 추적 ID |
| --- | --- | --- | --- |
| 실제 IMV SDK DLL 참조 | 미구현-외부정보필요 | `ImvCameraDevice`, `ImvCameraManager` | `O-001` |
| 카메라 연속 수신 worker | 진행중 | `VisionCameraReceiveWorker` 뼈대 추가. 다음은 `VisionCameraCoordinator` 연결 | `O-002` |
| RTSP/NVR 수신 | 보류-범위조정 | NVR은 녹화/모니터링 보조. 측정 원본 Direct SDK 우선 | `O-001` |
| 트리거 설정 | 미구현-외부정보필요 | `ImvFunctionAdapter.IMV_SetEnumFeatureSymbol`, `IMV_ExecuteCommandFeature` | `O-003` |
| VLAD 모델 등록 | 미구현-외부정보필요 | `VladRuntimeContext.RegisterInferenceModel` | `O-006` |
| VLAD 추론 호출 | 미구현-외부정보필요 | `VladRuntimeContext.Inference` | `O-006` |
| detect data 파싱 | 미구현-외부정보필요 | `VladRuntimeContext` 또는 별도 parser | `O-007` |
| pixel -> mm 보정 | 미구현-내부작업 | 향후 `Calibration` 폴더 | `O-004` |
| 카메라 Option UI | 완료 | App/ViewModel + Camera config 저장소 | 유지 |

## 담당자 작업 시 주의점

- x86 DLL은 현재 `win-x64` 앱에 직접 섞을 수 없습니다.
- 기존 `MVSDK_Net.dll`은 .NET Framework 4.0 래퍼라 .NET 9 직접 참조 전 호환성 검증이 필요합니다.
- callback에서 받은 프레임 버퍼를 UI thread나 이력 저장까지 오래 들고 있으면 안 됩니다. 필요한 데이터는 복사하거나 파일로 저장하고 즉시 `ReleaseFrame` 해야 합니다.
- WPF UI 갱신은 ViewModel을 통해 진행하고, Vision 프로젝트에서 UI control을 직접 참조하지 않습니다.
- 대용량 AI DLL과 모델 파일은 GitHub 일반 커밋 대상이 아니라 배포 산출물 또는 설치 패키지로 관리합니다.
