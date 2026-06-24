# Vision 문서 안내

기준일: 2026-06-22

AI/카메라 담당자가 우선 읽어야 하는 문서만 이 폴더에 모았습니다. 전체 프로젝트 문서를 모두 읽기 전에 아래 순서로 확인하면 됩니다.

## 먼저 읽을 문서

| 순서 | 파일 | 목적 |
| --- | --- | --- |
| 1 | `vision-project-boundary.md` | Vision 프로젝트가 담당하는 범위와 App/DB/UI와의 경계 확인 |
| 2 | `native-deployment.md` | VLAD DLL, CUDA/cuDNN, 배포 폴더 구조 확인 |
| 3 | `vision-implementation-checklist.md` | 현재 구현 상태와 AI 담당자 확인 필요 항목 확인 |
| 4 | `vlad-imv-conversion-guide.md` | 기존 VLAD/IMV 이름과 현재 프로젝트 이름 대응 확인 |
| 5 | `camera-ai-integration.md` | 카메라/NVR/AI 측정값 연동 방향 확인 |
| 6 | `vlad-image-flow-decision-2026-06-11.md` | 기준 이미지와 검사 이미지 저장 정책 확인 |

## 현재 코드 기준 요약

- 메인 앱은 `.NET Framework 4.7.2`, x64 전용 WPF MVVM입니다.
- 카메라/모델 설정 기준은 `CFG\Config.json`입니다.
- `RuntimeData\Camera\camera-config.json`은 더 이상 사용하지 않습니다.
- WPF 기본 실행 흐름은 별도 `VisionWorker.exe`가 아니라 in-process `VisionInferenceWorker` 스레드입니다.
- `AI.Vision.IOInspector.VisionWorker` 프로젝트는 남아 있지만 현재는 진단/레거시 용도입니다.
- `Native\VLAD` 하위 관리 DLL은 `RuntimeAssemblyResolver`가 앱 시작 시 탐색 경로에 등록합니다.

## Vision 코드 주요 진입점

| 목적 | 파일/클래스 |
| --- | --- |
| Vision 서비스 생성 | `VisionRuntimeFactory` |
| CAM 모드 초기화 | `VladCamModeRuntime` |
| `VladId` 공유/재사용 | `VladSdkSession` |
| VLAD_Ops 함수명 호환 | `LegacyVlad\VLAD_Ops_Ai.cs` |
| VLAD_SDK P/Invoke | `LegacyVlad\VladNativeMethods.cs` |
| RTSP Thread 호환 | `LegacyVlad\VLAD_Ops_RTSP.cs` |
| 검사 추론 | `VladVisionInferenceEngine` |
| 추론 Worker 스레드 | `Threading\VisionInferenceWorker.cs` |
| 결과 파싱 | `VladInferenceResultParser` |
| 측정값 매핑 | `VladMeasurementMapper` |
| 카메라 서비스 | `VisionCameraService`, `VisionCameraCoordinator` |

## 남은 핵심 리스크

- 최종 VLAD 모델 export 구조 미확정.
- CUDA/cuDNN/VC++ Runtime 배포 방식 미확정.
- 6채널 장시간 RTSP/NVR 검증 미완료.
- VLAD 결과 구조체와 측정값/판정 매핑 최종 스키마 미확정.
- in-process VLAD 초기화가 네이티브 fail-fast에 취약할 수 있으므로 최종 환경에서 안정성 검증 필요.
