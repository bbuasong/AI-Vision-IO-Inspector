# 전체 이미지/Crop 이미지 이중 VLAD ID 런타임

작성일: 2026-07-20  
상태: C# 런타임 구조 적용, 두 ID native ABI는 AI DLL 확인 필요

## 1. 목적

전체 크기 기준 이미지와 Crop 이미지를 각각 학습/관리하는 AI DLL을 수용하기 위해, VLAD 등록 핸들을 하나가 아닌 두 개로 관리한다.

| 구분 | C# 이름 | 역할 |
| --- | --- | --- |
| 전체 이미지 ID | `FullImageVladId` | 전체 이미지 추론, 현재 RTSP callback 등록, 구버전 DLL 호환 추론의 기준 ID |
| Crop 이미지 ID | `CroppedImageVladId` | Crop 이미지 추론 및 결과/검색 계약에 전달할 별도 ID |

두 ID는 `IntPtr`이며 **해당 WPF 프로세스 안에서만 유효**하다. DB, JSON 설정 파일, 검사 이력, 외부 학습 프로세스에 포인터 값을 저장하거나 전달하면 안 된다.

## 2. 초기화와 RTSP 등록 흐름

```text
프로그램 시작
  -> VladCamModeRuntime.EnsureLoaded
  -> VladSdkSession.EnsureStarted
  -> VLAD_Ops_Ai_Env_Start(..., fullImageModelPath, registerRtsp=true)
  -> FullImageVladId 생성 및 RTSP callback 등록
  -> VLAD_Ops_Ai_Env_Start(..., croppedImageModelPath, registerRtsp=false)
  -> CroppedImageVladId 생성
  -> VladCamModeState에 두 ID 보관
```

`VLAD_Ops_RTSP`는 현재 `ActiveVladId` 하나와 하나의 최신 프레임 캐시만 관리한다. Crop ID까지 RTSP callback을 등록하면 기존 전체 이미지 RTSP 등록 키와 프레임 캐시가 삭제될 수 있다. 따라서 RTSP callback과 6대 카메라 최신 프레임은 `FullImageVladId`에만 등록한다.

Crop 이미지는 검사 시작 시 전체 이미지 프레임에서 AI DLL이 정의한 방식으로 생성/사용해야 한다. 현재 앱은 Crop 좌표·학습 방식 자체를 추측하거나 별도 Crop 하지 않는다.

## 3. 모델 경로 설정

`CFG\Config.json`의 기존 `MODEL`은 전체 이미지 모델 경로로 사용한다. 선택적으로 Crop 전용 모델 경로를 추가할 수 있다.

```json
{
  "CUSTOM": {
    "HD": {
      "MODEL": "RuntimeData/Models/VLAD/Full",
      "CROP_MODEL": "RuntimeData/Models/VLAD/Crop"
    }
  }
}
```

- `CROP_MODEL`이 없거나 비어 있으면 `MODEL`과 같은 경로로 두 번째 ID를 등록한다.
- 실제 AI 모델이 전체/Crop별로 분리되어 있지 않다면 현재처럼 동일 모델 경로를 사용하는 것이 맞다.
- 모델을 분리할지, 동일 모델에서 두 ID만 분리할지는 AI 담당자가 결정해야 한다.

## 4. 검사/결과/검색 호출 원칙

새 HD DLL 목표 ABI는 다음과 같이 두 ID를 명시적으로 받는다.

```c
void* VLAD_HD_Inference_Mat(
    void* fullImageVladId,
    void* croppedImageVladId,
    void* rawData,
    float threshold,
    int drawMode,
    const char* inspectionContextJsonUtf8);

int VLAD_HD_InferenceData_Result(
    void* fullImageVladId,
    void* croppedImageVladId,
    void* detectData,
    void* rawData,
    void* classCount,
    char* resultJsonUtf8,
    int resultJsonCapacity,
    int* requiredResultJsonBytes,
    const char* customParameterUtf8);

void* VLAD_Search_Mat(
    void* fullImageVladId,
    void* croppedImageVladId,
    void* rawData,
    float threshold,
    int drawMode,
    const char* searchContextJsonUtf8);

int VLAD_Search_Data(
    void* fullImageVladId,
    void* croppedImageVladId,
    void* searchData,
    char* resultJsonUtf8,
    int resultJsonCapacity,
    int* requiredResultJsonBytes);
```

현재 `VLAD_SDK.dll`에서 확인된 `VLAD_Inference_Mat`과 `VLAD_Search_*` export는 ID 하나만 받는다. 따라서 현재 코드의 동작은 아래와 같다.

