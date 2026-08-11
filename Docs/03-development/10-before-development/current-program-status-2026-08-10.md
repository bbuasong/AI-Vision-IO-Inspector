# 현재 프로그램 분석 현황

기준일: 2026-08-10

분석 대상은 `Codes\AI-Vision IO Inspector`의 현재 소스입니다. 이번 문서는 코드 수정 없이 정적 분석으로 확인한 내용을 최신 기준으로 정리합니다. 실제 장비 연결, VLAD DLL 실호출, 빌드 실행 검증은 이번 작업 범위에 포함하지 않았습니다.

## 1. 현재 기준 요약

| 항목 | 현재 상태 |
| --- | --- |
| 솔루션 | `AI.Vision.IOInspector.sln` |
| 프로젝트 | `App`, `Application`, `Domain`, `Infrastructure`, `Vision` 5개 프로젝트 |
| Target Framework | .NET Framework 4.7.2 |
| 빌드/배포 기준 | Windows x64, `win-x64`, Version `1.1.0.0` |
| 기준 설정 | `CFG\Config.json`의 `LAST_UI=CUSTOM`, `LAST_USER=HD` |
| VLAD 모델 경로 | `RuntimeData/Models/VLAD/Ex_Weight` |
| VLAD 테스트 JSON | `CFG\VladRuntimeSettings.json` 기준 `UseTestResultJson=false` |
| 카메라 구성 | Top, Front, Back, Left, Right, Thickness 6채널 모두 `CAM_ENABLED=true` |
| 검사 점수 기준 | `INSPECTION_PASS_SCORE_THRESHOLD=95.00` |
| 유사도 검색 기준 | `SINGLE_PART_SIMILARITY_THRESHOLD=99.00` |

## 2. 프로젝트 역할

| 프로젝트 | 역할 |
| --- | --- |
| `AI.Vision.IOInspector.App` | WPF UI, ViewModel, 팝업, RTSP 라이브 화면, OCR/부품등록 화면 제어 |
| `AI.Vision.IOInspector.Application` | 검사 워크플로우, 판정, 측정부 비교, 통계 서비스 |
| `AI.Vision.IOInspector.Domain` | Part, PartImage, MeasurementRegion, Inspection 등 핵심 모델과 파일명 정책 |
| `AI.Vision.IOInspector.Infrastructure` | SQLite, 기준 이미지 파일 저장, OCR, 카메라 설정, 이력 보존 |
| `AI.Vision.IOInspector.Vision` | VLAD SDK 연동, MAT JSON 요청/결과 파싱, RTSP callback 프레임 캐시 |

## 3. 프로그램 시작 및 의존성 구성

`AppBootstrapper`는 시작 시 다음 서비스를 조립합니다.

1. `NativeDependencyLoader`로 Native/VLAD, Epson OCR, LibVLC 등 네이티브 의존성 경로를 준비합니다.
2. `VisionRuntimeFactory.BeginInitializeVladRuntimeOnStartup()`로 VLAD 런타임 초기화를 백그라운드 예약합니다.
3. SQLite 기준정보/이력 저장소를 생성합니다.
4. `VisionRuntimeFactory`에서 카메라 서비스와 AI 추론 서비스를 생성합니다.
5. `LocalReferenceImageFileService`, `VladImageMergeService`, `WpfReferenceCoordinateImageService`, `EpsonEsC320wOcrService` 등을 MainWindowViewModel에 주입합니다.

근거 파일:

- `Codes\AI-Vision IO Inspector\AI.Vision.IOInspector.App\AppBootstrapper.cs`
- `Codes\AI-Vision IO Inspector\AI.Vision.IOInspector.Infrastructure\Services\NativeDependencyLoader.cs`
- `Codes\AI-Vision IO Inspector\AI.Vision.IOInspector.Vision\VisionRuntimeFactory.cs`

## 4. 검사 워크플로우

현재 검사 흐름은 `InspectionWorkflowService.RunInspection()` 기준입니다.

