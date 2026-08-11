# 외부 이미지 학습 프로세스 연동

기준일: 2026-07-20

## 목적

`StartImageTraining()`이 `CFG/VladRuntimeSettings.json`의 학습 프로그램을 실행하고 다음 정보를 WPF 옵션 화면에 표시한다.

- `StandardOutput`
- `StandardError`
- `Process.Exited`
- 현재 상태와 진행률
- 학습 완료 후 VLAD 재초기화 결과

## 실행 흐름

```text
옵션 > 학습 바로시작 / 1회 예약 / 매일 예약
  -> MainWindowViewModel.StartImageTraining
  -> VisionAiInferenceService.StartImageTraining
  -> VisionInferenceWorker.StartImageTraining
  -> VladVisionInferenceEngine.StartImageTraining
  -> TrainingProcessService.Start(FullImageVladId, CroppedImageVladId)
  -> VLAD_Ops_Ai.CreateImageTrainingStartInfo
  -> CFG/VladRuntimeSettings.json의 StudyBatchFilePath 실행
```

`TrainingProcessService`는 `UseShellExecute=false`, `RedirectStandardOutput=true`, `RedirectStandardError=true`, `EnableRaisingEvents=true`로 실행한다.

현재 설정의 `Tests/ToolsV2/ai_train.bat`는 확장자와 달리 `MZ` 헤더를 가진 Windows PE 실행 파일이다. 파일 메타데이터의 원본 파일명은 `ExternalTraining.Sample.exe`, 제품명은 `Training Process Sample`이므로 현재 파일은 실제 AI 학습기가 아니라 연동 검증용 샘플이다. 프로그램은 파일 헤더를 확인하여 PE이면 직접 실행하고, 실제 텍스트 배치 파일이면 `cmd.exe /c`로 실행한다.

## 출력 프로토콜

학습 프로그램은 한 줄 단위로 다음 형식을 출력한다.

```text
TYPE|VALUE|MESSAGE
```

| TYPE | 의미 |
| --- | --- |
| `START` | 학습 시작 |
| `PROGRESS` | `VALUE`를 0~100 진행률로 사용 |
| `DONE` | 학습 데이터 생성 완료 |
| `ERROR` | 학습 실패 |
| `CANCELED` | 학습 취소 |
| `WARN` | 경고 |
| `LOG` | 일반 진행 로그 |

`StandardError`는 진단 정보일 수 있으므로 수신만으로 실패를 확정하지 않는다. 최종 성공은 `DONE` 수신, 종료 코드 0, `ERROR/CANCELED` 미수신을 모두 만족해야 한다.

## 학습 완료 후 VLAD 재초기화

`DONE`을 받는 즉시 VLAD를 다시 시작하지 않는다. 학습 프로세스가 모델 파일을 모두 닫고 종료한 뒤 다음 순서로 처리한다.

```text
DONE StandardOutput 수신
  -> Process.Exited 대기
  -> ExitCode == 0 확인
  -> 이전 RTSP callback/최신 프레임 캐시 차단
  -> VLAD_Unregistration(CroppedImageVladId)
  -> VLAD_Unregistration(FullImageVladId)
  -> VLAD_Ops_Ai_Env_Start(전체 이미지 모델, RTSP 등록)
  -> VLAD_Ops_Ai_Env_Start(Crop 이미지 모델, RTSP 미등록)
  -> 새 FullImageVladId / CroppedImageVladId 저장
  -> Config.json의 활성 RTSP 채널을 새 FullImageVladId로 재등록
  -> 검사 가능 상태
```

검사와 재초기화는 `VladRuntimeLifecycleService.OperationSyncRoot`로 직렬화한다. 학습 중에는 새 검사를 시작하지 않으며, 이전 FullImageVladId에서 늦게 도착한 RTSP callback은 무시한다. 현재 RTSP callback은 전체 이미지 ID 하나에만 등록한다.

`IntPtr` VladId는 앱 프로세스 내부 핸들이므로 별도 학습 프로세스의 명령행이나 환경변수로 전달하지 않는다. `TrainingProcessService.Start`가 두 ID를 받는 목적은 학습 시작 전 두 런타임이 모두 준비됐는지 확인하는 것이다.

다음 조건에서는 재초기화를 수행하지 않는다.

- `DONE`을 받지 못함
- 종료 코드가 0이 아님
- `ERROR` 또는 `CANCELED`를 받음

## 옵션 UI

- `학습 바로시작`: 즉시 실행
- `1회 예약`: `yyyy-MM-dd HH:mm`에 한 번 실행
- `매일 학습`: `HH:mm`에 날짜당 한 번 실행
- `Grid 지우기`: 화면의 수신 정보만 삭제
- 현재 상태, 진행률, 현재 메시지, 오류 코드/메시지, 시작/종료/경과시간 표시
- 수신 Grid: 시간, Source, Type, Value, Message, Raw

## 주요 코드

| 역할 | 코드 |
| --- | --- |
| 외부 프로세스 실행/출력 수신 | `Vision/Services/TrainingProcessService.cs` |
| Unregister/Env_Start/RTSP 재등록 | `Vision/Services/VladRuntimeLifecycleService.cs` |
| 학습 종료 성공 판단 | `Vision/Engines/VladVisionInferenceEngine.cs` |
| Study 실행 경로 생성 | `Vision/LegacyVlad/VLAD_Ops_Ai.cs` |
| 이전 RTSP callback 차단 | `Vision/LegacyVlad/VLAD_Ops_RTSP.cs` |
| 옵션 UI 상태와 예약 | `App/ViewModels/MainWindowViewModel.cs` |

## 남은 현장 검증

1. 실제 학습 프로그램이 UTF-8로 `DONE|100|완료`를 출력하는지 확인한다.
2. 정상 종료 후 `VLAD_Unregistration`이 `true`를 반환하는지 확인한다.
3. 새 `VLAD_Ops_Ai_Env_Start`가 0이 아닌 새 `FullImageVladId`와 `CroppedImageVladId`를 모두 반환하는지 확인한다.
4. 새 모델로 추론 결과가 달라지는지 확인한다.
5. 재초기화 후 전체 이미지 ID 기준으로 6개 RTSP 채널이 다시 프레임을 수신하는지 확인한다.
6. 검증용 `ExternalTraining.Sample.exe`를 실제 AI 학습 프로그램 또는 배치 파일로 교체한다.
