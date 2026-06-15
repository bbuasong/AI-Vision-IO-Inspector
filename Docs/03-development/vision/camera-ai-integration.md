# 카메라/AI 연동 설계 메모

## 2026-05-29 장비 확정

| 구분 | 모델 | 수량 | 핵심 사양 | 적용 방향 |
| --- | --- | ---: | --- | --- |
| Global Shutter Network Camera | DC-T3145G | 2 | 5M, 최대 2448x2048, 6:5/4:3/16:9 지원 | 이동/트리거 영향이 큰 촬영 위치에 우선 배치 |
| Rolling Shutter Camera | DC-T3145R | 4 | 5M, 최대 2592x1944, Max 30fps, 4:3 중심 | 정지 상태 또는 움직임 영향이 낮은 촬영 위치에 배치 |
| NVR | DR-2508P-A | 1 | 8CH Direct IP NVR, 2TB 내장 | 녹화/모니터링/백업용. 측정용 원본 취득은 직접 카메라 SDK 우선 |

두 카메라 모두 4:3 또는 6:5 계열 해상도가 중요하므로, 프로그램의 6개 화면은 16:9 고정으로 늘려 그리지 않고 원본 비율을 유지해야 한다.

## VLAD Source 분석 결론

`Docs/00-inbox/documents/VLAD Source`는 세 부분으로 나뉜다.

| 폴더 | 역할 | 재사용 판단 |
| --- | --- | --- |
| `VLAD_Ops - Rev2` | .NET Framework 4.7.2 WinForms 운영 UI, 카메라/RTSP/AI 호출 샘플 | 직접 이식보다 흐름 참고 |
| `VLAD_SDK - Rev3` | C++ 네이티브 SDK, AI 등록/추론, RTSP, OpenCV 유틸 | DLL 연동 방식과 결과 포맷 참고 |
| `HD_Dll` | VLAD 확장 DLL 예시, AI 검출 후 OpenCV 후처리 | 측정 후처리 예시로만 참고 |

기존 코드는 현재 WPF MVVM .NET 9 구조와 맞지 않는다. 따라서 WinForms 화면, static 전역 상태, 하드코딩 경로를 가져오지 말고 Application/Infrastructure 계층의 어댑터로 다시 감싸야 한다.

## 참고할 코드

| 목적 | 참고 위치 | 확인 내용 |
| --- | --- | --- |
| RTSP 수신 | `VLAD_Ops_RTSP.cs`, `VLAD_SDK_Rtsp.cpp` | LibVLC 콜백 기반 프레임 수신 구조 |
| 직접 카메라 SDK | `VLAD_Ops_imvCam.cs`, `Camera/C#/IMV/*` | `MVSDK_Net.MyCamera` 기반 Enum/Open/Start/GetFrame/PixelConvert 흐름 |
| 지정 카메라 연결 | `Camera/C#/IMV/ConnectSpecCamera` | index, DeviceUserID, CameraKey, IP 기반 핸들 생성 |
| 소프트웨어 트리거 | `Camera/C#/IMV/SoftwareTrigger` | `TriggerSource`, `TriggerSelector`, `TriggerMode`, `TriggerSoftware` 설정 흐름 |
| 다중 카메라/프레임 큐 | `Camera/C#/IMVFG/MultipleCamera` | 프레임 콜백, CloneFrame, ReleaseFrame, 표시 스레드 분리 |
| AI P/Invoke | `VLAD_Ops_Ai.cs`, `VLAD_SDK.h` | `VLAD_Registration`, `VLAD_Inference_Mat`, 결과 draw/info API |
| 객체검출 결과 파싱 | `Camera/Common_Lib/VLAD_SDK.cs` | class, score, bbox, mask 좌표 TLV 구조 |
| 측정 후처리 예시 | `HD_Dll/HD_Proc.cpp` | contour, minAreaRect, circle 기반 pixel 치수 계산 예시 |

## 그대로 쓰면 위험한 부분

