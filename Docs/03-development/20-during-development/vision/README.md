# Vision 문서 안내

기준일: 2026-07-20

AI/카메라 담당자가 우선 읽어야 하는 문서만 이 폴더에 모았습니다. 전체 프로젝트 문서를 모두 읽기 전에 아래 순서로 확인하면 됩니다.

## 먼저 읽을 문서

| 순서 | 파일 | 목적 |
| --- | --- | --- |
| 1 | `vision-project-boundary.md` | Vision 프로젝트가 담당하는 범위와 App/DB/UI와의 경계 확인 |
| 2 | `native-deployment.md` | VLAD DLL, CUDA/cuDNN, 배포 폴더 구조 확인 |
| 3 | `vision-implementation-checklist.md` | 현재 구현 상태와 AI 담당자 확인 필요 항목 확인 |
| 4 | `vlad-imv-conversion-guide.md` | 기존 VLAD/IMV 이름과 현재 프로젝트 이름 대응 확인 |
| 5 | `camera-ai-integration.md` | 카메라/NVR/AI 측정값 연동 방향 확인 |
| 6 | `vlad-hd-json-interface-contract-2026-07-20.md` | 목표 HD 검사 JSON, 결과 JSON, 유사도 JSON과 DLL ABI 전환 조건 확인 |
| 7 | `dual-vlad-id-runtime-2026-07-20.md` | 전체 이미지/Crop 이미지용 두 VLAD ID 생성, RTSP 제한, 학습 후 재초기화 확인 |
| 8 | `ai-result-contract.md` | 현재 구형 CSV 결과 문자열 수신/매핑 계약 확인 |
| 9 | `vlad-detect-str-contract-2026-07-16.md` | 검사 CSV와 유사도 후보 JSON의 결과 채널, 측정부 매핑, 다중 카메라 주의사항 확인 |
| 10 | `vlad-similarity-search-dll-contract-2026-07-16.md` | 현재 유사도 검색 호출부와 후보 순위, 품번, 품명, Score JSON 계약 확인 |
| 11 | `vlad-image-flow-decision-2026-06-11.md` | 기준 이미지와 검사 이미지 저장 정책 확인 |
| 12 | `training-process-integration.md` | 외부 학습 stdout/stderr/종료와 VLAD 재초기화 흐름 확인 |
| 13 | `vlad-test-result-json-mode-2026-07-20.md` | DLL 없이 결과 JSON 수신 이후의 파싱·측정값·이력·유사도 UI 검증 방법 확인 |
| 14 | `cuda-ptxas-runtime-2026-07-20.md` | ptxas/conhost 발생 원인, CUDA 캐시 설정, 재확인 기준 확인 |

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
| 전체/Crop `VladId` 공유/재사용 | `VladSdkSession` |
| VLAD_Ops 함수명 호환 | `LegacyVlad\VLAD_Ops_Ai.cs` |
| VLAD_SDK P/Invoke | `LegacyVlad\VladNativeMethods.cs` |
| RTSP Thread 호환 | `LegacyVlad\VLAD_Ops_RTSP.cs` |
| 검사 추론 | `VladVisionInferenceEngine` |
| 추론 Worker 스레드 | `Threading\VisionInferenceWorker.cs` |
| 결과 파싱 | `VladInferenceResultParser` |
| 측정값 매핑 | `VladMeasurementMapper` |
| 카메라 서비스 | `VisionCameraService`, `VisionCameraCoordinator` |
| 외부 학습 프로세스 | `TrainingProcessService` |
| 학습 후 VLAD 재등록 | `VladRuntimeLifecycleService` |

## 남은 핵심 리스크

- 최종 VLAD 모델 export 구조 미확정.
- CUDA/cuDNN/VC++ Runtime 배포 방식 미확정.
- 6채널 장시간 RTSP/NVR 검증 미완료.
- 현재 CSV 결과 parser와 `IndexNo -> MeasurementRegion.Id` 매핑은 구현됐지만, 실제 VLAD DLL 반환 위치와 실데이터 검증은 남아 있음.
- 목표 HD JSON ABI는 `vlad-hd-json-interface-contract-2026-07-20.md`에 확정했으며, 새 DLL export/UTF-8 buffer/6채널 실검증이 필요함.
- 전체 이미지/Crop 이미지용 두 VLAD ID의 C# 수명주기는 준비됐지만, 현재 DLL은 단일 ID export만 확인됐음. 실제 두 ID native ABI 검증은 `dual-vlad-id-runtime-2026-07-20.md`를 따른다.
- in-process VLAD 초기화가 네이티브 fail-fast에 취약할 수 있으므로 최종 환경에서 안정성 검증 필요.

## 2026-07-13 교육 자료

- `vision-training-guide-2026-07-13.md`: 프로그램 실행 Initial, Search DB 선택, 검사/이력 저장 흐름과 Vision 담당자 작업 경계를 정리한 교육용 자료입니다.
