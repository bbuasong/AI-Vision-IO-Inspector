# VLAD/IMV 변환 가이드

## 2026-06-09 목적

기존 `VLAD_Ops` 코드를 알고 있는 AI/Vision 담당자가 현재 `AI.Vision.IOInspector.Vision` 프로젝트에서 같은 역할의 코드를 빠르게 찾을 수 있도록 함수명 대응 관계를 정리합니다.

## 기존 함수명 유지 방침

- 기존 원본 소스 자체는 GitHub에 올리지 않습니다.
- 대신 현재 프로젝트 안에 `VLAD_Ops_Ai.cs`, `VLAD_Ops_RTSP.cs`, `VLAD_Ops_imvCam.cs` 이름의 호환 진입점을 둡니다.
- WPF UI는 직접 제어하지 않고, Vision 프로젝트는 카메라 프레임/AI 추론/결과 변환까지만 담당합니다.
- 기존 코드의 delegate callback은 SDK가 요구하는 부분만 유지하고, 일반 로직에는 무분별한 delegate/lambda 사용을 피합니다.

## 함수 대응표

| 기존 VLAD/IMV 함수 | 현재 위치 | 현재 역할 |
| --- | --- | --- |
| `VLAD_Ops_Ai_Env_Start` | `LegacyVlad\VLAD_Ops_Ai.cs`, `VladSdkSession.cs` | `VLAD_Registration` 또는 `VLAD_Custom_Registration` 후 모델 등록 |
| `VLAD_Registration` | `LegacyVlad\VLAD_Ops_Ai.cs`, `VladNativeMethods.cs` | VLAD SDK 기본 핸들 등록 |
| `VLAD_Custom_ID_Generate` | `LegacyVlad\VladNativeMethods.cs` | Custom 등록용 ID 생성 |
| `VLAD_Custom_Registration` | `LegacyVlad\VLAD_Ops_Ai.cs`, `VladNativeMethods.cs` | HD처럼 별도 site_name/custom_info가 필요한 등록 경로 |
| `VLAD_Ops_Inference_Registration` | `LegacyVlad\VLAD_Ops_Ai.cs`, `VladNativeMethods.cs` | 모델 경로, site, gpu id 등록 |
| `VLAD_Inference_Mat` | `LegacyVlad\VLAD_Ops_Ai.cs`, `Engines\VladVisionInferenceEngine.cs` | OpenCV Mat/rawData 포인터 기반 추론 호출 |
| `VLAD_InferenceData_Get_Valid_Count` | `LegacyVlad\VLAD_Ops_Ai.cs` | detect data 유효 개수 확인 |
| `VLAD_InferenceData_V1_Draw` | `LegacyVlad\VLAD_Ops_Ai.cs` | V1 결과 draw/파싱 연결 지점 |
| `VLAD_InferenceData_V2_Draw` | `LegacyVlad\VLAD_Ops_Ai.cs` | V2 결과 draw/파싱 연결 지점 |
| `VLAD_Rtsp_Info_Client_Registration` | `LegacyVlad\VLAD_Ops_Ai.cs`, `VLAD_Ops_RTSP.cs` | VLAD SDK 방식 RTSP callback 등록 |
| `VLAD_Ops_RTSP_Thread` | `LegacyVlad\VLAD_Ops_RTSP.cs` | 기존 RTSP 스레드 진입점 호환 |
| `VLAD_Ops_RTSP_Frame_Proc` | `LegacyVlad\VLAD_Ops_RTSP.cs` | 기존 RTSP frame callback 호환 |
| `VLAD_Ops_imvCam_Thread` | `ImvCamera\VLAD_Ops_imvCam.cs` | 기존 IMV 수신 스레드 이름 보존 |
| `VLAD_Ops_imvCam_IMV_Open` | `ImvCamera\VLAD_Ops_imvCam.cs` | 현재 `ImvCameraDevice.OpenDevice`, `StartGrabbing` 연결 |
| `Camera_Control.Open_Cam` | `ImvCamera\Camera_Control.cs` | 기존 카메라 open/start/callback 흐름 대응 |
| `IMV_EnumDevices` | `ImvCamera\MVSDK_Net_Compat.cs` | `MyCamera.IMV_EnumDevices` 사용 |
| `IMV_GetFrame` / `IMV_ReleaseFrame` | `ImvCamera\MVSDK_Net_Compat.cs` | `MyCamera` 인스턴스 기반 frame 획득/반환 |

## 현재 구현 상태

- `MVSDK_Net.dll` 직접 참조는 `AI.Vision.IOInspector.Vision.csproj`에 표시됩니다.
- `OpenCvSharp*` 직접 참조도 `AI.Vision.IOInspector.Vision.csproj`에 표시됩니다.
- `VLAD_SDK.dll`, `VLAD_Ctrl.dll`, `libvlc.dll`, `libvlccore.dll`, `opencv_world453.dll`, `jsoncpp.dll`은 `NativeReferences\VLAD` 링크로 Visual Studio에서 보입니다.
- 현재 메인 솔루션 빌드는 .NET Framework 4.7.2, PlatformTarget=x64 기준입니다. Debug/Release x64 출력 폴더에 CFG, DB, Native\VLAD, RuntimeData\Models를 복사하는 구조로 정리했습니다.

## 남은 실제 구현 지점

1. `MVSDKmd.dll`과 제조사 종속 DLL을 확보한 뒤 `MVSDK_Net_Compat.IMV_GetVersion()` 및 `IMV_EnumDevices()` 런타임 호출을 검증합니다.
2. `VLAD_Ops_RTSP_Frame_Proc`에서 받은 `display` 포인터의 실제 이미지 포맷, width, height, stride를 담당자와 확정합니다.
3. `VLAD_Inference_Mat`의 detect data를 `VisionInspectionOutput.Measurements`로 변환하는 파서를 구현합니다.
4. 측정값은 기본 `mm`로 고정하되, AI가 pixel 값을 줄 경우 calibration/homography 변환 계층을 추가합니다.