| 문제 | 영향 | 대응 |
| --- | --- | --- |
| `D:\DevTools\...VLAD_SDK.dll` 하드코딩 | 배포 PC에서 DLL 로드 실패 | 앱 실행 폴더의 `Native/` 또는 설정 기반 DLL 로딩 |
| RTSP 프레임 1920x1080 고정 | 2448x2048, 2592x1944, 4:3/6:5 화면 왜곡 또는 메모리 오류 | 프레임 메타데이터에 width, height, stride, pixel format 포함 |
| RTSP callback이 포인터만 전달 | C#에서 실제 해상도/stride/timestamp를 알 수 없음 | callback 계약 확장 또는 별도 RTSP 수신 구현 |
| `VLAD_Ops_imvCam`의 static 단일 카메라 | 6대 동시 운용 불가 | 카메라별 인스턴스와 `CameraDeviceId` 기반 관리 |
| WinForms/static UI 결합 | WPF MVVM에서 테스트/교체 어려움 | `ICameraSource`, `IAiInferenceService` 구현체로 분리 |
| 기존 측정값이 pixel 기준 | mm/cm 단위 판정 불가 | 보정값과 기준 단위 변환 계층 추가 |

## 권장 구조

현재 `ICameraService`와 `IAiInferenceService`는 좋은 출발점이다. 다만 실제 6대 카메라와 AI 측정을 받기 위해서는 `CapturedImage`만으로 부족하므로 라이브 프레임용 모델을 추가한다.

| 구성 | 책임 |
| --- | --- |
| `CameraDeviceProfile` | 카메라 모델, IP, RTSP URL, ViewType, 셔터 타입, 해상도, 트리거 방식, NVR 채널 관리 |
| `CameraFrame` | CameraId, ViewType, Width, Height, Stride, PixelFormat, FrameId, CapturedAt, Buffer/FilePath 관리 |
| `ICameraSource` | 단일 카메라 Open/Close/Start/Stop/CaptureOne 처리 |
| `MultiCameraCoordinator` | 6대 카메라 상태, 동시/순차 촬영, 타임아웃, 실패 이벤트 통합 |
| `DirectSdkCameraSource` | DC-T3145G/R 직접 SDK 수신 구현. 측정용 원본 취득의 1순위 |
| `RtspCameraSource` | NVR/RTSP 미리보기 또는 보조 수신 구현 |
| `VladAiInferenceService` | VLAD DLL 등록/해제/추론/결과 파싱을 담당 |
| `MeasurementCalibrationService` | pixel 측정값을 mm 기준값으로 변환하고 UI 단위로 환산 |

화면의 6개 View는 기존 기준 이미지 순서와 맞춰 `Top`, `Front`, `Back`, `Left`, `Right`, `Thickness`로 관리한다. Global Shutter 2대는 실제 설비에서 움직임이 큰 위치가 확인된 뒤 ViewType에 배정한다.

## AI 결과 계약

VLAD Source의 기본 결과는 객체 검출 중심이다.

- class id/name
- score
- bbox x/y/w/h
- mask 좌표

현재 요구사항의 최종 판정에는 길이/너비/높이/두께의 측정값, 기준값, 허용값, 단위, OK/NG가 필요하다. 따라서 AI 연동 계약은 다음 둘 중 하나로 확정해야 한다.

| 방식 | 설명 | 권장 |
| --- | --- | --- |
| AI가 측정값까지 반환 | AI/Vision 모듈이 mm 기준 측정값과 판정을 반환 | AI 팀이 치수 알고리즘까지 담당할 때 |
| AI는 위치만 반환, Application이 측정 | AI는 bbox/mask/keypoint를 반환하고 Application이 OpenCV/보정으로 치수 계산 | Application에서 측정 로직을 통제해야 할 때 |

어느 방식이든 내부 판정 기준 단위는 `mm`로 통일한다. UI에서 `cm`로 보이거나 CSV에 `cm`로 입력되더라도 DB 또는 계산 계층에서 `mm`로 환산한 뒤 비교해야 한다.

## 단위/보정 원칙

기존 VLAD/HD 예제는 pixel 길이를 계산할 뿐 물리 단위 보정이 없다. 따라서 카메라에서 받은 측정값이 cm/mm로 자동 보정된다고 보면 안 된다.

| 항목 | 원칙 |
| --- | --- |
| 기준 단위 | 내부 계산은 mm 기준 |
| UI 단위 | 사용자가 선택한 단위로 표시하되 저장 시 기준 단위 값을 함께 보관 |
| pixel 변환 | 카메라/ViewType/해상도별 `mmPerPixelX`, `mmPerPixelY` 또는 homography 필요 |
| 보정 범위 | 렌즈 왜곡, 카메라 각도, ROI 위치에 따라 단순 스케일이 부족할 수 있음 |
| 이력 저장 | 측정값, 기준값, 단위, raw pixel 값, calibration id를 함께 저장하는 구조가 바람직함 |

