# CUDA PTXAS Runtime Check - 2026-07-20

## 확인 결과

- 현재 C# 코드에서 `cmd.exe`를 직접 실행하는 경로는 `StartImageTraining()`의 `Study.bat` 실행뿐이다. 이 경로는 `UseShellExecute=false`, `CreateNoWindow=true`로 실행된다.
- `ffmpeg.exe`도 동일하게 창을 숨겨 실행되며, 현재 RTSP 검사 캡처는 `CaptureAll()` -> `CaptureVladRtsp()` -> `VLAD_Ops_RTSP.TrySaveLatestFrame()`의 callback 최신 프레임 저장 경로를 사용한다. 검사 시 RTSP를 새로 열어 `ffmpeg.exe`를 실행하지 않는다.
- `InspectCapturedImages()`는 Timer에서 호출되지 않는다. `검사 시작` -> `RunInspection()` -> `VisionInferenceWorker` 요청 -> `InspectCapturedImages()` 순서로만 한 번 실행되며, 등록된 카메라 이미지 수만큼 `VLAD_HD_Inference_Mat` 또는 호환 `VLAD_Inference_Mat`을 호출한다.
- x64 Debug 실행 검증에서 `VLAD_Custom_Registration` 중 일시적인 `conhost.exe` 생성이 관찰됐다. C# 소스에는 이 콘솔 호스트나 `ptxas.exe`를 직접 실행하는 코드가 없다.

따라서 검사 또는 초기화 중 보이는 검은 콘솔 창은 VLAD_SDK.dll 내부의 TensorFlow/CUDA 런타임이 GPU PTX 커널을 컴파일할 때 실행하는 `ptxas.exe`와 그 콘솔 호스트로 분류한다. 이는 기존 VLAD DLL 호출의 내부 동작이며 WPF의 `CreateNoWindow` 설정으로 직접 숨길 수 없다.

## 적용한 완화 조치

`CFG\VladRuntimeSettings.json`에 아래 설정을 추가했다.

```json
"CudaCacheDirectoryPath": "%LOCALAPPDATA%\\AI-Vision IO Inspector\\CudaCache"
```

`VladRuntimeSettings.ApplyVladSdkDllDirectory()`는 `VLAD_Custom_Registration` 전에 해당 폴더를 만들고, 현재 프로세스의 `CUDA_CACHE_PATH` 환경 변수로 설정한다.

- 실제 확인 경로: `C:\Users\user\AppData\Local\AI-Vision IO Inspector\CudaCache`
- 최초 실행 또는 새 CUDA 커널을 처음 사용할 때는 `ptxas.exe`가 발생할 수 있다.
- 같은 GPU/드라이버/커널 조합의 다음 실행에서는 CUDA 캐시가 재사용되어 반복 실행이 줄어드는 것이 기대 결과다.

## 재확인 기준

1. 실행 중 `DB\Logs\vlad-startup.log`의 `SET_VLAD_DLL_DIRECTORY` 항목에서 `ActiveCudaCachePath`가 위 경로인지 확인한다.
2. 검사 시작 후 첫 1회와 같은 조건의 두 번째 검사에서 콘솔 창 반복 횟수와 `ptxas.exe` 발생 여부를 비교한다.
3. 캐시 경로가 정상인데도 매 검사마다 계속 발생하면 VLAD DLL이 TensorFlow 그래프/세션을 Mat 호출마다 새로 생성하는지 AI 담당자가 DLL 내부에서 확인해야 한다. C# 호출부는 `VladId`를 재사용하며 등록을 매 검사마다 반복하지 않는다.
