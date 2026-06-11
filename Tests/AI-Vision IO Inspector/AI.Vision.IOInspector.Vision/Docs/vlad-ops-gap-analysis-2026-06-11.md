# VLAD_Ops 구현 점검 2026-06-11

## 결론

현재 프로젝트는 기존 `VLAD_Ops`의 함수명과 핵심 진입점을 일부 유지했지만, 기존 프로그램을 그대로 옮긴 상태는 아닙니다.

현재 실제 검사 흐름은 다음 구조입니다.

```text
검사 시작 버튼
  -> MainWindowViewModel.ExecuteRunInspection
  -> InspectionWorkflowService.RunInspection
  -> ICameraService.CaptureAll
  -> IAiInferenceService.Inspect
  -> MeasurementService / JudgmentService
  -> 이력 저장 및 UI 갱신
```

`ApplyInspectionPartContext`, `LoadCapturedImages`, `LoadInspectionMeasurements`, `LoadEvents`는 AI로 넘어가는 지점이 아니라, 검사 완료 후 결과를 UI에 반영하는 단계입니다.

## 현재 잘 들어간 부분

| 항목 | 현재 위치 | 판단 |
| --- | --- | --- |
| `VLAD_Ops_Ai_Env_Start` | `LegacyVlad/VLAD_Ops_Ai.cs` | 기존 분기 구조와 유사하게 구현됨 |
| `VLAD_Custom_Registration` | `LegacyVlad/VladNativeMethods.cs` | P/Invoke 선언 있음 |
| `VLAD_Rtsp_Info_Client_Registration` | `LegacyVlad/VLAD_Ops_Ai.cs`, `LegacyVlad/VLAD_Ops_RTSP.cs` | 기존 함수명으로 호출 가능 |
| `VLAD_Ops_RTSP_Thread` | `LegacyVlad/VLAD_Ops_RTSP.cs` | 기존 스레드 진입점 이름 유지 |
| `VLAD_Inference_Mat` | `Engines/VladVisionInferenceEngine.cs` | 촬영 이미지 파일을 Mat로 변환해 호출 |
| `VLAD_InferenceData_*_Draw` | `LegacyVlad/VladInferenceResultParser.cs` | V1/V2 결과 해석 경로 있음 |
| `MVSDK_Net`/`OpenCvSharp` 참조 | `AI.Vision.IOInspector.Vision.csproj` | Visual Studio에서 명시적으로 보임 |

## 2026-06-11 수정한 문제

1. `RtspUrl`이 비어 있으면 검사 시작에서 즉시 실패하던 문제를 수정했습니다.
   현재 설정은 `IpAddress`, `Port`, `StreamPath`로 RTSP URL을 만드는 방식이므로 `RtspUrlBuilder.Build()`를 사용하도록 변경했습니다.

2. VLAD 모델 경로가 없는 경우 카메라 캡처 전 단계에서 실패하지 않도록 했습니다.
   `VLAD RTSP Thread`는 보조 호환 경로이므로 모델 경로가 없으면 시작을 생략하고, 실제 캡처는 기존 RTSP 캡처 경로로 진행합니다.

3. `VLAD_Ops_RTSP.cs`의 깨진 주석/인코딩을 UTF-8 한국어 주석으로 정리했습니다.

4. RTSP 콜백 예외가 프로세스 밖으로 나가지 않도록 방어했습니다.
   Native 콜백에서 예외가 밖으로 나가면 프로그램이 종료될 수 있습니다.

5. RTSP 콜백 Mat 크기를 1920x1080 고정값만 쓰지 않고, 채널 설정 해상도를 받을 수 있게 했습니다.

6. 기존 담당자가 찾기 쉬운 `VLAD_Ops_Ai_Cam_InferenceData` 호환 함수를 추가했습니다.

## 아직 부족한 부분

| 항목 | 이유 | 필요한 결정 |
| --- | --- | --- |
| VLAD RTSP 콜백 결과와 WPF UI 연결 | 기존 WinForms는 콜백에서 직접 `PictureBox`에 표시했지만, 현재 WPF는 `RtspVideoHost`가 별도 표시를 담당함 | RTSP 표시를 WPF LibVLC로 유지할지, VLAD 콜백 프레임으로 통합할지 결정 필요 |
| VLAD RTSP Thread의 정상 종료 | 기존 코드도 등록 후 해제 API가 명확하지 않음 | VLAD SDK의 RTSP 해제/Stop API 확인 필요 |
| 실제 VLAD 모델 경로 | 현재 `CFG/Config.json`의 `MODEL`은 `E:/Tensor_Projects/Ex/Chip/Ex_Weight`이며 개발 PC에는 없음 | 설치 PC 모델 경로 확인 또는 `AI_VISION_VLAD_MODEL_PATH` 환경변수 지정 |
| detectData 치수 포맷 | VLAD 기본 결과는 class/score/bbox 중심으로 보이며 길이/너비/높이/두께 확정 포맷은 아직 불명확 | AI 담당자와 측정값 반환 규격 확정 필요 |
| 카메라별 실제 해상도/픽셀포맷 | RTSP 콜백 포인터가 항상 BGR 8UC3인지, 설정 해상도와 같은지 장비 검증 필요 | 실제 NVR 스트림으로 장시간 테스트 필요 |

## 검사 시작 오류 원인

가장 가능성이 높았던 원인은 두 가지입니다.

1. `RtspUrl` 빈 값 문제
   `camera-config.json`은 `RtspUrl`을 비워두고 `StreamPath`만 사용합니다. 기존 `CaptureVladRtsp`는 이 값을 빈 값으로 보고 예외를 냈습니다. 이 문제는 수정했습니다.

2. VLAD 모델 경로 없음
   현재 `E:/Tensor_Projects/Ex/Chip/Ex_Weight` 경로가 개발 PC에 없습니다. 이 경우 AI 추론 단계에서 `VLAD 모델 경로를 찾을 수 없습니다`로 Error가 나는 것이 정상입니다. 설치 PC에 E/H 드라이브와 모델이 준비되면 이 부분은 통과해야 합니다.

## 권장 방향

현재 단계에서는 화면 표시와 캡처는 WPF/RTSP 경로를 유지하고, AI 판정은 `VladVisionInferenceEngine`에서 촬영 이미지 파일을 받아 처리하는 구조가 가장 안전합니다.

AI 담당자가 기존 `VLAD_Ops` 방식으로 직접 옮겨야 한다면 우선 수정 대상은 다음 파일입니다.

- `LegacyVlad/VLAD_Ops_Ai.cs`
- `LegacyVlad/VLAD_Ops_RTSP.cs`
- `Engines/VladVisionInferenceEngine.cs`
- `Services/VisionAiInferenceService.cs`
- `Models/VisionInspectionInput.cs`
- `Models/VisionInspectionOutput.cs`
