# Vision 구현 체크리스트

## 2026-06-09 현재 판단

VLAD_Ops/VLAD_SDK 코드를 현재 `AI-Vision IO Inspector` 구조에 맞춰 옮기는 작업은 1차 구현이 완료되었습니다.
이제 프로그램은 다음 경로를 기준으로 동작합니다.

```text
검사 시작
  -> 카메라 캡처
  -> 촬영 이미지 파일 생성
  -> OpenCV Mat 변환
  -> VLAD_Inference_Mat 호출
  -> VLAD detectData 해석
  -> 측정부 값 매핑
  -> 기존 판정/이력 저장 흐름
```

다만 실제 현장 모델, 카메라 보정값, AI 담당자의 최종 detectData 포맷 정의가 없으면 치수값의 정확도는 확정할 수 없습니다.

## 완료 항목

| 항목 | 상태 | 위치 |
| --- | --- | --- |
| Vision 전용 프로젝트 분리 | 완료 | `AI.Vision.IOInspector.Vision` |
| Direct SDK 카메라 열기/닫기 | 완료 | `ImvCamera/ImvCameraDevice.cs` |
| Direct SDK 프레임 수신 | 완료 | `IMV_GetFrame`, `IMV_PixelConvert`, `IMV_ReleaseFrame` |
| Direct SDK 프레임 BMP 저장 | 완료 | `ImvCamera/ImvBitmapWriter.cs` |
| 실제 프레임 수신 기준 연결 상태 판정 | 완료 | `Services/VisionCameraCoordinator.cs` |
| 기존 `VLAD_Ops_imvCam_Thread` 형태 호환 | 완료 | `ImvCamera/VLAD_Ops_imvCam.cs` |
| `MVSDK_Net.dll` 참조 | 완료 | `AI.Vision.IOInspector.Vision.csproj` |
| `OpenCvSharp*.dll` 참조 | 완료 | `AI.Vision.IOInspector.Vision.csproj` |
| `VLAD_SDK.dll`, `VLAD_Ctrl.dll`, `MVSDKmd.dll` 배포 포함 | 완료 | `Native/VLAD`, App csproj Copy 설정 |
| `plugins`/VLC 관련 DLL 배포 포함 | 완료 | `Native/VLAD/plugins`, App csproj Copy 설정 |
| `CFG/Config.json` 실행 폴더 복사 | 완료 | App csproj Copy 설정 |
| `VLAD_Ops_Ai_Env_Start` 호환 함수 | 완료 | `LegacyVlad/VLAD_Ops_Ai.cs` |
| `VLAD_Ops_Ai_Cam_InferenceData` 호환 함수 | 완료 | `LegacyVlad/VLAD_Ops_Ai.cs` |
| `VLAD_Ops_RTSP_Thread` 호환 진입점 | 보강 완료 | `LegacyVlad/VLAD_Ops_RTSP.cs` |
| RTSP URL 자동 생성 | 완료 | `RtspUrlBuilder.Build`, `VisionCameraCoordinator.CaptureVladRtsp` |
| `VLAD_Inference_Mat` 호출 경로 | 완료 | `Engines/VladVisionInferenceEngine.cs` |
| 이미지 파일 -> OpenCV Mat 변환 | 완료 | `LegacyVlad/OpenCvSharpMatImage.cs` |
| VLAD detectData 파서 | 부분 완료 | `LegacyVlad/VladInferenceResultParser.cs` |
| detectText/bbox -> 측정부 값 매핑 | 부분 완료 | `Services/VladMeasurementMapper.cs` |
| 픽셀-mm 보정 파일 구조 | 부분 완료 | `CFG/Calibration.json`, `MeasurementCalibrationService.cs` |

## 외부 조건이 필요한 항목

| 항목 | 왜 필요한가 | 다음 작업 |
| --- | --- | --- |
| 실제 VLAD 모델 경로 | `Config.json`의 `MODEL` 경로가 실제 PC에 존재해야 VLAD 등록 가능 | 설치 PC에서 `E:/Tensor_Projects/...` 또는 환경변수 `AI_VISION_VLAD_MODEL_PATH` 확인 |
| 실제 detectData 포맷 | VLAD 기본 메시지는 class/score/bbox 중심이며, 치수값이 별도 포맷으로 올 수 있음 | AI 담당자에게 길이/너비/높이/두께 반환 포맷 확정 요청 |
| 카메라별 캘리브레이션 | 픽셀 bbox를 mm로 바꾸려면 시점별 mm/pixel 값 필요 | `CFG/Calibration.json`에 Top/Front/Back/Left/Right/Thickness 보정값 입력 |
| VLAD RTSP 콜백 프레임 규격 | callback의 display 포인터가 실제로 BGR 8UC3인지, 해상도가 채널 설정과 일치하는지 확인 필요 | 실제 NVR RTSP 스트림으로 Top/Front부터 검증 |
| VLAD RTSP Thread 종료 API | 기존 코드에 명확한 해제 흐름이 없음 | VLAD SDK 담당자에게 RTSP client unregister/stop API 확인 |
| 실장비 연속 스트리밍 장시간 테스트 | 6대 카메라 동시 수신에서 CPU/메모리/디스크 부하 확인 필요 | 실제 카메라 6대 연결 후 30분 이상 테스트 |
| 모델 기준 Pass/Fail 정의 | 검출이 있으면 NG인지, 특정 class만 NG인지 정책 필요 | AI 담당자와 class별 판정 정책 확정 |

