# 미구현/미비 항목 추적

## 운영 원칙

- 이 문서는 날짜별로 남은 미구현/미비 항목을 확인하고 하나씩 클리어하기 위한 기준 문서다.
- `대기`, `보류`, `확인필요`처럼 원인을 알 수 없는 상태명만 단독으로 쓰지 않는다.
- 상태는 `완료`, `진행중`, `미구현-내부작업`, `미구현-외부정보필요`, `부분완료-검증필요`, `보류-범위조정` 중 하나로 기록한다.
- 외부 답변이 필요한 항목도 내부에서 미리 준비할 수 있는 설계/어댑터/검증 코드는 별도 작업으로 분리한다.
- 항목이 완료되면 완료일과 근거 파일 또는 빌드/테스트 결과를 남긴다.

## 2026-05-31 현재 남은 핵심 항목

| ID | 항목 | 현재 상태 | 방향성 | 다음 작업 | 완료 기준 |
| --- | --- | --- | --- | --- | --- |
| O-001 | 실제 카메라 SDK/RTSP 연결 | 진행중 | 측정 원본은 최종적으로 Direct SDK 우선이지만, 현재 연결된 IDIS 장비는 RTSP로 먼저 수신 검증한다. | 옵션 UI에 IP/Port/계정/StreamPath 저장과 실제 RTSP 연결 테스트, ffmpeg 기반 1프레임 캡처 경로 추가 완료. 다음은 현장 IP/계정 입력 후 캡처 검증 | 실제 카메라 1대 연결, 프레임 수신, 해상도/픽셀포맷 로그 확인 |
| O-002 | 연속 영상 미리보기 | 진행중 | `Capture` 요청 Worker와 별도로 최신 프레임 유지 Worker를 둔다. UI는 최신 프레임만 표시하고 모든 프레임을 파일로 저장하지 않는다. | `VisionCameraReceiveWorker` 뼈대 추가 완료. 다음은 실제 SDK 또는 메모리 프레임 소스와 `VisionCameraCoordinator` 연결 | 시뮬레이션/파일 소스로 6채널 최신 프레임 갱신 검증 |
| O-003 | 트리거 방식 | 미구현-외부정보필요 | Continuous, Software, Line1 중 현장 방식에 맞춘다. 내부 코드는 `CameraTriggerMode`로 분기 가능하게 유지한다. | 현장 장비 구성에서 6대 동시 트리거인지 순차 트리거인지 확인 | 옵션 설정값으로 트리거 모드 저장 및 SDK 호출 경로 확인 |
| O-004 | pixel-to-mm 카메라 보정 | 미구현-내부작업 | AI가 pixel 값을 주는 경우를 대비해 mmPerPixel 또는 homography 보정 모델을 둔다. 단위 변환은 이미 Application 계층에서 수행한다. | `Calibration` 모델/저장소/보정값 적용 위치 설계 | 보정값이 있는 측정부는 pixel 측정값을 mm 기준으로 판정 |
| O-005 | 렌즈 왜곡 보정 | 미구현-외부정보필요 | 카메라별 보정판 촬영 결과를 기준으로 왜곡 보정 파라미터를 관리한다. | 보정판 종류, 촬영 절차, 담당 범위 확인 | 카메라별 보정 파라미터 저장 및 적용 경로 정의 |
| O-006 | VLAD 실제 모델 등록/추론 | 미구현-외부정보필요 | `VLAD_Ops_Ai_Compat` 이름을 유지하고 실제 호출은 `VladRuntimeContext`에 넣는다. | VLAD 모델 파일, GPU 사용 여부, Register/WarmUp/Release 필요 API 확인 | 실제 DLL 호출로 모델 등록 후 샘플 이미지 1장 추론 성공 |
| O-007 | VLAD/AI 결과 파싱 | 미구현-외부정보필요 | detect data를 `VisionInspectionOutput.Measurements`로 변환한다. 측정부 ID와 NG 원인을 연결해야 한다. | AI 담당자에게 class/bbox/mask/keypoint/치수 반환 스키마 확인 | 측정부별 측정값, 기준값, 판정, NG 항목이 이력에 저장 |
| O-008 | NG 이미지/원본 이미지 보존 정책 | 부분완료-검증필요 | 이력 DB 보존 정책은 있으나 이미지 파일 보존 기간과 OK/NG 차등 정책은 고객 확정이 필요하다. | 고객/품질 담당에게 OK/NG 이미지 보존 기간과 디스크 한계 확인 | 보존 설정값으로 오래된 이미지와 DB 이력을 일관 삭제 |
| O-009 | 바코드/라벨 입력 방식 | 미구현-외부정보필요 | 1차는 품번 입력 TextBox를 유지하고, 스캐너가 키보드 웨지면 별도 SDK 없이 처리한다. | 스캐너 입력 방식 확정 | 스캔 값으로 부품 조회와 검사 시작 흐름 검증 |
| O-010 | 실제 장비 PC 배포 검증 | 부분완료-검증필요 | self-contained win-x64 배포와 Native 폴더 구조는 준비했다. 장비 PC에서 DLL/드라이버가 로드되는지 확인해야 한다. | 장비 PC에서 publish 산출물 실행 및 Native DLL 로딩 로그 확인 | 개발툴 없이 EXE 실행, DB/Native/RuntimeData 경로 정상 |
| O-011 | 통계 화면 운영 검증 | 부분완료-검증필요 | 기본 통계 화면은 구현되어 있으나 고객이 원하는 기간/품목 필터 수준은 추가 확인한다. | 통계 필터 요구 확인 | 등록 부품수, 검사실적, OK/NG, 평균 검사시간이 고객 기준으로 표시 |
| O-012 | Excel 직접 업로드 | 보류-범위조정 | 현재는 CSV 다중등록이 구현되어 있다. xlsx/xlsm/xlsb 직접 지원은 1차 범위 여부를 확인한다. | 고객/PM에게 Excel 직접 지원 필요성 확인 | 1차 제외 또는 직접 업로드 구현 범위 확정 |

