# Vision 담당자 문서

이 폴더는 카메라, 영상 처리, AI 추론, VLAD/IMV 연동 담당자가 우선 읽어야 하는 문서만 모아둔 위치입니다.

## 읽는 순서

| 순서 | 파일 | 목적 |
| ---: | --- | --- |
| 1 | `vision-project-boundary.md` | 현재 Vision 프로젝트의 책임 범위, 앱과 연결되는 지점, 아직 구현되지 않은 항목 확인 |
| 2 | `vlad-imv-conversion-guide.md` | 기존 VLAD/IMV 함수명과 현재 프로젝트의 클래스/메소드 대응 관계 확인 |
| 3 | `legacy-traceability-1to7.md` | 1~7 항목 중 기존 VLAD 코드에서 얻을 수 있는 근거와 신규 설계가 필요한 부분 확인 |
| 4 | `camera-ai-integration.md` | 확정 카메라 사양, NVR/직접 SDK 방향, AI 측정값/단위 처리 방향 확인 |
| 5 | `native-deployment.md` | VLAD/IMV/AI 네이티브 DLL 배포 위치, x64 기준, 실행 PC 배포 주의사항 확인 |
| 6 | `vision-implementation-checklist.md` | 빠뜨리기 쉬운 Vision 구현 항목과 현재 구현/미구현 상태 확인 |

## 작업 기준

- Vision 담당자는 기본적으로 이 폴더와 `AI.Vision.IOInspector.Vision` 프로젝트 소스만 먼저 보면 됩니다.
- 전체 요구사항, 화면, DB, 이력, CSV 정책은 상위 `Docs` 폴더에 남겨둡니다.
- 날짜별 미구현/미비 항목의 최상위 추적은 상위 `Docs/03-development/open-items.md`를 기준으로 합니다.
- 기존 VLAD/IMV 함수명은 검색성과 담당자 대응을 위해 유지합니다.
- 설명 주석과 신규 문서는 한국어로 작성합니다.
