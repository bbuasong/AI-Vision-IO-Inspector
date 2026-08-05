# SDK 런타임 책임 경계

- 작성일: 2026-08-05
- 적용 버전: `Codes/Version1_1_0_0/AI-Vision IO Inspector`

## 애플리케이션 책임

- EXE 기준 `Native/VLAD` DLL 탐색 경로 준비
- CUDA 런타임 DLL 탐색 경로 준비
- Config의 모델, GPU ID, RTSP URL, 해상도 값을 SDK API에 전달
- `VLAD_Custom_Registration`, RTSP 등록, 추론, 결과 조회, Unregistration API 호출
- SDK callback 포인터를 계약된 크기로 즉시 복사하고 애플리케이션 소유 메모리로 관리
- SDK 반환 코드와 결과 JSON을 파싱하여 UI와 이력에 전달

## SDK 담당 영역

- TensorFlow/CUDA 초기화 및 GPU 메모리 운용
- `ptxas.exe`, `cmd.exe`, `conhost.exe` 등 SDK 내부 자식 프로세스 실행 정책
- 모델 로드, 추론, 학습 산출물 처리
- SDK 내부 스레드, 메모리 할당 및 해제

애플리케이션은 SDK 내부에서 생성한 프로세스를 숨기거나 종료하지 않습니다. `CUDA_CACHE_PATH`, `TF_FORCE_GPU_ALLOW_GROWTH`, `CUDA_DEVICE_ORDER`, `CUDA_VISIBLE_DEVICES`도 애플리케이션에서 강제로 변경하지 않습니다. GPU 선택은 Config의 `GPU_ID`를 SDK 등록 인자로 전달하는 방식만 사용합니다.

## 애플리케이션 소유 외부 프로세스

- Epson OCR API
- 학습 배치 프로그램
- RTSP 연결 시험용 ffmpeg

위 프로세스는 애플리케이션이 `Process.Start`로 직접 생성하므로 숨김 실행, timeout, 종료 및 Dispose를 애플리케이션에서 관리합니다. 정상 검사 캡처는 LatestFrame callback 캐시를 사용하므로 ffmpeg 프로세스를 실행하지 않습니다.

## Vision Worker 구성

- 별도 실행 파일이었던 `AI.Vision.IOInspector.VisionWorker` 프로젝트는 현재 WPF 실행 경로에서 참조되지 않아 2026-08-05 제거했습니다.
- `AI.Vision.IOInspector.Vision` 프로젝트의 `VisionInferenceWorker`는 검사 요청을 UI 스레드 밖에서 처리하는 내부 작업 스레드이므로 유지합니다.
- `VisionCameraCaptureWorker`는 카메라 프레임 수신을 담당하는 내부 작업 스레드이므로 유지합니다.
- 위 내부 작업 스레드는 SDK 내부 프로세스나 스레드를 숨기거나 종료하지 않습니다.
