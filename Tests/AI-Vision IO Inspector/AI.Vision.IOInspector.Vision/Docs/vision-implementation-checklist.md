# Vision 구현 체크리스트

## 2026-05-31 현재 판단

카메라만 준비하면 모든 기능이 바로 완료되는 상태는 아닙니다. 현재 준비된 것은 WPF 앱, SQLite 기준정보/이력, 기준 이미지 관리, 검사 흐름, AI/카메라 어댑터 경계, 시뮬레이션 촬영/판정, 카메라별 촬영 Worker 뼈대입니다.

실제 현장에서 필요한 핵심 미완료 항목은 `실제 카메라 SDK 연결`, `연속 영상 수신/미리보기`, `pixel-to-mm 보정`, `VLAD/AI 실제 추론 결과 파싱`입니다.

상위 추적 기준은 `Docs/03-development/open-items.md`의 `O-001`~`O-012`를 따른다.

## 2026-05-31 상태표

| 항목 | 상태 | 현재 근거 | 다음 작업 |
| --- | --- | --- | --- |
| Vision 전용 프로젝트 | 완료 | `AI.Vision.IOInspector.Vision` 프로젝트 분리 | 유지 |
| AI 추론 Worker | 완료 | `VisionInferenceWorker` 구현 | 실제 엔진 연결 시 부하 검증 |
| 카메라 촬영 요청 Worker | 완료 | `VisionCameraCaptureWorker` 구현 | 실제 SDK 촬영 호출로 교체 |
| 카메라 옵션 UI | 완료 | 앱 `옵션` 탭에 6채널 상태 표시 추가 | 편집/저장 UI는 실제 장비 정보 확보 후 확장 |
| 6개 ViewType 매핑 | 완료 | Top/Front/Back/Left/Right/Thickness 설정 구조 존재 | 실제 카메라 Serial/IP 매핑 입력 |
| 기준 이미지 6방향 관리 | 완료 | 위치별 유니크 이미지 저장/교체/삭제 | 실제 캡처 이미지로 현장 검증 |
| DB 기준정보/이력 | 완료 | SQLite `DataBase.db` 기준 구조 사용 | 운영 백업/보존 정책 점검 |
| 측정값 단위 변환 | 부분완료-검증필요 | `mm`, `cm`, `m` 변환 비교 가능 | pixel 값 보정은 `O-004` |
| 실제 IMV/카메라 SDK 연결 | 미구현-외부정보필요 | Adapter 이름과 뼈대만 있음 | `O-001` |
| 연속 영상 수신/미리보기 | 진행중 | `VisionCameraReceiveWorker` 뼈대 추가 | `O-002` |
| RTSP/NVR 정책 | 완료 | NVR은 녹화/모니터링 보조로 확정 | 측정 원본은 Direct SDK 유지 |
| 트리거 방식 | 미구현-외부정보필요 | Continuous/Software/Line1 설정값 있음 | `O-003` |
| 카메라 보정 | 미구현-내부작업 | 보정 모델 없음 | `O-004` |
| 렌즈 왜곡 보정 | 미구현-외부정보필요 | 보정 모델 없음 | `O-005` |
| VLAD 모델 등록/워밍업/해제 | 미구현-외부정보필요 | `VladRuntimeContext` 뼈대만 있음 | `O-006` |
| VLAD/AI 결과 파싱 | 미구현-외부정보필요 | 시뮬레이션 엔진만 동작 | `O-007` |
| NG 원인 연결 | 부분완료-검증필요 | 측정값 기준 NG 이력 표시 가능 | `O-007` |
| 이벤트/NG 이미지 보관 | 부분완료-검증필요 | 이력/로그 저장 구조 있음 | `O-008` |
| 배포 구조 | 부분완료-검증필요 | self-contained publish와 Native 폴더 기준 정리 | `O-010` |

## VSLD/VLAD 기능 대응 현황

| 기존 기능 | 현재 위치 | 상태 |
| --- | --- | --- |
| `VLAD_Ops_Ai_Env_Start` | `VLAD_Ops_Ai_Compat`, `VladRuntimeContext.Register`, `RegisterInferenceModel` | 호환 이름 추가, 실제 DLL 호출 미구현 |
| `VLAD_Warm_Up` | `VladRuntimeContext` | 실제 호출 미구현 |
| `VLAD_Inference_Mat` | `VladRuntimeContext.Inference`, `IVisionInferenceEngine.Inspect` | 시뮬레이션만 동작 |
| `VLAD_Unregistration` | `VladRuntimeContext` 해제 단계 | 실제 호출 미구현 |
| `VLAD_InferenceData_V1_Draw` | `VladFunctionAdapter`, `VladNativeMethods` | P/Invoke 선언/파서 자리 추가, 실제 파싱 미구현 |
| `VLAD_InferenceData_V2_Draw` | `VladFunctionAdapter`, `VladNativeMethods` | P/Invoke 선언/파서 자리 추가, 실제 파싱 미구현 |
| `VLAD_Ops_imvCam_Thread` | `VLAD_Ops_imvCam`, `VisionCameraCaptureWorker` | 호환 이름 추가, 연속 수신 Worker 미구현 |
| `VLAD_Ops_imvCam_IMV_Open` | `VLAD_Ops_imvCam`, `ImvCameraDevice` | 호환 이름 추가, 실제 SDK 호출 미구현 |
| `Camera_Control.Open_Cam` | `Camera_Control`, `ImvCameraDevice` | 호환 이름 추가, 실제 SDK 호출 미구현 |
| `Camera_Control.Close_Cam` | `Camera_Control`, `ImvCameraDevice` | 호환 이름 추가, 실제 SDK 호출 미구현 |
| `IMV_EnumDevices` | `ImvCameraManager.EnumDevices` | 뼈대만 있음 |
| `IMV_OpenDevice` | `ImvCameraDevice.OpenDevice` | 뼈대만 있음 |
| `IMV_StartGrabbing` | `ImvCameraDevice.StartGrabbing` | 뼈대만 있음 |
| `IMV_SetBufferCount` | `ImvCameraDevice.SetBufferCount` | 뼈대만 있음 |
| `IMV_GetFrame` | `ImvCameraDevice.GetFrame` -> `VisionFrame` | 뼈대만 있음 |
| `IMV_ReleaseFrame` | `ImvCameraDevice.ReleaseFrame` | 뼈대만 있음 |
| `IMV_StopGrabbing` | `ImvCameraDevice.StopGrabbing` | 뼈대만 있음 |
| 카메라별 Thread | `VisionCameraCaptureWorker` | 촬영 요청 Worker 구현 완료 |
| AI 추론 Thread | `VisionInferenceWorker` | 구현 완료 |

## 계속 주의할 점

- 실제 SDK callback에서 받은 프레임 버퍼를 오래 들고 있으면 안 됩니다. 필요한 데이터만 복사하고 즉시 `ReleaseFrame` 해야 합니다.
- 30fps 6대 영상을 모두 파일로 저장하면 디스크가 빠르게 찹니다. 연속 미리보기는 메모리 최신 프레임 중심, 파일 저장은 검사/NG/이벤트 중심으로 가는 것이 맞습니다.
- AI가 치수를 직접 반환하는지, bbox/mask/keypoint만 반환하는지에 따라 측정 책임 위치가 달라집니다.
- 카메라 보정이 없으면 pixel 측정값을 mm 기준값과 신뢰성 있게 비교할 수 없습니다.
- NVR 영상은 2026-05-30 기준 녹화/모니터링 보조로 확정했습니다. 측정 원본은 Direct SDK로 가져오는 구조를 유지합니다.
