# 미구현/미비 항목 추적

## 운영 원칙

- 날짜별로 남은 미구현/미비 항목을 확인하고, 완료되면 완료일과 근거 파일을 기록합니다.
- 상태는 `완료`, `진행중`, `미구현-내부작업`, `미구현-외부정보필요`, `부분완료-검증필요`, `보류-범위조정` 중 하나로 기록합니다.
- `대기`, `보류`, `확인필요`처럼 원인이 드러나지 않는 표현은 사용하지 않습니다.

## 2026-06-15 현재 잔여 항목

| ID | 항목 | 상태 | 현재 판단 | 다음 작업 | 완료 기준 |
| --- | --- | --- | --- | --- | --- |
| O-001 | 실제 카메라/NVR RTSP 연결 | 부분완료-검증필요 | Top/Front 프레임 수신은 확인했지만 6채널 장시간 스트리밍 검증은 남았습니다. | 실제 6대 연결 후 검사 UI 연속 표시와 상태 갱신 확인 | 6채널 모두 `연결됨`, 프레임 갱신, 오류 로그 없음 |
| O-002 | 연속 영상 미리보기 | 진행중 | 검사 UI에 스트리밍을 표시하는 방향입니다. | 최신 프레임 캐시와 UI 표시 주기를 안정화 | UI가 수동 스냅샷 없이 계속 갱신 |
| O-003 | 트리거 방식 | 미구현-외부정보필요 | Continuous/Software/Line1 구조는 있으나 현장 트리거 정책 미확정입니다. | 장비 배선과 검사 시작 신호 방식 확인 | 옵션 값에 따라 SDK 트리거 호출 검증 |
| O-004 | pixel-mm 보정 | 미구현-내부작업 | `Calibration.json` 구조는 있으나 실제 보정값은 없습니다. | 카메라별 mm/pixel 또는 보정판 절차 확정 | pixel 기반 측정값이 mm 기준값과 비교 가능 |
| O-005 | 렌즈 왜곡 보정 | 미구현-외부정보필요 | 카메라/렌즈/설치 거리 기준이 필요합니다. | 보정 필요 여부와 파라미터 취득 방식 결정 | 보정 파라미터 저장/적용 경로 구현 |
| O-006 | VLAD 최종 모델 등록/추론 | 미구현-외부정보필요 | 받은 checkpoint-only 폴더는 VLAD_SDK 추론 모델 구조가 아닙니다. 현재 앱은 원본 VLAD_Ops처럼 C#에서 모델 구조를 선차단하지 않고, 문제 원인은 Debug 진단으로만 남긴 뒤 SDK 등록 결과를 따릅니다. | AI 담당자가 SavedModel/ONNX/PT/T7 구조로 export | `VLAD_Custom_Registration` 성공 및 샘플 이미지 추론 성공 |
| O-007 | VLAD/AI 결과 파싱 | 진행중 | 원본 VLAD_Ops처럼 SDK Draw 결과를 기본 사용합니다. raw detectData 직접 파싱은 crash 위험 때문에 환경변수 opt-in으로 제한했습니다. | 길이/너비/높이/두께 반환 규격 확정 | 측정부별 측정값/NG 사유가 이력에 저장 |
| O-008 | 기준 이미지 비교 정책 | 미구현-외부정보필요 | 기준 이미지를 AI가 직접 비교하는지, 참고 이미지로만 쓰는지 확정 필요합니다. | AI 담당자와 비교/판정 책임 범위 결정 | 검사 시작 시 기준 이미지와 현재 이미지 사용 방식 확정 |
| O-009 | RTSP Thread 종료 | 미구현-외부정보필요 | 원본 VLAD_Ops에도 명확한 client stop API가 보이지 않습니다. | VLAD SDK 담당자에게 unregister/stop API 확인 | 앱 종료/재설정 시 RTSP thread 누수 없음 |
| O-010 | 장비 PC 배포 | 부분완료-검증필요 | Native/VLAD, plugins, Config 복사 구조는 있습니다. | 개발툴 없는 PC에서 publish 산출물 실행 | EXE 단독 실행, DLL 로드, DB/History 경로 정상 |
| O-011 | 통계 화면 검증 | 부분완료-검증필요 | 기본 통계 화면은 있으나 고객 기준 필터 확정이 필요합니다. | 기간/품번/NG 유형 기준 확인 | 고객 기준 통계가 실제 이력과 일치 |
| O-012 | Excel 직접 업로드 | 보류-범위조정 | 현재는 CSV 다중등록 중심입니다. | xlsx 직접 지원 필요 여부 확인 | 범위 확정 후 구현 또는 제외 기록 |
| O-013 | 두께 복수 측정 | 보류-범위조정 | 현재는 길이/너비/높이/두께 1세트 기본입니다. | 두께가 2개 이상 필요한 품목 사례 확인 | DB/UI/CSV에 복수 두께 구조 반영 |

## 2026-06-12 정리된 항목

| ID | 정리 내용 | 근거 |
| --- | --- | --- |
| C-001 | 모델 경로 구조를 C#에서 선차단하지 않도록 변경 | `VladModelPathInspector`, `VladVisionInferenceEngine`, `VisionCameraCoordinator` |
| C-002 | 원본 VLAD_Ops처럼 RTSP Thread가 직접 VLAD 등록을 만들지 않고 기존 VLAD_ID만 사용하도록 정리 | `VladSdkSession`, `VisionCameraCoordinator` |
| C-003 | `VLAD_Warm_Up`, `VLAD_Unregistration` 진입점 추가 | `VladNativeMethods`, `VLAD_Ops_Ai`, `VladSdkSession`, `VladFunctionAdapter` |
| C-004 | 측정값 미확정 시 기준값으로 위장하지 않도록 변경 | `VladMeasurementMapper` |
| C-005 | Vision 문서의 인코딩 깨짐과 현재 상태 불일치 정리 | `AI.Vision.IOInspector.Vision/README.md`, `vision-implementation-checklist.md`, `vlad-ops-gap-analysis-2026-06-11.md` |

## 2026-06-15 정리된 항목

| ID | 정리 내용 | 근거 |
| --- | --- | --- |
| C-006 | 원본 VLAD_Ops에 없던 checkpoint-only 모델 구조 선차단을 제거하고 Debug 진단만 남기도록 변경 | `VladVisionInferenceEngine`, `VladModelPathInspector` |
| C-007 | Vision 하위 Docs를 배포 대상 밖 상위 문서 폴더로 이동 | `Docs/03-development/vision` |
| C-008 | `VLAD_Ops_Ai_Compat` Env Start 중복 구현을 공식 `VLAD_Ops_Ai` 구현으로 위임 | `VLAD_Ops_Ai_Compat.cs` |