| C# 진입점 | 현재 배포 DLL 호환 동작 | 새 HD DLL 전환 후 동작 |
| --- | --- | --- |
| `VLAD_HD_Inference_Mat(full, crop, ...)` | 전체 이미지 ID로 기존 4인자 `VLAD_Inference_Mat` 호출 | 두 ID를 native export에 함께 전달 |
| `VladInferenceResultParser.Parse(full, crop, ...)` | 전체 이미지 ID로 기존 Draw/TLV 결과 파싱 | `VLAD_HD_InferenceData_Result` JSON을 두 ID로 수신 |
| `VLAD_Search_Mat(full, crop, ...)` | 전체 이미지 ID로 기존 단일-ID export 호출 | 두 ID를 native export에 함께 전달 |
| `VLAD_Search_Data(full, crop, ...)` | 전체 이미지 ID로 기존 단일-ID export 호출 | 두 ID를 native export에 함께 전달 |

`CFG\VladRuntimeSettings.json`의 `UseTestResultJson=true`는 별도 테스트 경로다. 이 경우에는 native DLL을 호출하지 않고 계약 JSON을 반환해 결과 수신 이후의 C# 처리만 검증한다. 실제 검사에서는 기본값 `false`를 사용한다.

즉, C#은 지금부터 두 ID를 생성·보관·전달할 준비가 됐지만, **현재 배포 DLL이 Crop ID를 실제 추론에 사용한다고 보장할 수는 없다.** AI 담당자가 위 ABI의 x64 DLL과 C/C++ 헤더를 제공해야 실제 두 모델/두 입력 추론이 시작된다.

## 5. 학습 프로세스와 재초기화

```text
학습 시작
  -> TrainingProcessService.Start(FullImageVladId, CroppedImageVladId)
  -> Study 프로그램 StandardOutput/StandardError/Exited 수신
  -> DONE + ExitCode 0 + ERROR/CANCELED 없음
  -> Crop ID Unregistration
  -> Full ID Unregistration
  -> Full ID Env_Start + RTSP 재등록
  -> Crop ID Env_Start (RTSP 미등록)
  -> 새 두 ID 저장
```

`TrainingProcessService.Start`는 두 ID가 모두 준비됐는지만 확인한다. 학습 프로그램은 별도 OS 프로세스이므로 `IntPtr` 값을 학습 실행 파일에 전달하지 않는다. 학습 프로그램은 설정된 모델/이미지 폴더와 파일을 처리하고, 완료 뒤 앱이 새 모델을 다시 등록한다.

## 6. 코드 위치

| 역할 | 코드 |
| --- | --- |
| 두 ID 생성/정리 | `Vision/LegacyVlad/VladSdkSession.cs` |
| CAM 모드 상태 보관 | `Vision/LegacyVlad/VladCamModeRuntime.cs` |
| `Env_Start` RTSP 등록 여부 분리 | `Vision/LegacyVlad/VLAD_Ops_Ai.cs` |
| 검사/검색/학습 두 ID 전달 | `Vision/Engines/VladVisionInferenceEngine.cs` |
| 구버전 Draw/TLV 결과 호환 | `Vision/LegacyVlad/VladInferenceResultParser.cs` |
| 학습 외부 프로세스 수명 | `Vision/Services/TrainingProcessService.cs` |
| RTSP 전체 이미지 ID 고정 | `Vision/Services/VisionCameraCoordinator.cs` |

## 7. AI 담당자 확인 및 검증 항목

1. `VLAD_Custom_Registration`을 같은 프로세스에서 두 번 호출하는 것이 SDK/GPU 메모리 기준으로 지원되는지 확인한다.
2. 전체/Crop 모델이 같은 경로인지, `MODEL`과 `CROP_MODEL`처럼 별도 경로가 필요한지 결정한다.
3. 새 HD DLL이 두 ID를 받는 export 이름, 인자 순서, CallingConvention, UTF-8 JSON 규약을 C/C++ 헤더로 제공한다.
4. 두 ID 등록 뒤 각각의 모델이 실제로 구분된 결과를 내는지 콘솔 샘플로 검증한다.
5. 전체 이미지 ID만 RTSP callback을 받는 구조가 DLL의 Crop 처리 방식과 맞는지 확인한다.
6. 학습 완료 후 두 ID가 모두 새 값으로 재등록되고, 이전 ID callback이 무시되는지 6채널 환경에서 확인한다.
