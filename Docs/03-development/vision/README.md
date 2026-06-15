# Vision 담당자 문서

이 폴더는 카메라 영상 처리, AI 추론, VLAD/IMV 연동 담당자가 우선 읽어야 하는 문서만 모아둔 위치입니다.

## 읽는 순서

| 순서 | 파일 | 목적 |
| ---: | --- | --- |
| 1 | `vision-project-boundary.md` | 현재 Vision 프로젝트의 책임 범위와 App/DB/History와 연결되는 지점 확인 |
| 2 | `vlad-imv-conversion-guide.md` | 기존 VLAD_Ops/IMV 함수명과 현재 프로젝트 함수명의 대응 관계 확인 |
| 3 | `native-deployment.md` | VLAD, VLC, OpenCV, IMV DLL 배치와 x64 배포 기준 확인 |
| 4 | `camera-ai-integration.md` | 확정 카메라 사양, NVR/RTSP/Direct SDK 방향, 측정값 단위 처리 확인 |
| 5 | `legacy-traceability-1to7.md` | 기존 VLAD 코드에서 얻을 수 있는 근거와 신규 설계가 필요한 부분 확인 |
| 6 | `vlad-ops-gap-analysis-2026-06-11.md` | 기존 VLAD_Ops 대비 현재 구현 상태, 검사 시작 오류 원인, 남은 차이 확인 |
| 7 | `vlad-image-flow-decision-2026-06-11.md` | 기준 이미지/검사 캡처/AI 판정/이력 이미지 저장의 판단 기준 확인 |
| 8 | `vision-implementation-checklist.md` | 남은 구현/검증 항목 확인 |
| 9 | `vlad-ops-env-start-map-2026-06-15.md` | 원본 VLAD_Ops_Ai_Env_Start 호출 패턴과 현재 코드 대응 확인 |

## 2026-06-11 기준 요약

- 현재 WPF 앱의 실시간 미리보기는 `RtspVideoHost`와 LibVLC 경로를 사용합니다.
- 기존 VLAD 담당자가 찾기 쉽도록 `LegacyVlad\VLAD_Ops_Ai.cs`, `LegacyVlad\VLAD_Ops_RTSP.cs`, `ImvCamera\VLAD_Ops_imvCam.cs` 이름의 호환 진입점을 유지합니다.
- `AI.Vision.IOInspector.Vision.csproj`에서 `MVSDK_Net`, `OpenCvSharp`, `OpenCvSharp.Blob`, `OpenCvSharp.Extensions`, `OpenCvSharp.UserInterface` 참조가 명시적으로 보입니다.
- `MVSDK_Net.dll`은 x64 관리 DLL이므로 App/Vision 프로젝트는 `PlatformTarget=x64`로 고정했습니다.
- 현재 `Native\VLAD`에는 `MVSDKmd.dll`과 VLAD/VLC/OpenCV 관련 DLL이 배치되어 있습니다.
- `CFG\Config.json`의 `MODEL` 경로가 실제 PC에 없으면 검사 시작 후 AI 추론 단계에서 Error가 납니다.