## SQLite 확장 권장

현재 `DB/DataBase.db`에 기준정보와 이력을 나눈 방향은 유지한다. 카메라/AI 연동을 위해 다음 테이블을 추가하는 것이 좋다.

| 테이블 | 주요 필드 |
| --- | --- |
| `Camera_Devices` | id, view_type, model, shutter_type, serial_no, ip_address, rtsp_url, nvr_channel, width, height, connection_type, is_enabled |
| `Camera_Calibrations` | id, camera_device_id, view_type, part_no, width, height, mm_per_pixel_x, mm_per_pixel_y, homography_json, valid_from, is_active |
| `Ai_Models` | id, model_name, model_path, label_path, runtime_type, gpu_id, version, is_active |
| `History_Frames` | inspection_id, camera_device_id, view_type, frame_id, image_path, width, height, captured_at |
| `History_MeasurementDetails` | inspection_id, measurement_region_id, measured_value, nominal_value, tolerance, unit, raw_pixel_value, calibration_id, result |

## 적용 순서

1. DC-T3145G/R이 `MVSDK_Net` 또는 동일 계열 SDK로 직접 연결되는지 확인한다.
2. 카메라를 index가 아니라 IP/Serial/CameraKey로 식별하는 Proof 코드를 만든다.
3. 6대 카메라 프레임을 원본 비율로 표시하는 `ICameraSource`/`MultiCameraCoordinator`를 구현한다.
4. NVR/RTSP는 녹화와 보조 미리보기 경로로 붙이고, 측정용 원본은 직접 SDK 경로를 우선 사용한다.
5. 기준 이미지와 실시간 프레임을 ViewType 기준으로 매칭한다.
6. 보정판 또는 기준 치수로 ViewType별 `mmPerPixel`/homography 보정값을 만든다.
7. VLAD DLL 또는 신규 AI 모듈을 `VladAiInferenceService`로 감싸고, 결과를 Domain의 `MeasurementResult`로 변환한다.
8. 이력에는 측정 결과뿐 아니라 사용한 카메라, 해상도, 프레임 경로, 보정 id를 함께 저장한다.

## 카메라 연결/설정 관리 방향

현재 권장 구조는 `카메라 6대 -> NVR -> PC`만으로 제한하지 않는다. NVR은 녹화와 운영 확인에 유용하지만, 검사 판정에 쓰는 프레임은 지연, 재인코딩, 해상도 고정 문제를 피하기 위해 가능하면 PC가 카메라에 직접 접속해 취득한다.

| 경로 | 목적 | 판정 사용 |
| --- | --- | --- |
| 카메라 -> PC Direct SDK | 트리거, 원본 해상도, 프레임 메타데이터, 보정 기반 측정 | 1순위 |
| 카메라/NVR -> PC RTSP | 운영 미리보기, 녹화 확인, 장애 시 보조 화면 | 보조 |
| 카메라 -> NVR | 장기 녹화, 사후 확인 | 이력/증빙 |

카메라와 `Top/Front/Back/Left/Right/Thickness`의 매핑은 코드 고정값이 아니라 설정 데이터로 관리한다. 초기 개발 단계에서는 SQLite 설정 테이블을 기준으로 하고, 운영자가 수정해야 하는 항목은 별도 Option UI를 제공하는 방향이 좋다.

| 설정 항목 | 설명 |
| --- | --- |
| ViewType | Top, Front, Back, Left, Right, Thickness |
| CameraRole | 측정용, 미리보기용, 녹화 확인용 |
| Model | DC-T3145G 또는 DC-T3145R |
| ShutterType | Global 또는 Rolling |
| SerialNo/IP/CameraKey | 카메라 고유 식별자. index 사용은 지양 |
| RtspUrl/NvrChannel | NVR/RTSP 보조 경로 |
| Width/Height/PixelFormat | 실제 프레임 메타데이터 |
| CalibrationId | 단위 보정값 연결 |
| IsEnabled | 일시 미사용 여부 |

Option UI는 `카메라 설정` 탭 또는 별도 설정 창으로 두고, 다음 기능을 제공한다.

- 검색된 카메라 목록 표시
- 각 카메라를 ViewType에 배정
- 연결 테스트와 현재 프레임 미리보기
- 해상도/프레임레이트/트리거 모드 확인
- 보정값 선택 또는 보정 UI 이동
- 설정 저장 후 재시작 없이 적용할 수 있는 범위 명시