1. 입력 품번으로 기준정보를 조회합니다.
2. 품번이 없으면 검사 결과를 `ERROR`로 저장하고 종료합니다.
3. 측정부가 등록된 품목이면 검사 시점의 Thickness 기준 이미지 경로를 coordinate 이미지로 대체합니다.
4. 활성 카메라의 최신 프레임을 캡처합니다.
5. 캡처 이미지를 VLAD MAT API로 검사합니다.
6. MAT JSON 결과에서 이미지 판정, score, dimensions, measurements를 파싱합니다.
7. AI 측정값을 DB 측정부 기준정보와 연결합니다.
8. 최종 OK/NG/ERROR를 판정하고 이력/이미지/이벤트를 저장합니다.

확인된 구현 포인트:

- `RunInspection()`은 기준정보 미등록 시 `ERROR`로 종료합니다.
- 측정부가 있는 품목은 `ReplaceThicknessReferencePathWithCoordinate()`에서 `{품번}_coordinate.png`를 우선 사용하고, 없으면 `coordinate.png`를 fallback으로 사용합니다.
- coordinate 이미지를 찾지 못하면 기존 Thickness 이미지를 사용하며 Warning 이벤트를 남깁니다.

근거 파일:

- `Codes\AI-Vision IO Inspector\AI.Vision.IOInspector.Application\Services\InspectionWorkflowService.cs`
- `Codes\AI-Vision IO Inspector\AI.Vision.IOInspector.Domain\Models\ReferenceImageFileNamePolicy.cs`

## 5. VLAD MAT JSON 방식 반영 상태

최신 기준은 별도 Result 값을 받지 않고 `VLAD_HD_Inference_Mat`에 전달한 JSON 버퍼를 DLL이 in-place로 갱신하는 방식입니다.

### 5.1. `VLAD_Custom_Registration`의 `modelPath`

현재 소스 기준 `Config.json`의 `MODEL` 값은 `RuntimeData/Models/VLAD/Ex_Weight`입니다.

전달 경로 결정 순서는 다음과 같습니다.

1. `VladVisionSettings.Load()`가 EXE 폴더의 `CFG\Config.json`을 읽습니다.
2. 환경변수 `AI_VISION_VLAD_MODEL_PATH`가 있으면 이 값이 `Config.json`의 `MODEL`보다 우선합니다. 현재 확인한 셸 환경에서는 설정되어 있지 않습니다.
3. `MODEL`이 상대경로이면 `AppContext.BaseDirectory` 기준 절대경로로 변환합니다.
4. `VLAD_Ops_Ai_Env_Start()`가 `EnsureTrailingSlash()`를 적용합니다.
5. 최종적으로 `VLAD_Custom_Registration(customId, "CUSTOM", null, "HD", modelPathWithTrailingSlash, "{\"MODEL\":0,\"CAM\":0}", gpuId)` 형태로 호출합니다.

2026-08-10 실행 로그 기준 실제 전달값은 다음입니다.

```text
C:\LinkGenesis\AI-Vision IO Inspector\Run_2608010b\RuntimeData\Models\VLAD\Ex_Weight\
```

코드에서 확인한 현재 개발 소스의 `CFG\Config.json`을 소스 루트 기준으로 해석하면 다음 위치와 대응됩니다.

```text
C:\SVN_LinkGenesis\FA_HDX\AI-Vision IO Inspector\Codes\AI-Vision IO Inspector\RuntimeData\Models\VLAD\Ex_Weight
```

