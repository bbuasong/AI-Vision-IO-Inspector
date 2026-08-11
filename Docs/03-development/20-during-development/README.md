# 20. 개발 시 준수 사항

기준일: 2026-08-11

구현하는 동안 지켜야 할 규칙과, 이미 확정돼서 임의로 뒤집으면 안 되는 결정을 모았습니다.

## 코드를 쓰기 전에

| 파일 | 내용 |
| --- | --- |
| `coding-rules.md` | C# 코딩 규칙과 주석 원칙. 람다/delegate 최소화, .NET 4.5에서 익숙한 명시적 코드 선호, 주석은 why 중심 |
| `decisions.md` | **확정된 의사결정.** 여기 있는 항목은 임의로 바꾸지 않습니다. 변경이 필요하면 사유와 함께 이 문서를 먼저 갱신합니다 |
| `checklist.md` | 투입 초기, 구현 전, 구현 후 체크리스트 |

## 코드를 쓴 다음에

| 파일 | 내용 |
| --- | --- |
| `review-checklist.md` | 코드 리뷰 및 자체 점검 항목 |
| `work-log-automation.md` | `scripts\append_work_log.ps1`로 작업 로그를 날짜 섹션에 추가하는 방법 |

작업 기록은 `../10-before-development/work-log.md`와 `changelog.md`에 남깁니다.

## 작업 요청 형식

| 파일 | 내용 |
| --- | --- |
| `codex-task-template.md` | AI 어시스턴트 작업 요청 템플릿 |

## VLAD SDK를 건드릴 때

`vision/` 폴더에 있습니다. **날짜가 최신인 문서가 현행 계약입니다.**

### 현행 계약 (2026-08 기준)

| 파일 | 내용 |
| --- | --- |
| `vision/VLAD_HD_Inference_Mat수정-2026-08-07.md` | **최신 MAT 추론 API 계약** |
| `vision/vlad-hd-api-v1.3-correction-2026-08-07.md` | v1.3 계약 정정 사항 |
| `vision/VLAD_HD_Inference_Mat수정-2026-08-05.md` | 고정 버퍼 계약 적용 시점 기록 |
| `vision/rtsp-latest-frame-capture-2026-08-05.md` | RTSP callback 최신 프레임 캡처 방식 |
| `vision/sdk-runtime-boundary-2026-08-05.md` | 관리 코드와 SDK 런타임의 책임 경계 |
| `vision/native-deployment.md` | Native 의존성 배포 구성, CUDA/cuDNN 요구사항 |
| `vision/vision-implementation-checklist.md` | Vision 구현 점검 항목 |
| `vision/README.md` | Vision 담당자용 안내 |

### 이전 계약과 분석 자료

아래는 경위 파악용입니다. **현재 구현과 다를 수 있으므로 그대로 적용하지 않습니다.**

| 시기 | 파일 |
| --- | --- |
| 2026-08-03/04 | `vlad-hd-api-parameter-revision-proposal-2026-08-03.md`, `vlad-hd-api-parameter-revision-mail-draft-2026-08-03.md`, `vlad-hd-api-1.1-implementation-preparation-2026-08-04.md` |
| 2026-07 | `vlad-hd-json-interface-contract-2026-07-20.md`, `vlad-test-result-json-mode-2026-07-20.md`, `cuda-ptxas-runtime-2026-07-20.md`, `dual-vlad-id-runtime-2026-07-20.md`, `training-process-integration.md`, `ai-result-contract.md`, `vlad-detect-str-contract-2026-07-16.md`, `vlad-similarity-search-dll-contract-2026-07-16.md`, `vlad-result-and-similarity-contract-2026-07-15.md`, `vision-training-guide-2026-07-13.md`, `inspectmat-context-json.md`, `camera-ai-integration.md` |
| 2026-06 | `vlad-imv-conversion-guide.md`, `vlad-ops-env-start-map-2026-06-15.md`, `vlad-ops-gap-analysis-2026-06-11.md`, `vlad-image-flow-decision-2026-06-11.md`, `vision-project-boundary.md`, `legacy-traceability-1to7.md` |

## 현재 코드에서 주의할 지점

`../10-before-development/current-program-status-2026-08-10.md`에서 확인된 내용 중 구현 시 영향이 큰 항목입니다.

- 검사 요청/결과는 C#이 할당하고 0으로 초기화한 **8192-byte UTF-8 고정 버퍼**를 사용합니다. 호출 직후 관리 문자열로 복사한 뒤 `finally`에서 해제하며, `detectData`/`searchData`는 SDK 소유 포인터로 둡니다.
- 판정 기준은 JSON의 `scoreThreshold` 하나입니다. 네이티브 `threshold` 인자를 되살리지 않습니다.
- 신규 결과에서는 **C#이 Score나 허용오차를 다시 판정하지 않습니다.** AI가 반환한 `viewJudge`와 측정부 `judge`를 그대로 사용합니다. 로컬 비교는 구형 DLL 결과의 fallback으로만 남아 있습니다.
- 결과 JSON 파싱 실패는 빈 검출이 아니라 **즉시 검사 실패**로 처리합니다. 무음 PASS가 생기지 않도록 이 동작을 유지합니다.
- `Native\VLAD` 하위 관리 DLL은 .NET Framework 기본 탐색 경로에 없습니다. `NativeDependencyLoader`가 등록하는 구조를 우회하지 않습니다.