## 확인 필요

| 항목 | 확인 질문 |
| --- | --- |
| 카메라 SDK | DC-T3145G/R이 현재 VLAD Source의 `MVSDK_Net`/`IMV` 계열과 호환되는가? |
| 연결 방식 | 프로그램이 카메라에 직접 접속하는가, NVR RTSP만 받는가, 두 방식을 병행하는가? |
| 트리거 | 검사 시점에 6대 동시 트리거가 필요한가, 순차 촬영이면 되는가? |
| View 배정 | Top/Front/Back/Left/Right/Thickness 중 Global Shutter가 필요한 위치는 어디인가? |
| AI 출력 | AI가 측정값/단위까지 반환하는가, 아니면 bbox/mask/keypoint만 반환하는가? |
| 보정 절차 | mm 단위 보정을 위한 기준물, 보정판, 작업자 보정 UI가 필요한가? |

## 2026-05-29 코드 반영

VLAD Source 분석 결과를 현재 .NET 9 WPF 구조에 맞춰 다음 방식으로 반영했다.

| 코드 | 역할 |
| --- | --- |
| `CameraConnectionType` | `Simulated`, `DirectSdk`, `Rtsp`, `NvrRtsp`, `File` 연결 방식 구분 |
| `CameraTriggerMode` | Continuous, Software, Line1 트리거 설정 구분 |
| `CameraChannelConfig` | ViewType, 모델명, IP, Serial, DeviceUserId, RTSP URL, NVR 채널, 해상도, FPS, 노출, Gain, SDK 경로 관리 |
| `CameraConfigurationStore` | `RuntimeData/Camera/camera-config.json`에 6개 카메라 매핑 저장 |
| `ConfiguredCameraService` | 설정 기준으로 6개 채널을 순서대로 캡처하고 `CapturedImage` 목록 생성 |
| `ICameraFrameSource` | IMV SDK, RTSP, NVR, File, Simulated 프레임 취득 구현체의 공통 경계 |
| `SimulatedCameraFrameSource` | 실제 장비 없이 BMP 파일을 생성해 UI/이력/기준이미지 저장 흐름 검증 |
| `FileCameraFrameSource` | 샘플 이미지 파일을 카메라 프레임처럼 복사해 테스트 |
| `VladRtspNativeMethods` | VLAD_SDK RTSP 콜백 API의 P/Invoke 선언 보관 |

기본 설정은 실제 카메라가 없는 개발 환경을 고려해 `Simulated`로 생성한다. 앱에서 검사 실행 시 `RuntimeData/CameraCaptures/yyyyMMdd` 아래에 6개 BMP 프레임이 생성되고, 이 경로가 검사 화면의 실시간 이미지와 검사 이력에 연결된다.

기본 View 배정은 다음과 같다.

| ViewType | 기본 모델 | 해상도 | 비고 |
| --- | --- | --- | --- |
| Top | DC-T3145G | 2448x2048 | Global Shutter 기본 배정 |
| Front | DC-T3145R | 2592x1944 | Rolling Shutter |
| Back | DC-T3145R | 2592x1944 | Rolling Shutter |
| Left | DC-T3145R | 2592x1944 | Rolling Shutter |
| Right | DC-T3145R | 2592x1944 | Rolling Shutter |
| Thickness | DC-T3145G | 2448x2048 | Global Shutter 기본 배정 |

`DirectSdk`, `Rtsp`, `NvrRtsp`는 설정값으로 선택할 수 있는 구조만 먼저 마련했다. 현재는 해당 방식으로 변경하면 명확한 NotSupported 메시지를 반환한다. 실제 장비 검증 후 다음 구현체를 추가한다.

| 구현 예정 | 필요한 확인 |
| --- | --- |
| `DirectSdkCameraFrameSource` | `MVSDK_Net.dll`, `CLIDelegate.dll`, `ThridLibray.dll`, native DLL 배포 위치와 .NET 9 호환성 |
| `RtspCameraFrameSource` | VLAD_SDK/libVLC 사용 여부 또는 별도 RTSP 캡처 라이브러리 선정 |
| `NvrRtspCameraFrameSource` | DR-2508P-A 채널별 RTSP URL 규칙, 인증, 해상도/프레임 지연 |
| Option UI | 카메라 검색, ViewType 매핑, 연결 테스트, 현재 프레임 미리보기, 설정 저장 |