## 2026-05-31 바로 수행할 내부 작업 순서

1. `VisionCameraReceiveWorker`를 `VisionCameraCoordinator`에 연결하고, 실제 SDK 연결 전에는 파일 저장 없이 최신 프레임을 공급할 수 있는 테스트 소스를 준비한다.
2. `Calibration` 데이터 모델을 설계해 pixel-to-mm 보정값을 어디에 저장하고 적용할지 정한다.
3. `VladRuntimeContext` 안에 실제 VLAD API 연결 순서 주석과 예외/로그 정책을 더 구체화한다.
4. 이미지 보존 정책을 `HistoryRetentionOptions`와 연결할 수 있는 설정 항목으로 분리한다.
5. 장비/AI 담당자에게 답을 받아야 하는 항목은 `questions.md`와 이 문서의 ID를 같이 사용해 추적한다.
## 2026-06-05 장비 연결 잔여 항목

| ID | 항목 | 현재 상태 | 확인 결과 | 다음 작업 | 완료 기준 |
| --- | --- | --- | --- | --- | --- |
| O-001A | NVR RTSP/HTTP 사용자 권한 | 미구현-외부정보필요 | OpenCvSharp 런타임 로드와 `192.168.1.230:554` 포트 접근은 성공했지만 RTSP DESCRIBE 단계에서 `401 Unauthorized`가 발생했다. | NVR 설정에서 RTSP 전용 사용자 또는 기존 사용자에 `Use RTSP/HTTP` 권한을 부여하고, RTSP/HTTP 비밀번호를 재설정한 뒤 옵션 UI의 User/Password에 반영한다. | 프로그램의 `상태 새로고침` 또는 `카메라 화면 갱신`에서 Top 채널 프레임 파일이 생성되고 UI에 표시된다. |
| O-001B | RTSP 경로 확정 | 부분완료-검증필요 | IDIS 문서 기준 카메라 직접 접속은 `trackID=1~3`, NVR/Recorder 경유는 `trackID=채널&streamID=스트림` 형식이 가능하다. 현재 Top은 `trackID=1&streamID=1`로 설정되어 있다. | RTSP 권한 해결 후 `trackID=1`, `trackID=1&streamID=1`, 보조 스트림 `streamID=2`를 VLC와 프로그램에서 순서대로 검증한다. | 6개 위치별 ViewType과 NVR 채널/스트림 경로가 옵션 설정으로 고정된다. |
| O-010A | LibVLC/ffmpeg RTSP 런타임 배포 | 부분완료-검증필요 | VLAD OpenCvSharp는 .NET 9 호환 불가로 확인했다. RTSP 수신은 `RuntimeData\Native\LibVLC`의 LibVLCSharp/LibVLC를 우선 사용하고, ffmpeg.exe를 대체 경로로 사용한다. | 배포 패키지에 `RuntimeData\Native\LibVLC` 또는 `RuntimeData\Native\FFmpeg\ffmpeg.exe`를 포함하고, zip/메일로 받은 DLL은 `Unblock-File` 또는 파일 속성의 차단 해제를 적용한다. | 개발툴 없는 장비 PC에서 EXE 실행 후 RTSP 프레임 수신이 성공한다. |