## 현재 판정 방식

- VLAD 추론이 실패하면 `IsSuccess=false`로 반환하고 검사 결과는 Error 처리됩니다.
- VLAD 검출 결과가 0건이면 부품 정합성은 OK로 봅니다.
- VLAD 검출 결과가 1건 이상이면 기본적으로 NG 후보로 봅니다.
- 측정값은 다음 순서로 채웁니다.
  1. `detectText`에서 `길이`, `너비`, `높이`, `두께` 숫자를 찾음
  2. bbox 픽셀값을 찾고 `Calibration.json` 보정값이 있으면 mm로 변환
  3. 치수값을 확정할 수 없으면 기준값으로 채우고 `CalibrationMissing` 또는 `ReferenceValueFallback`을 기록

## 담당자가 알아야 할 함수 대응

| 기존 VLAD_Ops 함수 | 현재 프로젝트 위치 | 상태 |
| --- | --- | --- |
| `VLAD_Ops_Ai_Env_Start` | `LegacyVlad/VLAD_Ops_Ai.cs` | 구현 |
| `VLAD_Custom_Registration` | `LegacyVlad/VladNativeMethods.cs` | 구현 |
| `VLAD_Inference_Mat` | `LegacyVlad/VLAD_Ops_Ai.cs`, `VladVisionInferenceEngine.InspectMat` | 구현 |
| `VLAD_InferenceData_V1_Draw` | `LegacyVlad/VLAD_Ops_Ai.cs`, `VladInferenceResultParser` | 구현 |
| `VLAD_InferenceData_V2_Draw` | `LegacyVlad/VLAD_Ops_Ai.cs`, `VladInferenceResultParser` | 구현 |
| `VLAD_InferenceData_Get_Valid_Count` | `LegacyVlad/VLAD_Ops_Ai.cs`, `VladInferenceResultParser` | 구현 |
| `VLAD_Get_Class_Str` | `LegacyVlad/VLAD_Ops_Ai.cs`, `VladInferenceResultParser` | 구현 |
| `VLAD_Rtsp_Info_Client_Registration` | `LegacyVlad/VLAD_Ops_Ai.cs`, `LegacyVlad/VLAD_Ops_RTSP.cs` | 래퍼 구현 |
| `VLAD_Ops_imvCam_Thread` | `ImvCamera/VLAD_Ops_imvCam.cs` | 구현 |
| `IMV_EnumDevices` | `ImvCamera/ImvCameraManager.cs` | 구현 |
| `IMV_GetFrame` | `ImvCamera/ImvCameraDevice.cs` | 구현 |
| `IMV_ReleaseFrame` | `ImvCamera/ImvCameraDevice.cs` | 구현 |

## 계속 주의할 점

- SDK 프레임 버퍼를 오래 잡고 있으면 카메라 수신이 밀릴 수 있으므로 `IMV_ReleaseFrame`은 반드시 즉시 호출합니다.
- `OpenCvSharp` 네이티브 DLL이 실행 폴더에 없으면 `OpenCvSharp.NativeMethods` 초기화 예외가 납니다. 현재 App 빌드 출력에는 복사되도록 설정되어 있습니다.
- `camera-config.json`에서 `RtspUrl`을 비워두는 경우 `IpAddress`, `Port`, `StreamPath`로 URL을 생성합니다. `StreamPath`가 NVR 채널과 다르면 영상 수신은 실패합니다.
- `VLAD RTSP Thread`는 현재 호환 보조 경로입니다. 실제 검사 결과는 `VladVisionInferenceEngine`이 캡처 이미지 파일을 다시 읽어 추론하는 경로를 사용합니다.
- `VLAD Source` 원본 폴더는 GitHub에 올리지 않습니다.
- 이미지는 DB 일괄 삭제나 CSV 재등록 과정에서 삭제하지 않습니다. 삭제는 사용자가 지정한 삭제 동작, 현재 6개 저장, 미등록 제품 기준이미지 저장, 수동 폴더 관리 케이스에만 허용합니다.
