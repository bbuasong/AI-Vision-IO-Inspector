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
| 6 | `vision-implementation-checklist.md` | 남은 구현/검증 항목 확인 |

## 2026-06-09 기준 요약

- 현재 WPF 앱의 실시간 미리보기는 `RtspVideoHost`와 LibVLC 경로를 사용합니다.
- 기존 VLAD 담당자가 찾기 쉽도록 `LegacyVlad\VLAD_Ops_Ai.cs`, `LegacyVlad\VLAD_Ops_RTSP.cs`, `ImvCamera\VLAD_Ops_imvCam.cs` 이름의 호환 진입점을 유지합니다.
- `AI.Vision.IOInspector.Vision.csproj`에서 `MVSDK_Net`, `OpenCvSharp`, `OpenCvSharp.Blob`, `OpenCvSharp.Extensions`, `OpenCvSharp.UserInterface` 참조가 명시적으로 보입니다.
- `MVSDK_Net.dll`은 x64 관리 DLL이므로 App/Vision 프로젝트는 `PlatformTarget=x64`로 고정했습니다.
- 현재 `MVSDKmd.dll`은 프로젝트 Native 폴더에 없습니다. IMV Direct SDK 카메라 제어를 실제 실행하려면 제조사 SDK의 `MVSDKmd.dll`과 종속 DLL을 추가해야 합니다.