단, 실제 앱은 소스 루트가 아니라 실행 파일 폴더 기준으로 설정을 읽습니다. 따라서 배포/실행 폴더가 `C:\LinkGenesis\AI-Vision IO Inspector\Run_2608010b`이면 위 실행 로그처럼 `Run_2608010b\RuntimeData\Models\VLAD\Ex_Weight\`가 DLL에 전달됩니다.

근거 파일:

- `Codes\AI-Vision IO Inspector\AI.Vision.IOInspector.Vision\LegacyVlad\VladVisionSettings.cs`
- `Codes\AI-Vision IO Inspector\AI.Vision.IOInspector.Vision\LegacyVlad\VladCamModeRuntime.cs`
- `Codes\AI-Vision IO Inspector\AI.Vision.IOInspector.Vision\LegacyVlad\VladSdkSession.cs`
- `Codes\AI-Vision IO Inspector\AI.Vision.IOInspector.Vision\LegacyVlad\VLAD_Ops_Ai.cs`
- `Codes\AI-Vision IO Inspector\AI.Vision.IOInspector.App\bin\x64\Debug\net472\DB\Logs\20260810\vlad-startup-20260810-114611.log`

### 5.2. MAT JSON 요청/결과 처리

현재 코드 확인 결과:

- Native 선언은 `VLAD_HD_Inference_Mat(IntPtr fullImageVladId, IntPtr rawData, IntPtr requestJsonUtf8)` 3개 인자입니다.
- C# wrapper는 요청 JSON을 8192 byte UTF-8 고정 버퍼에 넣고, 호출 후 같은 버퍼를 문자열로 읽습니다.
- `BuildInspectionContextJsonV11()`은 요청 단계에서 결과 자리인 `viewJudge`, `score`, `dimensions`, `measurements`를 미리 포함합니다.
- 결과 파서는 `viewJudge`, `score`, `scoreThreshold`, `dimensions.width/depth/height`, `measurements[].indexNo/measuredValue`를 읽습니다.
- 실패 JSON을 빈 검출로 처리하지 않고 즉시 검사 실패로 반환하므로 무음 PASS 위험은 줄어든 상태입니다.

요청 JSON 구조는 현재 다음 형태입니다.

```json
{
  "partNo": "품번",
  "viewName": 6,
  "viewJudge": 0,
  "score": 0.00,
  "scoreThreshold": 95.00,
  "dimensions": {
    "width": 0.00,
    "depth": 0.00,
    "height": 0.00
  },
  "measurementPoints": [
    {
      "indexNo": 1,
      "nominalValue": 0,
      "toleranceMin": 0,
      "toleranceMax": 0,
      "x1": 0,
      "y1": 0,
      "x2": 0,
      "y2": 0
    }
  ],
  "measurements": []
}
```

최신 반영 판단:

- 기존의 별도 `Result` 조회 방식은 현재 실행 경로에서 사용하지 않습니다.
- `README`, 일부 주석, 일부 오류 메시지에는 과거 `VLAD_Search_Data`, `VLAD_Custom_InferenceData` 표현이 남아 있어 문서/메시지 정리 대상입니다. 실행 경로 문제는 아니며 혼동 방지용 정리 항목입니다.

근거 파일:

- `Codes\AI-Vision IO Inspector\AI.Vision.IOInspector.Vision\LegacyVlad\VladNativeMethods.cs`
- `Codes\AI-Vision IO Inspector\AI.Vision.IOInspector.Vision\LegacyVlad\VLAD_Ops_Ai.cs`
- `Codes\AI-Vision IO Inspector\AI.Vision.IOInspector.Vision\Engines\VladVisionInferenceEngine.cs`
- `Codes\AI-Vision IO Inspector\AI.Vision.IOInspector.Vision\LegacyVlad\VladInferenceResultParser.cs`

## 6. OCR 미등록 품번 등록 전환

검사 화면의 OCR 검색에서 품번이 DB에 없으면 다음 흐름으로 동작합니다.

1. `TryApplyOcrPartNoToSearch()`가 OCR 품번으로 DB 기준정보를 조회합니다.
2. 품번이 없으면 `ShowOcrUnregisteredConfirmation()`으로 등록 진행 여부를 묻습니다.
3. 사용자가 등록 진행을 선택하면 `PrepareRegistrationForMissingPartCode()`를 호출합니다.
4. 이 함수는 `RegistrationPartNo`에 OCR 품번을 넣고, 품명/분류/측정부/이미지 입력 상태를 신규 등록용으로 초기화합니다.
5. `SelectedTabIndex=2`, `SelectedRegistrationSubTabIndex=0`으로 부품 등록 탭의 단일품목 등록 화면으로 이동합니다.

사용자가 2026-08-10에 실제 동작 확인을 완료한 항목입니다.

근거 파일:

- `Codes\AI-Vision IO Inspector\AI.Vision.IOInspector.App\ViewModels\MainWindowViewModel.cs`
- `Codes\AI-Vision IO Inspector\AI.Vision.IOInspector.App\Services\WpfMessageDialogService.cs`
- `Codes\AI-Vision IO Inspector\AI.Vision.IOInspector.App\OcrUnregisteredPromptWindow.xaml`

## 7. RTSP/카메라 처리 상태

현재 설정상 6채널은 모두 RTSP 방식이며, 위치는 `Top`, `Front`, `Back`, `Left`, `Right`, `Thickness`입니다.

확인된 구현:

- 검사 UI의 라이브 화면은 `RtspVideoHost`가 LibVLC 기반으로 직접 렌더링합니다.
- 검사 캡처는 VLAD RTSP callback이 캐시한 최신 프레임을 파일로 저장하는 경로가 있습니다.
- `VLAD_Ops_RTSP.TryCloneLatestFrames()`는 요청한 채널 중 확보된 채널을 반환하고, 실패 채널은 채널별 메시지로 남깁니다.
- 오래된 프레임은 `maximumFrameAgeMilliseconds` 기준으로 차단합니다.
- `VladVisionInferenceEngine.GetValidCapturedImages()`는 실제 파일이 존재하는 이미지만 VLAD 입력으로 사용합니다.

주의할 점:

- 코드상으로는 일부 채널 실패 시 확보된 채널만 반환할 수 있는 구조가 있습니다.
- 단, 최종 정책은 아직 현장 검증 필요입니다. 실패 채널을 단순 skip할지, 정상 채널 검사는 계속하되 최종 결과를 `ERROR`로 남길지 확정해야 합니다.
- 이전 snapshot 파일이 남아 있는 경우 `GetValidCapturedImages()`가 파일 존재만 보고 입력으로 사용할 수 있으므로, 실패 채널에서 stale 이미지가 재사용되지 않는지 실제 검사 로그로 확인해야 합니다.

잔여 검증:

- `cam1.txt`~`cam6.txt` VLC/ffmpeg 단독 접속 로그 확인
- 고장 카메라 포함 시 정상 카메라만으로 검사 진행 여부 확인
- 실패 채널의 UI/이력/EventLog/최종 판정 정책 확인

근거 파일:

- `Codes\AI-Vision IO Inspector\AI.Vision.IOInspector.App\Controls\RtspVideoHost.cs`
- `Codes\AI-Vision IO Inspector\AI.Vision.IOInspector.Infrastructure\Services\Camera\CameraConfigurationStore.cs`
- `Codes\AI-Vision IO Inspector\AI.Vision.IOInspector.Vision\LegacyVlad\VLAD_Ops_RTSP.cs`
- `Codes\AI-Vision IO Inspector\AI.Vision.IOInspector.Vision\Engines\VladVisionInferenceEngine.cs`

## 8. Thickness 측정부/coordinate 이미지 상태

현재 기준:

- 등록 화면은 측정부를 최대 5개까지 `MeasurementRegion.IndexNo` 기준으로 관리합니다.
- coordinate 이미지는 `WpfReferenceCoordinateImageService`가 Thickness 원본 이미지 위에 측정부 선을 그려 생성합니다.
- 파일명은 `{품번}_coordinate.png`가 최신 기준이고, 과거 호환용으로 `coordinate.png`도 읽습니다.
- 검사 시 측정부가 있는 품목이면 Thickness 기준 이미지 경로가 coordinate 이미지로 대체됩니다.
- VLAD 결과의 `measurements[].indexNo/measuredValue`는 parser에서 index 순서로 정렬되고, 측정값은 `MeasurementRegion.Id`에 연결되어 UI/이력 비교에 사용됩니다.

잔여 검증:

- 실제 측정부 등록 품목에서 검사 UI의 Thickness 표시값이 DB의 측정부 IndexNo와 같은 순서인지 확인해야 합니다.
- 검사 UI에서 보여주는 등록 기준 이미지가 일반 Thickness 원본이 아니라 coordinate 이미지인지 확인해야 합니다.
- 측정부 1~5개 가변 개수에서 UI, 이력, CSV, 최종 판정이 같은 값을 가리키는지 확인해야 합니다.

근거 파일:

- `Codes\AI-Vision IO Inspector\AI.Vision.IOInspector.App\Services\WpfReferenceCoordinateImageService.cs`
- `Codes\AI-Vision IO Inspector\AI.Vision.IOInspector.App\ViewModels\MainWindowViewModel.cs`
- `Codes\AI-Vision IO Inspector\AI.Vision.IOInspector.Application\Services\MeasurementService.cs`
- `Codes\AI-Vision IO Inspector\AI.Vision.IOInspector.Vision\Services\VladMeasurementMapper.cs`

## 9. 데이터 저장과 이력

현재 저장 구조:

- 기준정보는 SQLite `PartList_*` 계열 테이블에서 관리합니다.
- 검사 이력은 SQLite `History_*` 계열 테이블에서 관리합니다.
- 기준 이미지는 `Config.json`의 `IMAGE_PATH` 기준으로 `분류코드\품번` 하위에 저장합니다.
- 검사 결과 이미지는 `OUTPUT_PATH` 기준 이력 폴더에 저장합니다.
- OCR 등록용 임시 파일은 `OCR_PATH` 아래에 보관하고, DB 저장/취소 흐름에서 정리합니다.
- 보존기간과 디스크 여유공간 기준 삭제 정책은 `InspectionDataRetentionService`에서 처리합니다.

근거 파일:

- `Codes\AI-Vision IO Inspector\AI.Vision.IOInspector.Infrastructure\Repositories\SqliteDatabase.cs`
- `Codes\AI-Vision IO Inspector\AI.Vision.IOInspector.Infrastructure\Repositories\SqlitePartRepository.cs`
- `Codes\AI-Vision IO Inspector\AI.Vision.IOInspector.Infrastructure\Repositories\SqliteInspectionRepository.cs`
- `Codes\AI-Vision IO Inspector\AI.Vision.IOInspector.Infrastructure\Services\LocalReferenceImageFileService.cs`
- `Codes\AI-Vision IO Inspector\AI.Vision.IOInspector.Infrastructure\Services\InspectionDataRetentionService.cs`

## 10. 완료로 반영한 항목

| 항목 | 상태 |
| --- | --- |
| MAT API 결과 수신 방식 | 별도 Result 조회가 아니라 `VLAD_HD_Inference_Mat` in-place JSON 방식으로 반영 확인 |
| MAT 요청 JSON | `viewJudge`, `score`, `dimensions`, `measurements` 결과 자리 포함 확인 |
| OCR 미등록 품번 등록 전환 | 팝업 후 단일품목 등록 UI 이동 및 OCR 품번 자동 입력 확인 |
| 프로그램 아이콘 시안 | PNG/ICO 생성 완료, 프로젝트 리소스 적용은 선택 항목 |

## 11. 남은 확인 항목

현재 기준 잔여 항목은 `Docs\03-development\30-open-items\open-items.md`의 2026-08-10 섹션을 기준으로 관리합니다.

| ID | 항목 | 다음 확인 |
| --- | --- | --- |
| O-032 | 6개 RTSP URL 단독 검증 로그 | `cam1.txt`~`cam6.txt` 연결 성공/실패, 코덱, 해상도, FPS, timeout 정리 |
| O-033 | 고장 카메라 포함 검사 정책 | 정상 채널 처리와 실패 채널 ERROR 반영, stale snapshot 미사용 확인 |
| O-034 | Thickness 값/coordinate 기준 이미지 | 실제 측정부 등록 품목에서 UI 값, 기준 이미지, 이력 매핑 확인 |
| O-035 | 과거 Result/Data 용어 정리 | README/주석/오류 메시지 중 현재 MAT JSON과 다른 표현 정리 여부 결정 |
| O-036 | 프로그램 아이콘 적용 | 생성된 ICO를 WPF 실행 파일/창 아이콘으로 연결할지 결정 |

## 12. 권장 확인 순서

1. `cam1.txt`~`cam6.txt`를 채널별로 먼저 판독해 RTSP 단독 접속 상태를 확정합니다.
2. 일부 카메라 실패 상태를 의도적으로 만들고 검사 버튼을 눌러 정상 채널 처리, 실패 채널 로그, 최종 판정, stale 이미지 재사용 여부를 확인합니다.
3. 측정부가 1개 이상 등록된 실제 품목으로 Thickness 검사 UI, coordinate 기준 이미지, 측정값 IndexNo 매핑을 확인합니다.
4. 위 3개가 끝난 뒤 문서/메시지의 과거 `Result/Data` 용어 정리와 아이콘 적용 여부를 결정합니다.
