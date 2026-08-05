# RTSP LatestFrame 검사 캡처 구조

- 작성일: 2026-08-05
- 적용 버전: `Codes/Version1_1_0_0/AI-Vision IO Inspector`
- 대상: VLAD RTSP callback 기반 6채널 검사 이미지 캡처

## 변경 이유

기존 검사 경로는 VLAD callback 캐시가 있어도 다음 경우 `ConfiguredCameraService.Capture()`로 전환했습니다.

- Config 해상도가 VLAD callback 고정 해상도보다 큰 경우
- callback 최신 프레임이 없거나 오래된 경우

이 전환 경로는 검사 버튼을 누를 때 채널별 RTSP를 다시 열고 `ffmpeg`, LibVLC, OpenCvSharp 순으로 프레임 캡처를 시도했습니다. 6채널에서 RTSP 재접속, 디코더 시작, 프로세스 생성이 동시에 발생하여 일부 채널 실패와 콘솔 창 표시 가능성이 있었습니다.

## 적용 구조

```text
VLAD RTSP callback
  -> VladId / display 포인터 / 해상도 검증
  -> 카메라별 WriteBuffer에 즉시 Marshal.Copy
  -> CurrentBuffer와 WriteBuffer 교체
  -> SDK 소유 포인터는 callback 밖으로 보관하지 않음

CaptureAll
  -> 사용 RTSP 채널의 LatestFrame 캐시 참조 일괄 확보
  -> 카메라별 짧은 lock에서 CurrentBuffer 복제
  -> 모든 lock 해제
  -> 복제본을 PNG로 저장
  -> 저장 파일을 검사 입력으로 전달
```

카메라별 캐시는 두 개의 고정 배열을 재사용합니다. callback마다 대형 배열을 새로 할당하지 않으므로 6채널 연속 수신 시 LOH/GC 부하를 줄입니다. 검사 시 생성한 복제본은 callback 버퍼와 메모리를 공유하지 않습니다.

## 실패 처리

- 요청한 RTSP 채널 중 최신 프레임이 하나라도 없거나 3초보다 오래되면 일괄 캡처를 실패로 처리합니다.
- 정상 검사 경로에서는 실패 후 RTSP를 새로 열거나 `ffmpeg.exe`로 우회하지 않습니다.
- 일부 파일 저장 중 실패하면 해당 일괄 작업에서 먼저 생성한 파일을 정리하고 오류를 반환합니다.
- 비활성 채널은 기존 worker 구성에서 제외되므로 검사 대상에 포함되지 않습니다.

## 외부 프로세스 창

- 정상 RTSP 검사 캡처에서는 `ffmpeg.exe`를 실행하지 않습니다.
- 연결 시험 등에서 사용하는 `RtspCameraFrameSource`의 ffmpeg 실행은 `UseShellExecute=false`, `CreateNoWindow=true`, `WindowStyle=Hidden`으로 설정했습니다.
- 학습 배치와 Epson OCR 프로세스도 같은 숨김 창 정책을 사용합니다.
- 검사 중 `ptxas.exe`, `cmd.exe`, `conhost.exe`가 VLAD/TensorFlow/CUDA 내부에서 생성되는 경우 애플리케이션이 숨기거나 종료하지 않습니다. 이 프로세스의 창 정책은 VLAD SDK 담당 영역입니다.

## 해상도 적용 규칙

- 각 채널의 `Config.json` `CAM_WIDTH/CAM_HEIGHT`를 RTSP callback 버퍼 복사, LatestFrame 캐시, PNG 저장의 단일 해상도 기준으로 사용합니다.
- `1920x1080` 고정 해석과 기본값 대체는 사용하지 않습니다.
- `CAM_WIDTH` 또는 `CAM_HEIGHT`가 없거나 0 이하이면 해당 채널 등록을 실패 처리하고 로그에 설정 오류를 남깁니다.
- Config 해상도는 NVR이 해당 RTSP URL로 송출하는 실제 해상도와 반드시 일치해야 합니다. callback의 `display` 포인터에는 해상도 메타데이터가 없으므로 값이 다르면 안전하게 자동 판별할 수 없습니다.
- 예: DC-T3145R 채널을 `2592x1944`로 송출하고 Config에도 동일하게 설정하면 callback 복사본과 저장 PNG도 `2592x1944`로 생성합니다.

## 검증 결과

- .NET Framework 4.7.2 / x64 Debug 전체 솔루션 빌드: 경고 0, 오류 0
- 합성 6채널 callback 버퍼의 SDK 원본 배열 변경 후 캐시 독립성 확인
- 이중 버퍼 갱신 전/후 CaptureAll 복제본이 서로 변경되지 않음을 확인
- 복제 프레임 PNG 저장 및 저장 해상도 확인
- 채널별 Config 해상도가 RTSP 등록 파라미터와 LatestFrame 저장 해상도에 그대로 전달되는지 확인
