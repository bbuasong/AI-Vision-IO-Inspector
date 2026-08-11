# Training Process StandardOutput WPF Sample

이 폴더는 `StartImageTraining`에서 외부 Training 프로그램을 실행하고, 외부 프로그램의 `StandardOutput`, `StandardError`, `Process.Exited`를 WPF 화면에서 확인하기 위한 설명용 샘플입니다.

두 프로그램은 각각 자기 폴더 안의 별도 `.sln`으로 관리합니다. 루트 폴더에는 통합 솔루션을 두지 않습니다.

```text
06-training
 ├─ TrainingProcessMonitor.Wpf
 │   ├─ TrainingProcessMonitor.Wpf.sln
 │   └─ TrainingProcessMonitor.Wpf.csproj
 └─ ExternalTraining.Sample
     ├─ ExternalTraining.Sample.sln
     └─ ExternalTraining.Sample.csproj
```

## 프로그램 역할

### TrainingProcessMonitor.Wpf

WPF Monitor 프로그램입니다.

- `Start` 버튼을 누르면 `StartImageTraining()` 함수가 호출됩니다.
- `StartImageTraining()`은 `ExternalTraining.Sample.exe`를 별도 프로세스로 실행합니다.
- 실행 시 `UseShellExecute = false`, `RedirectStandardOutput = true`, `RedirectStandardError = true`, `EnableRaisingEvents = true`를 사용합니다.
- Training 프로그램에서 받은 stdout/stderr/process 이벤트를 화면 하단 `DataGrid`에 행 단위로 표시합니다.
- `DONE` 또는 `ERROR` 없이 프로세스가 종료되면 `Process.Exited`에서 비정상 종료로 보정합니다.

### ExternalTraining.Sample

외부 Training 프로그램입니다. 이 프로그램도 WPF UI입니다.

- WPF Monitor에서 전달받은 `--jobId`, `--input`, `--output`, `--log` argument를 화면에 표시합니다.
- 각 버튼을 누르면 현재 진행 상태를 `StandardOutput`으로 내보냅니다.
- `STDERR 진단` 버튼은 `StandardError`로 진단 로그를 보냅니다.
- 보낸 메시지는 자체 화면의 Grid에도 표시됩니다.

## 메시지 버튼

External Training 프로그램의 버튼은 종류별로 묶어서 배치했습니다.

```text
정상 흐름: Start -> Progress -> Done
START 0              -> START|0|작업 시작
PROGRESS 10          -> PROGRESS|10|이미지로딩중
PROGRESS 30          -> PROGRESS|30|데이터셋 구성 중
PROGRESS 60          -> PROGRESS|60|모델 학습 중
PROGRESS 90          -> PROGRESS|90|모델 저장 중
DONE 100             -> DONE|100|완료

정보 / 경고 / 진단
LOG 이미지 로딩      -> LOG|0|이미지 120장을 로딩했습니다
WARN W001            -> WARN|W001|일부 이미지가 제외되었습니다
STDERR 진단          -> DEBUG|0|학습 내부 진단 로그 예시입니다

오류
ERROR E001           -> ERROR|E001|학습 이미지 개수 부족
ERROR E004           -> ERROR|E004|학습 중 예외가 발생했습니다

취소 / 비정상 종료
CANCELED             -> CANCELED|0|사용자 요청으로 학습이 취소되었습니다
메시지 없이 종료     -> DONE/ERROR 없이 종료되는 상황 확인
```

## 실행 순서

1. `ExternalTraining.Sample\ExternalTraining.Sample.sln`을 열어 빌드합니다.
2. `TrainingProcessMonitor.Wpf\TrainingProcessMonitor.Wpf.sln`을 열어 빌드합니다.
3. `TrainingProcessMonitor.Wpf`를 실행합니다.
4. Monitor 화면에서 `Start` 버튼을 누릅니다.
5. External Training UI가 뜨면 진행 단계 버튼을 누릅니다.
6. Monitor 화면의 `Training 프로그램에서 받은 정보 Grid`에 stdout/stderr/process 정보가 표시되는지 확인합니다.

Monitor 프로젝트는 External 프로젝트를 `ProjectReference`로 묶지 않습니다. 다만 Monitor 실행 편의를 위해, 빌드 시 이미 생성되어 있는 `..\ExternalTraining.Sample\bin\{Configuration}\ExternalTraining.Sample.exe`를 Monitor 출력 폴더로 복사합니다.

## 빌드 방법

Visual Studio에서 각 `.sln`을 따로 열어 빌드하거나 아래 명령을 사용할 수 있습니다.

```text
"C:\Program Files (x86)\Microsoft Visual Studio\2017\Professional\MSBuild\15.0\Bin\MSBuild.exe" ExternalTraining.Sample\ExternalTraining.Sample.sln /p:Configuration=Debug /p:Platform="Any CPU"
"C:\Program Files (x86)\Microsoft Visual Studio\2017\Professional\MSBuild\15.0\Bin\MSBuild.exe" TrainingProcessMonitor.Wpf\TrainingProcessMonitor.Wpf.sln /p:Configuration=Debug /p:Platform="Any CPU"
```

old-style .NET Framework WPF 프로젝트이므로 `dotnet build`보다 Visual Studio/MSBuild 빌드를 권장합니다.

## 자동 검증용 옵션

작업자 설명용 UI 버튼과 별개로, 실행 오류 검증을 위해 자동 옵션을 넣었습니다.

External Training 단독 자동 송신:

```text
ExternalTraining.Sample.exe --jobId verify --input "D:\input" --output "D:\output" --log "D:\log" --autoEmit true
```

Monitor 통합 자동 검증:

```text
TrainingProcessMonitor.Wpf.exe --autoTest
```

`--autoTest`는 Monitor가 Training 프로그램을 실행하고, Training 프로그램은 `--autoEmit true`로 자동 메시지를 내보낸 뒤 종료합니다.

## 검증 결과

현재 샘플은 다음 검증을 완료했습니다.

```text
1. ExternalTraining.Sample.sln Debug 빌드 성공: warning 0, error 0
2. TrainingProcessMonitor.Wpf.sln Debug 빌드 성공: warning 0, error 0
3. ExternalTraining.Sample.exe 실제 실행 및 stdout 자동 송신 성공
4. TrainingProcessMonitor.Wpf.exe --autoTest 통합 실행 성공
5. Monitor 로그에서 START, PROGRESS, DONE, Process.Exited 수신 확인
```

