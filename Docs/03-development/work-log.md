# 진행 로그

## 2026-05-29

- 프로젝트 초기 문서 구조와 VS Code 설정을 생성했습니다.
- 원본 자료 정리, 요구사항 정리, 설계, 작업 추적을 분리했습니다.
- `Downloads`에 있던 `요구사항 명세서.docx`와 `HD현대사이트솔루션_AI비전_입고검사_업무파악_정리.docx`를 `00-inbox/documents/`에 원본 보관했습니다.
- 두 docx 파일의 본문, 표, 이미지를 `00-inbox/extracted/`에 추출했습니다.
- 요구사항, 프로그램 구조, 데이터 모델, 화면 구성, 개발 로드맵, 신규 요청 처리 절차를 프로젝트 기준으로 갱신했습니다.


- `Tests\AI-Vision IO Inspector`에 Visual Studio 2022 기반 .NET 9 WPF MVVM 개발용 솔루션을 생성하고, Domain/Application/Infrastructure/App 구조로 1차 기능을 구현했습니다.
- `dotnet build AI.Vision.IOInspector.sln --configuration Debug` 결과 경고 0개, 오류 0개로 빌드를 확인했습니다.
- 부품 등록의 삭제 동작을 즉시 삭제에서 삭제 예정 후 `DB 저장` 시 반영되는 흐름으로 수정하고, 빌드 경고 0개/오류 0개를 확인했습니다.
- 측정부 기준 데이터를 세트 단위(측정부 길이/너비/높이/두께, 추가 세트는 측정부2...)로 정리하고, 부품 등록 UI의 추가/삭제 버튼을 세트 단위 표현으로 변경했습니다. 빌드 경고 0개/오류 0개를 확인했습니다.
- 부품 등록 상단 검색 영역의 분류설명 입력 폭을 분류코드와 같은 크기로 줄이고, 검색 추천 키워드가 초록색으로 표시되도록 부품 등록 화면에도 추천 표시 영역을 추가했습니다. 빌드 경고 0개/오류 0개를 확인했습니다.
- 화면 표기를 `Part No/Part Name`에서 `품번/품명`으로 변경하고, 검사 이력에 분류코드/분류설명/구분/측정값/기준값/불일치 항목 컬럼과 CSV 저장 버튼을 추가했습니다. 실행 중인 앱 프로세스 종료 후 빌드 경고 0개/오류 0개를 확인했습니다.
- 검사 이력 화면을 단일 목록과 키워드 검색 조건으로 정리하고, 검사 이력 JSON(`RuntimeData/InspectionHistory`)과 검사 로그(`RuntimeData/InspectionLogs`)를 보관기간/디스크 여유공간 기준으로 삭제 관리하도록 수정했습니다. 빌드 경고 0개/오류 0개를 확인했습니다.
- 검사 이력 CSV 저장 시 측정부별 구조가 다른 경우를 고려해 `측정부_길이_측정값`, `측정부_길이_기준값`, `측정부_길이_판정` 형태의 동적 컬럼으로 내보내도록 변경했습니다. 빌드 경고 0개/오류 0개를 확인했습니다.
- 검사 이력의 시간/품번/품명/분류코드/분류설명/구분/NG결과 검색 조건으로 항목을 추리고, 현재 표시된 이력만 CSV로 저장하도록 변경했습니다. 빌드 경고 0개/오류 0개를 확인했습니다.
- 부품 등록 UI에서 측정부 세트 영역 폭을 축소하고 기준 이미지 영역을 확장했습니다. 기준 이미지 목록은 스크롤 가능하게 유지하며, 선택한 이미지가 하단 미리보기 영역에 표시되도록 수정했습니다. 빌드 경고 0개/오류 0개를 확인했습니다.
- DB 조회 화면에서 선택 부품 측정부 상세 폭을 축소하고 등록 기준 이미지를 우측으로 배치했으며, 하단에 선택 이미지 미리보기 영역을 추가했습니다. 부품 등록 화면은 단일품목/다중품목 탭으로 나누고, 기준 이미지 위치 선택 및 다중품목 CSV 불러오기/현재 항목 CSV 내보내기 기능을 추가했습니다. 빌드 경고 0개/오류 0개를 확인했습니다.
- 다중품목 CSV 내보내기를 단일 항목이 아닌 전체 DB 부품 기준정보 대상으로 변경했습니다. 전체 부품 중 가장 많은 측정부 세트 수를 기준으로 CSV 컬럼을 생성하고, 없는 측정부 값은 `-`로 저장합니다. 기본 출력 폴더는 실행 중인 앱/Visual Studio 잠금으로 실패했으나 임시 출력 폴더 빌드에서 경고 0개/오류 0개를 확인했습니다.
- 단일품목 등록의 측정부 세트 입력을 길이/너비/높이/두께별 기준값, 허용값, 단위로 확장했습니다. 다중품목 CSV 입출력도 `측정부_길이`, `측정부_길이_허용`, `측정부_길이_단위` 형식으로 변경하고, 다중품목 결과 Grid에는 첫 번째 측정부의 값/허용/단위를 컬럼으로 표시하도록 수정했습니다.
- 단일품목 등록 화면에서 측정부세트 우측 빈 공간을 줄이기 위해 기준 이미지 영역 폭을 확대하고, 기준 이미지 Grid의 `File` 컬럼을 줄여 `Path` 컬럼 가시성을 높였습니다. 기준 이미지 목록의 가로/세로 스크롤은 필요 시 자동 생성되도록 유지했습니다.
- 부품 등록의 DB 조회/관리 Grid에서 `구분` 컬럼 폭을 축소해 분류설명 영역이 더 넓게 보이도록 조정했습니다.
- UI/저장명/CSV 헤더에서 첫 번째 측정부는 `측정부`로 표시하고, 두 번째부터 `측정부2`, `측정부3` 순번을 붙이도록 변경했습니다.
- 기준 이미지 미리보기 로딩 방식을 파일 잠금이 남지 않는 메모리 로딩으로 변경하고, 파일 삭제 실패 시 예외 대신 원인 메시지를 표시하도록 보완했습니다.
- 기준 이미지 삭제 후에는 다른 이미지를 자동 선택하지 않고 미리보기 영역을 빈 상태로 남기도록 조정했습니다.
- 기준 이미지 추가 시 기존 이미지 파일명을 덮어쓰지 않도록 다음 사용 가능한 번호를 찾아 저장하고, 파일 접근 오류는 화면 메시지로 표시하도록 보완했습니다.
- 기준 이미지 미리보기 디코딩 중 `ArgumentNullException` 등 이미지 해석 오류가 발생해도 화면 예외로 전파되지 않고 빈 미리보기로 처리되도록 보완했습니다.
- 기준 이미지 추가 전에 WPF 디코더로 원본 파일을 검증하고, 손상/미지원 이미지이면 목록에 추가하지 않고 메시지를 표시하도록 보완했습니다.
- 기준 이미지 저장 규칙을 위치별 유니크 구조로 변경했습니다. 현재 이미지는 `DB/Image/{분류코드}/{품번}/{품번}_{ViewType}` 형식으로 저장하고, 같은 위치 재등록 시 기존 파일을 `OldVer_현재시간` 파일명으로 백업한 뒤 교체합니다. 부품 삭제 확정 시 해당 부품 이미지 폴더와 백업 파일을 함께 삭제합니다.
- 부품등록과 DB 조회/확인의 기준 이미지 목록에서 `File` 컬럼을 제거하고, 이미지 표시 순서를 `Top`, `Front`, `Back`, `Left`, `Right`, `Thickness` 기준으로 고정했습니다.
- 메인 검사 UI를 6개 카메라 화면 중심으로 조정했습니다. 기준 이미지는 흐릿한 참조 이미지로 유지하고, 실시간 카메라 이미지는 별도 경로로 표시하며, PASS/FAIL 판정은 카메라 화면 상단의 작은 배지로 표시하도록 변경했습니다. 좌측 탭 이동 버튼은 제거하고 Guide List 영역을 확장했으며, 품번/품명/분류코드/분류설명/구분 정보는 더 또렷한 Key/Value 표 형태로 표시합니다.
- 등록 기준 이미지 목록의 긴 절대경로 표시를 `REFERENCE:\\{분류코드}\{품번}` 형식의 관리경로로 축약했습니다. 실제 이미지 미리보기/삭제/교체는 기존 `FilePath`를 그대로 사용합니다.
- 부품 등록 신규입력의 길이/너비/높이/두께 허용 기본값을 `0.5`에서 `0`으로 변경했습니다.
- SQLite를 기준정보/이력 저장소로 선택하고 `DB/DataBase.db`를 생성했습니다. `export_Test.csv` 11,407개 품번을 1회성으로 `PartList_*` 테이블에 적재했고, 검사 이력은 `History_*` 테이블로 분리했습니다. 기존 이력 보관기간/디스크 여유공간 삭제 정책도 SQLite 이력 테이블에 적용했습니다.
- `export_Test.csv`는 테스트용 1회성 원본이므로 앱 실행 시 참조하지 않도록 SQLite 초기화 코드에서 자동 적재 로직을 제거했습니다. 앞으로 `DataBase.db`가 없으면 빈 스키마만 생성됩니다.
- 누적형 md 파일을 업데이트할 때는 변경 당일 날짜 섹션을 먼저 만들고 그 아래에 변경 내용을 기록하는 원칙을 적용했습니다.
- 확정 카메라 사양(DC-T3145G 2대, DC-T3145R 4대, DR-2508P-A NVR 1대)을 반영하고, `VLAD Source`의 RTSP/IMV SDK/AI/측정 후처리 코드를 분석해 카메라·AI 연동 권장 구조를 문서화했습니다.
- 상위 프로젝트 루트의 `DB` 폴더와 빌드 출력 폴더의 `DB/Image`, `RuntimeData`를 `Tests/AI-Vision IO Inspector` 솔루션 폴더 아래로 이동했습니다. 앱도 솔루션 폴더 기준 `DB`와 `RuntimeData`를 사용하도록 경로 해석을 수정했습니다.
- 다중품목 CSV 불러오기에서 UTF-8 BOM이 포함된 내보내기 파일을 다시 읽을 때 `품번` 헤더를 못 찾는 문제를 보완했습니다. CSV 행별 예외 처리를 추가해 실패 행이 전체 불러오기를 중단시키지 않도록 했습니다.
- 부품등록 기준 이미지 영역에 현재 메인 화면의 6개 실시간 이미지 파일을 기준 이미지로 일괄 저장하는 버튼을 추가했습니다. 실제 카메라 연동 전 시뮬레이션 경로는 파일이 아니므로 저장 대상에서 제외합니다.
- 카메라 연결은 Direct SDK를 측정용 1순위, NVR/RTSP를 녹화/미리보기 보조 경로로 두고, 카메라와 Top/Front/Back/Left/Right/Thickness 매핑은 Option UI와 설정 테이블로 관리하는 방향을 문서화했습니다.
- DB 조회/부품 등록 검색 성능을 개선했습니다. 품목 목록은 앱 시작 및 저장/삭제/CSV 반영 후에만 SQLite에서 다시 읽고, 키 입력 검색은 메모리 캐시를 필터링하도록 변경했습니다. 초록 추천어는 최대 10개로 제한하고 250ms 입력 지연 처리, 검색 결과 컬렉션 일괄 교체, DataGrid 가상화를 적용했습니다. 빌드 경고 0개/오류 0개를 확인했습니다.
- 부품 기준정보를 `PartDataStore`로 분리했습니다. 앱 시작 시 SQLite에서 전체 품목을 로드하고, 검색/추천/CSV 내보내기는 DataStore 캐시를 사용합니다. 생성/수정/삭제/CSV 불러오기는 DB 저장 성공 후 DataStore 캐시를 함께 갱신하며, ViewModel은 화면 상태 갱신만 담당하도록 역할을 분리했습니다. 빌드 경고 0개/오류 0개를 확인했습니다.
- `VLAD Source`의 RTSP/IMV SDK 샘플을 현재 구조에 맞춰 카메라 서비스 경계로 반영했습니다. `CameraChannelConfig`, `CameraConfigurationStore`, `ConfiguredCameraService`, `ICameraFrameSource` 구조를 추가하고, 기본 6채널 설정은 `RuntimeData/Camera/camera-config.json`으로 관리하도록 했습니다. 실제 장비 연결 전에도 `SimulatedCameraFrameSource`가 BMP 파일을 생성해 검사 화면/이력/기준이미지 저장 흐름을 검증할 수 있습니다. 실행 중인 앱 프로세스가 DLL을 잠그고 있어 종료 후 재빌드했으며, 빌드 경고 0개/오류 0개를 확인했습니다.

## 2026-05-30

- VLAD/IMV 관련 DLL의 x64/x86, .NET Framework 래퍼 여부, 네이티브 의존성을 `dumpbin` 기준으로 검증했습니다. `VLAD_SDK.dll`은 x64 네이티브 DLL이며, VLC/ONNX/OpenCV/TensorFlow/동글 라이선스/VC++ 런타임 의존성이 있습니다. `MVSDK_Net.dll`, `CLIDelegate.dll`은 .NET Framework 4.0 계열이므로 .NET 9 앱에 직접 참조하지 않고 카메라 어댑터 경계 뒤에서 격리하는 방향으로 정리했습니다.
- 장비 PC에 개발 도구를 설치하지 않고 실행할 수 있도록 `NativeDependencyLoader`를 추가했습니다. 앱 시작 시 `Native\VLAD`, `Native\VLAD\plugins`, `Native\IMV\x64`, `Native\AI\x64`를 프로세스 DLL 검색 경로에 등록하고, VLC 플러그인 경로도 `VLC_PLUGIN_PATH`로 설정합니다.
- `Tests\AI-Vision IO Inspector\Native\README.md`, `Docs\03-development\vision\native-deployment.md`, `scripts\publish-win-x64.ps1`을 추가해 네이티브 DLL 배포 위치와 self-contained win-x64 publish 기준을 문서화했습니다. 대용량 벤더 DLL은 GitHub 일반 커밋 대상에서 제외하도록 `.gitignore`에 Native 바이너리 제외 규칙을 추가했습니다.
- 실행 중인 `AI.Vision.IOInspector.App` 프로세스가 출력 DLL을 잠그고 있어 종료 후 재빌드했습니다. WPF 증분 빌드 산출물(`App.g.cs`, `MainWindow.g.cs`)이 일시적으로 누락되어 `dotnet build -t:Rebuild`로 생성 파일을 다시 만들었고, 최종 빌드는 경고 0개/오류 0개로 통과했습니다. `scripts\publish-win-x64.ps1`로 `publish\win-x64-test` self-contained 배포 폴더 생성을 확인했습니다. 생성된 앱 EXE는 x64이며, .NET 런타임 DLL과 `DB\DataBase.db`, `Native` 폴더가 함께 배포됩니다.
- AI/카메라 담당자 전용 구현 영역으로 `AI.Vision.IOInspector.Vision` 프로젝트를 솔루션에 추가했습니다. App은 `VisionRuntimeFactory`를 통해 `ICameraService`, `IAiInferenceService` 구현체를 받도록 연결했고, 현재 검사 시뮬레이션은 `SimulatedVisionInferenceEngine`으로 이전했습니다. AI 엔진이 측정값 단위와 raw pixel 값을 반환할 수 있도록 `AiInferenceResult`를 확장하고, `MeasurementService`는 `mm`, `cm`, `m` 단위 변환 후 기준값과 비교하도록 보완했습니다. `dotnet build -t:Rebuild` 결과 경고 0개/오류 0개를 확인했고, `dotnet publish -c Release -r win-x64 --self-contained true`도 새 Vision 프로젝트를 포함해 정상 완료했습니다.
- Vision 프로젝트에 실행 뼈대를 보강했습니다. `VisionInferenceWorker` 전용 background thread로 AI 추론을 분리하고, `VisionCameraCoordinator`를 카메라 중심 조율 클래스로 추가했습니다. 기존 VLAD/IMV 담당자가 대응하기 쉽도록 `LegacyVlad`와 `ImvCamera` 폴더에 `VLAD_Registration`, `VLAD_Inference_Mat`, `OpenDevice`, `StartGrabbing`, `GetFrame`, `ReleaseFrame`, `StopGrabbing` 흐름과 대응되는 Adapter 뼈대를 추가했습니다. 실제 SDK 호출은 아직 구현하지 않았고, 누락 방지를 위해 `vision-project-boundary.md`에 대응표와 현재 미구현 범위를 기록했습니다.
- 기존 함수명을 그대로 검색할 수 있도록 VLAD/IMV facade를 추가했습니다. 이후 2026-06-19 정리에서 미사용 VLAD facade 계층은 `VLAD_Ops_Ai` 공식 진입점으로 단일화했습니다. 또한 `vlad-imv-conversion-guide.md`를 작성해 기존 `VLAD_Ops_Ai.cs`, `Camera_Control.cs`, IMV 샘플 코드의 함수가 현재 Vision 프로젝트의 어떤 클래스/메소드로 이동해야 하는지 변환 순서와 미구현 항목까지 정리했습니다.
- 비전 영역을 한국 담당자가 바로 읽을 수 있도록 `AI.Vision.IOInspector.Vision` 프로젝트의 XML/인라인 주석과 README 설명을 한국어 중심으로 정리했습니다. 기존 VLAD/IMV 함수명, SDK명, 클래스명은 검색성과 담당자 대응을 위해 그대로 유지했습니다.
- Vision 담당자가 여러 상위 md 파일을 모두 열 필요가 없도록 `camera-ai-integration.md`, `vision-project-boundary.md`, `vlad-imv-conversion-guide.md`, `native-deployment.md`를 `Docs\03-development\vision` 아래로 이동하고, 읽는 순서를 `Docs\README.md`에 정리했습니다.
- VSLD/VLAD 코드의 카메라별 Thread 구조를 현재 프로젝트에 맞춰 `VisionCameraCaptureWorker`, `VisionCameraCaptureRequest`, `IVisionCameraCaptureExecutor`로 보강했습니다. `VisionCameraCoordinator`는 Top/Front/Back/Left/Right/Thickness Worker를 생성하고 `CaptureAll` 요청을 분배합니다. 실제 SDK가 없으므로 현재 촬영 실행은 기존 `ConfiguredCameraService`를 사용하며, 빌드 경고 0개/오류 0개를 확인했습니다.
- Vision 담당자가 빠뜨리기 쉬운 실제 SDK 연결, RTSP/NVR 정책, 트리거, pixel-to-mm 보정, VLAD 결과 파싱, 이벤트 이미지 보관 정책을 `Docs\03-development\vision\vision-implementation-checklist.md`에 정리했습니다.
- 옵션 탭을 추가해 Top/Front/Back/Left/Right/Thickness 6대 카메라의 연결 방식, 사용 여부, 연결 상태, IP, Serial, User ID, NVR 채널, 해상도, FPS, Trigger, RTSP URL, 최근 프레임, 메시지, 확인 시각을 확인할 수 있게 했습니다. `CameraChannelStatus` 모델도 설정 정보까지 표시하도록 확장했습니다.
- `task-board.md`, `questions.md`, `vision-implementation-checklist.md`를 2026-05-30 기준으로 재정리했습니다. 완료된 항목과 부분완료/대기 항목을 분리했고, 실제 카메라 SDK 연결, 연속 영상 미리보기, pixel-to-mm 보정, VLAD/AI 실제 결과 파싱을 다음 핵심 미완료 항목으로 표시했습니다.
- NVR 사용 방향을 측정 원본이 아닌 녹화/모니터링 보조로 확정했습니다. 이에 따라 측정 원본은 Direct SDK 우선 구조로 유지하고, NVR 정책 미확정 항목은 해결 처리했습니다.
- 기존 VLAD/VSLD 코드와 현재 Vision 프로젝트의 차이가 커져 담당자 대응이 어려운 문제를 줄이기 위해 기존 함수명 호환 계층을 추가했습니다. `VLAD_Ops_Ai_Env_Start`, `VLAD_Ops_imvCam_Thread`, `VLAD_Ops_imvCam_IMV_Open`, `Camera_Control.Open_Cam`, `Close_Cam`, `Is_Open`, `Cam_Proc` 이름을 현재 Vision 프로젝트 안에서 검색 가능하게 했고, 1~7 항목별로 기존 코드에서 얻을 수 있는 근거와 신규 설계가 필요한 부분을 `legacy-traceability-1to7.md`에 정리했습니다.

## 2026-05-31

- 각 md 파일의 `미구현`, `미완료`, `대기`, `보류`, `확인필요` 항목을 점검하고, 모호한 상태명을 `미구현-내부작업`, `미구현-외부정보필요`, `부분완료-검증필요`, `보류-범위조정`처럼 원인과 다음 행동이 드러나는 형태로 정리했습니다.
- 날짜별 미구현/미비 항목을 계속 체크하고 클리어하기 위한 중심 문서로 `Docs/03-development/open-items.md`를 추가했습니다. 실제 카메라 SDK 연결, 연속 영상 미리보기, 트리거, pixel-to-mm 보정, 렌즈 왜곡 보정, VLAD 실제 추론, AI 결과 파싱, 이미지 보존 정책, 바코드 입력, 장비 PC 배포, 통계 검증, Excel 직접 업로드를 `O-001`~`O-012`로 추적합니다.
- `task-board.md`, `questions.md`, `acceptance-criteria.md`, `integration-spec.md`, Vision 체크리스트/변환 가이드를 `open-items.md`와 연결했습니다. 앞으로 완료 처리는 작업 보드와 open-items의 ID를 함께 갱신하는 방식으로 진행합니다.
- 연속 영상 미리보기의 첫 내부 작업으로 `IVisionCameraReceiveExecutor`, `VisionCameraReceiveWorker`를 추가했습니다. 실제 SDK 연결 전까지는 파일을 계속 저장하는 방식으로 미리보기를 만들지 않고, 최신 프레임 1장만 유지하는 구조로 확장합니다.

## 2026-06-01

- 옵션 화면에서 카메라 채널별 IP, Port, User, Password, StreamPath, RTSP URL을 직접 수정하고 저장할 수 있도록 변경했습니다. `상태 새로고침`과 `선택 연결테스트`는 더 이상 시뮬레이션 성공으로 표시하지 않고, RTSP 포트와 RTSP 응답을 실제로 확인합니다.
- IDIS DC-T3145G/NVR 연결 검증을 위해 RTSP URL 생성 규칙을 추가했습니다. 명시 RTSP URL이 없으면 `rtsp://User:Password@IP:Port/StreamPath` 형태로 생성하며, 기본 Port는 `554`, 기본 StreamPath는 `trackID=1`입니다.
- PC에 설치된 `ffmpeg.exe` 또는 배포 폴더 `Native\FFmpeg\ffmpeg.exe`를 찾아 RTSP 스트림에서 현재 프레임 1장을 캡처하는 `RtspCameraFrameSource`를 추가했습니다. 이 경로는 실제 카메라 영상 수신 검증용이며, 연속 미리보기 UI 연결은 후속 작업으로 남겼습니다.
- PC와 NVR을 `Network Client` 포트로 연결한 뒤 유선 IP `192.168.1.210`, NVR 웹 접속 IP `192.168.1.1`을 확인했습니다. 프로그램 런타임 카메라 설정은 `NvrRtsp`, `192.168.1.1:554`, `trackID=1~6`으로 반영했지만, NVR의 RTSP 포트 `554`가 시간 초과되어 실제 프레임 수신은 아직 실패했습니다. 다음 작업은 NVR 설정에서 RTSP 사용 및 RTSP/HTTP 사용자 권한을 활성화한 뒤 재검증하는 것입니다.
- Top 카메라 연결 점검 중 `192.168.1.1` 웹 접속은 가능하지만 RTSP 후보 포트 `554`, `8554` 등이 모두 시간 초과되는 것을 확인했습니다. 메인 검사 화면에 `카메라 화면 갱신` 버튼을 추가해 검사/AI 흐름과 별도로 사용 설정된 카메라의 현재 프레임을 받아 표시할 수 있게 했고, 실패 시 각 화면 슬롯과 Event 로그에 실패 원인을 표시하도록 보완했습니다. 또한 옵션 화면에서 생성된 RTSP URL이 고정 저장되어 비밀번호 변경을 막지 않도록 저장 시 `RtspUrl`은 비우고 IP/Port/User/Password/StreamPath에서 매번 다시 생성하도록 수정했습니다. `dotnet build` 결과 경고 0개, 오류 0개를 확인했습니다.

## 2026-06-02

- 옵션 화면의 카메라 연결 상태 기준을 RTSP 포트/응답 확인에서 실제 영상 프레임 수신 확인으로 변경했습니다. 이제 `상태 새로고침`과 `선택 연결테스트`는 사용 설정된 카메라에서 프레임 1장을 캡처해 파일이 생성된 경우에만 `연결됨`으로 표시하고, 인증 실패/채널 미등록/RTSP URL 오류/ffmpeg 캡처 실패는 `미연결`으로 표시합니다. 앱 시작/검사 후 상태 갱신은 기존 상태 캐시만 읽어 UI 정지를 줄이고, 사용자가 명시적으로 상태 확인을 누를 때만 실제 프레임 검증을 수행합니다. 실행 중인 앱 프로세스가 기본 출력 DLL을 잠그고 있어 별도 임시 출력 폴더로 App 프로젝트를 빌드했으며, 경고 0개/오류 0개로 확인했습니다.
## 2026-06-05

- VLAD Source의 실제 C# 샘플 본문은 대부분 빠져 있고 `Camera\C#\IMV`, `IMVFG`에는 폴더/obj 캐시만 남아 있음을 확인했다. 대신 VLAD 실행 산출물에 `OpenCvSharp.dll`, `OpenCvSharpExtern.dll`, `opencv_world453.dll`, `opencv_ffmpeg400_64.dll`이 있어 RTSP 프레임 수신 런타임으로 활용했다.
- 현재 프로그램의 RTSP 프레임 캡처는 OpenCvSharp 런타임을 우선 사용하고, 없을 때 `ffmpeg.exe`를 찾는 방식으로 보강했다. 런타임 DLL은 Git 제외 폴더인 `Tests\AI-Vision IO Inspector\RuntimeData\Native\OpenCvSharp\x64`에 배치했다.
- OpenCvSharp DLL 로딩은 성공했고, NVR `192.168.1.230:554` RTSP 포트도 접근 가능했다. 실제 프레임 열기는 `401 Unauthorized`로 실패했으므로 남은 문제는 코드보다 NVR의 RTSP/HTTP 사용자 권한 또는 RTSP 전용 비밀번호 설정이다.
- `dotnet build AI.Vision.IOInspector.Infrastructure.csproj` 결과 경고 0개, 오류 0개를 확인했다. 전체 솔루션 빌드는 실행 중인 `AI.Vision.IOInspector.App (43516)`가 출력 DLL을 잠그고 있어 App 복사 단계에서 실패했다.
- 이후 OpenCvSharp `DisposeUnmanaged` 경로에서 `System.Web.HttpContext` 로드 실패 예외가 발생해 VLAD OpenCvSharp가 .NET Framework 전용 DLL임을 확정했다. OpenCvSharp는 호환성 검사에서 자동 비활성화하고, VLAD LibVLCSharp/LibVLC 스냅샷 경로를 우선 사용하도록 변경했다.
- .NET 9 Probe 실행으로 OpenCvSharp 예외가 더 이상 전파되지 않음을 확인했다. 현재 실패 메시지는 NVR RTSP `401 Unauthorized` 인증 문제로만 정리된다.

## 2026-06-08

- 메일로 받은 `VLAD_SDK.dll`과 `Config.json`을 프로젝트 내부 런타임 위치(`Native\\VLAD`, `CFG`)에 배치했습니다.
- VLAD_Ops `bin\\x64\\Debug`의 DLL과 VLC `plugins` 392개를 복사했고, `VLAD_SDK.dll`은 `LoadLibrary`와 주요 export 함수(`VLAD_Custom_Registration`, `VLAD_Rtsp_Info_Client_Registration`, `VLAD_Inference_Mat`, `VLAD_Registration`) 확인을 통과했습니다.
- RTSP LibVLC 캡처 코드가 `RuntimeData\\Native\\LibVLC` 대신 `Native\\VLAD`를 우선 사용하도록 수정했습니다. 기존 경로는 예비 경로로 유지합니다.
- `dotnet build` 결과 경고 0개, 오류 0개를 확인했습니다. `cudart64_110.dll` 경고는 GPU CUDA 런타임 미설치 경고이므로 실제 AI 추론 전 GPU/CPU 운영 방식을 확정해야 합니다.

- NVR `192.168.1.230:554` RTSP 포트가 열려 있음을 확인했습니다. 웹 포트 `80`은 닫혀 있습니다.
- RTSP `OPTIONS`는 `trackID=1`, `trackID=2`에서 `200 OK`로 응답하지만, `DESCRIBE`는 `Digest` 인증을 요구하며 현재 계정 정보로는 `401 Unauthorized`가 반환됩니다.
- VLC 스냅샷 테스트도 `SETUP RTSP session` 단계에서 실패했습니다. 현재 문제는 네트워크 포트가 아니라 RTSP Digest 계정/권한 또는 NVR 채널 스트림 접근 권한 쪽으로 판단됩니다.
- 옵션 UI의 연결 상태는 실제 프레임 수신 성공 기준으로 표시되도록 RTSP 진단 메시지와 LibVLC 런타임 경로를 정리했습니다.
## 2026-06-09

- Vision 프로젝트에 기존 VLAD_Ops 담당자가 찾기 쉬운 호환 진입점을 보강했습니다. `VLAD_Ops_Ai`, `VLAD_Ops_RTSP`, `VLAD_Ops_imvCam` 파일명과 주요 함수명을 유지하고, 실제 WPF UI 제어는 기존 MVVM 서비스 경계에 남겼습니다.
- `AI.Vision.IOInspector.Vision.csproj`에 `MVSDK_Net`, `OpenCvSharp`, `OpenCvSharp.Blob`, `OpenCvSharp.Extensions`, `OpenCvSharp.UserInterface` 참조를 명시했습니다. `VLAD_SDK.dll`, `VLAD_Ctrl.dll`, `libvlc.dll`, `libvlccore.dll`, `opencv_world453.dll`, `jsoncpp.dll`은 Visual Studio에서 보이도록 `NativeReferences\\VLAD` 링크로 추가했습니다.
- `MVSDK_Net.IMVApi`는 외부 접근 불가 internal 타입임을 빌드 오류로 확인했습니다. 기존 IMV 샘플과 동일하게 `MyCamera.IMV_GetVersion`, `MyCamera.IMV_EnumDevices`, `cam.IMV_GetFrame`, `cam.IMV_ReleaseFrame` 형태를 사용하는 `MVSDK_Net_Compat` 래퍼로 수정했습니다.
- `MVSDK_Net.dll`은 AMD64 DLL이므로 `AI.Vision.IOInspector.App`과 `AI.Vision.IOInspector.Vision` 프로젝트를 `PlatformTarget=x64`로 고정했습니다.
- `dotnet build` 전체 솔루션 검증 결과 경고 0개, 오류 0개를 확인했습니다.
- 현재 프로젝트 Native 폴더에는 `MVSDKmd.dll`이 없습니다. 따라서 `MVSDK_Net.dll` 참조와 빌드는 가능하지만, IMV Direct SDK 카메라 제어를 실제 실행하려면 제조사 SDK 런타임 DLL 세트를 추가해야 합니다.

## 2026-06-11

- 기준 이미지 저장 흐름을 보강했습니다. `현재6개저장`으로 저장한 이미지가 파일 저장에만 머물지 않고 DB/DataStore/검사 슬롯/DB 조회 미리보기까지 즉시 갱신되도록 변경했습니다.
- 검사 시작 전에 현재 사용 설정된 카메라 위치의 기준 이미지 파일 존재 여부를 확인하도록 추가했습니다. 기준 이미지가 없으면 검사 워크플로우로 진입하지 않고 팝업과 Event 로그로 등록을 유도합니다.
- 검사 UI의 기준 이미지는 실시간 영상 아래에 흐릿한 참조 레이어로 보이도록 BlurEffect를 추가했습니다.
- VLAD_Ops의 경로 운영 방식에 맞춰 `CFG/Config.json`의 `IMAGE_PATH`, `OUTPUT_PATH`를 기준 이미지/검사 이력 이미지 루트로 읽는 `RuntimeImagePathSettings`를 추가했습니다. 현재 PC처럼 설정 드라이브가 없으면 프로젝트 내부 `DB/Image`, `DB/History`로 폴백합니다.
- 실행 중인 앱이 기본 출력 DLL을 잠그고 있어 기본 `dotnet build`는 복사 단계에서 실패했지만, 임시 출력 폴더 빌드로 경고 0개/오류 0개를 확인했습니다.
- 등록 기준 이미지의 경로는 보이지만 미리보기가 표시되지 않는 문제를 수정했습니다. LibVLC 스냅샷이 PNG 바이트를 `.jpg` 확장자로 저장하면서 `BitmapImage`가 메타데이터 예외를 내던 것이 원인이었고, `BitmapDecoder` fallback을 추가했습니다. 앞으로 RTSP/NVR 캡처 이력 이미지는 실제 포맷에 맞게 `.png` 확장자로 저장합니다.
- `_settings.ModelPath`가 `E:/Tensor_Projects/Ex/Chip/Ex_Weight`로 잡히는 이유를 점검했습니다. 해당 값은 메일로 받은 기존 VLAD_Ops `CFG/Config.json`의 `MODEL` 값에서 읽힌 것이며, 현재 PC에는 해당 폴더가 없습니다. 깨져 있던 `Config.json`을 정상 JSON으로 정리하고, `AI_VISION_VLAD_MODEL_PATH` 환경변수 또는 상대경로를 통한 모델 경로 override를 명확히 지원하도록 `VladVisionSettings`를 보강했습니다. 모델 경로가 없을 때는 검사 오류 메시지에 변경해야 할 설정 위치를 표시합니다.
- 기존 VLAD_Ops의 `VLAD_Ops_Ai_Env_Start` 흐름에는 C# 쪽에서 `Directory.Exists(modelPath)`로 먼저 차단하는 로직이 없음을 확인했습니다. 현재 프로그램의 선검사는 VLAD_SDK 등록 전 불필요하게 실행을 막을 수 있으므로 제거하고, 경로/모델 유효성 판단은 `VLAD_Custom_Registration` 결과로 확인하도록 맞췄습니다.
- 메일로 받은 `test2_20240508_2_checkpoint` 웨이트 파일은 `checkpoint`, `ckpt-0.*`, `pipeline.config`로 구성된 사전 학습/Export 모델 입력 파일입니다. 현재 프로그램에는 기준 이미지 촬영만으로 이 checkpoint 파일을 생성하는 학습 코드가 없으며, VLAD_SDK에 제공할 모델 경로로 사용해야 합니다. 개발 PC 실행을 위해 해당 파일을 `RuntimeData/Models/VLAD/Ex_Weight`에 배치하고 `CFG/Config.json`의 `MODEL`을 이 상대 경로로 변경했습니다.
- 기존 `VLAD_SDK - Rev3` 소스를 추가 분석해 `checkpoint` 파일 생성 로직이 없고, 추론 등록은 `nets_model.json + saved_model\saved_model.pb` 또는 `model.onnx`/`model.pt`/`model.t7` 구조를 요구함을 확인했습니다. 현재 받은 checkpoint-only 폴더는 그대로는 추론 모델로 로드할 수 없으므로, `VladModelPathInspector`를 추가해 검사/RTSP 시작 전에 원인을 명확한 메시지로 차단하도록 했습니다.

## 2026-06-12

- 기존 VLAD_Ops 흐름을 다시 비교해, 모델 경로/구조 문제를 C#에서 먼저 차단하는 것은 원본 동작과 다르다고 판단했습니다. `VladModelPathInspector`는 실행 차단이 아니라 진단 로그만 남기도록 변경했고, 실제 성공/실패는 `VLAD_Custom_Registration`과 SDK 내부 로딩 결과를 따르게 했습니다.
- 검사 추론 엔진이 주입받은 `VladSdkSession`을 사용하지 않고 별도 `VLAD_Ops_Ai_Env_Start`를 호출하던 문제를 수정했습니다.
- 원본 VLAD_Ops 기준에 맞춰 RTSP 보조 Thread는 직접 VLAD 등록을 만들지 않고, 이미 등록된 `CurrentVladId`가 있을 때만 callback을 등록하도록 정리했습니다. 기준 이미지 6장 저장/일반 카메라 캡처 중에는 VLAD 모델 등록을 시도하지 않습니다.
- 원본 VLAD_Ops/Common_Lib에서 사용하던 `VLAD_Warm_Up`, `VLAD_Unregistration`을 `VladNativeMethods`, `VLAD_Ops_Ai`, `VladSdkSession`에 추가했습니다.
- `VladMeasurementMapper`의 깨진 한국어 키워드/주석을 정리하고, 길이/너비/높이/두께 검색 키워드를 정상 한국어/영문으로 복구했습니다. 치수값을 확정할 수 없을 때 기준값으로 위장하지 않고 `MeasurementUnavailable` 또는 `CalibrationMissing` 상태와 0값을 남기도록 변경했습니다.
- `AI.Vision.IOInspector.Vision` README, Vision 구현 체크리스트, VLAD_Ops gap 분석, `open-items.md`를 2026-06-12 기준으로 다시 정리했습니다.
- `dotnet build "Tests\AI-Vision IO Inspector\AI.Vision.IOInspector.sln" --configuration Debug` 결과 경고 0개, 오류 0개를 확인했습니다.
- `현재6개저장` 기준 이미지 저장이 오래된 RTSP/LibVLC 버퍼 프레임을 저장할 수 있는 문제를 점검했습니다. RTSP 단일 캡처는 ffmpeg 새 프레임 캡처를 우선 사용하고, ffmpeg가 없는 현재 PC에서는 LibVLC 캐시를 낮춘 fallback을 사용하도록 수정했습니다. 저장 완료 후 검사 UI 슬롯도 DB에 복사된 최신 기준 이미지 파일을 즉시 표시하도록 갱신했습니다.
- `검사 시작` 시 UI가 멈추는 원인을 점검했습니다. `ExecuteRunInspection`이 UI 스레드에서 `InspectionWorkflowService.RunInspection`을 직접 실행하고, AI Worker가 `CompletedEvent.WaitOne()`을 타임아웃 없이 대기하던 것이 직접 원인이었습니다. 검사 실행은 background Task로 분리하고, 검사 중 라이브 미리보기 타이머를 잠시 멈추며, AI 추론 대기는 60초 타임아웃으로 제한했습니다. 현재 설정된 VLAD 모델 폴더는 checkpoint-only 구조라 실제 추론 모델 구조 확인이 계속 필요합니다.
- 기존 VLAD_Ops도 RTSP/카메라 수신은 `VLAD_Ops_RTSP_Thread` 같은 별도 Thread로 처리하지만, `VLAD_Ops_Ai_Env_Start` 모델 등록은 CAM/MOVIE/MAP 생성 흐름에서 동기 호출됩니다. 따라서 모델 경로, DLL, GPU 런타임, checkpoint/export 구조 문제로 SDK 등록 호출이 반환되지 않으면 기존 프로그램도 화면 생성 또는 검사 시작 시 멈출 수 있습니다.
- 현재 WPF 검사 시작 흐름은 기존 VLAD_Ops의 무거운 작업 Thread 분리 방향을 따르되, `TaskScheduler.Default`를 명시해 검사 워크플로우가 반드시 UI 스레드 밖에서 실행되도록 보강했습니다.

## 2026-06-15

- 검사 시작 시 프로그램이 죽는 위험을 줄이기 위해 VLAD 추론 진입 전 모델 폴더 구조를 진단하도록 변경했습니다. 원본 VLAD_Ops에는 C# 단계에서 모델 경로를 `Directory.Exists`로 차단하는 로직이 없으므로, 현재 코드도 checkpoint-only 구조를 Debug 진단으로만 남기고 실제 성공/실패는 `VLAD_Custom_Registration`과 SDK 내부 모델 선택 결과를 따르도록 정리했습니다.
- 원본 VLAD_Ops 흐름에 맞춰 USER_CUS_STD + V1 결과 처리는 VLAD_Custom_InferenceData_V1을 사용하고, C#에서 detectData 메모리를 직접 파싱하는 경로는 기본 비활성화했습니다.
- `Docs/03-development/vision` 문서 폴더로 Vision 관련 문서를 이동했습니다. Vision 프로젝트 배포 시 Docs 폴더가 포함되지 않도록 하기 위한 정리입니다.
- 작업 완료 후 변경 범위 확인, 빌드/검증, Git 커밋, GitHub 푸시를 기본 종료 규칙으로 Docs/AGENTS.md에 반영했습니다.
- 좌측 `SEARCH DB`에서 추천어를 클릭하거나 품번/품명을 정확히 입력했을 때 검사 대상 `SelectedPart`를 갱신하도록 보완했습니다. 이제 선택 부품의 기준 이미지와 MEASUREMENT가 즉시 다시 로드되며, DB 조회/부품등록의 관리용 선택 상태는 왼쪽 검사 대상과 분리됩니다. `dotnet build` 결과 경고 0개, 오류 0개를 확인했습니다.
- 부품등록 DB 저장/삭제와 `현재6개저장` 흐름을 점검해, 저장한 품목이 좌측 `SEARCH DB` 또는 바코드로 선택된 검사 대상과 같은 품번일 때만 검사 대상과 MEASUREMENT를 새로고침하도록 변경했습니다. 다른 품목을 편집할 때는 좌측 검사 대상이 저장 품목으로 강제로 바뀌지 않습니다. 기본 출력 폴더 빌드는 실행 중인 앱의 DLL 잠금으로 실패했지만, 임시 출력 경로 빌드 결과 경고 0개, 오류 0개를 확인했습니다.
- 좌측 `SEARCH DB`를 작업대 검색으로 분리했습니다. 바코드/작업대 검색은 검사 대상만 바꾸며, DB 조회/확인과 부품등록의 냉장고 목록 필터에는 영향을 주지 않습니다.
- DB 조회/확인과 부품등록의 초록색 추천어는 마지막으로 입력한 필드가 품번/품명/분류코드/분류설명 중 무엇인지 기억한 뒤 해당 입력창에 값을 넣도록 수정했습니다. 추천 목록은 현재 입력된 품번/품명/분류코드/분류설명 조건을 AND로 만족하는 항목만 표시합니다.
- 검색 분리와 VLAD 선차단 제거 후 임시 출력 경로 `dotnet build` 결과 경고 0개, 오류 0개를 확인했습니다.
- 검사 UI의 6개 카메라 타일에서 기준이미지를 전체 반투명 오버레이로 깔지 않고, 화면을 4x4로 보았을 때 좌측 최상단 1칸에 기준이미지 인셋으로 표시하도록 변경했습니다. PASS/FAIL/ERROR는 스토리보드 마지막 장의 방향처럼 타일 전체 테두리 색상과 중앙 큰 판정 텍스트로 표시하며, `dotnet build` 결과 경고 0개, 오류 0개를 확인했습니다.
- 실시간 영상 컨트롤이 `HwndHost` 네이티브 창이라 WPF 오버레이가 영상 위에 표시되지 않는 원인을 확인했습니다. 기준이미지 인셋을 영상 호스트와 겹치지 않는 좌상단 전용 셀로 이동했고, 검사 결과 표시 시에는 네이티브 스트림을 숨겨 캡처 이미지와 중앙 PASS/FAIL/ERROR 텍스트가 보이도록 수정했습니다. `dotnet build` 결과 경고 0개, 오류 0개를 확인했습니다.
- 이전 방식은 기준이미지가 실제 영상 위에 올라온 것을 보장하지 못해, `RtspVideoHost` 내부에 기준이미지 전용 네이티브 자식 창을 추가했습니다. 저장된 기준 이미지는 WPF/WIC로 읽고 Win32 DIB 비트맵으로 변환해 라이브 RTSP 영상 HWND 위 좌상단에 표시합니다. 실제 앱 실행 화면 캡처 `rtsp-overlay-mainapp-check-topmost-kept.png`에서 Top/Front 영상 위 기준 이미지 인셋 표시를 확인했고, `dotnet build` 결과 경고 0개, 오류 0개를 확인했습니다.

## 2026-06-16

- 검사 시작 시 WPF 앱이 종료되는 원인을 재검토했습니다. 실제 VLAD 초기화 경로를 워커에서 실행하자 VisionWorker가 ExitCode=-1073740791로 비정상 종료됐고, 출력에는 TensorFlow cudart64_110.dll 관련 메시지가 남았습니다. 이는 C# try/catch로 잡을 수 없는 네이티브 프로세스 종료 케이스입니다.
- AI 추론을 WPF 프로세스 내부에서 직접 실행하지 않고, AI.Vision.IOInspector.VisionWorker.exe 별도 프로세스로 실행하도록 분리했습니다. 메인 앱은 요청 JSON을 만들고 응답 JSON을 읽으며, 워커가 죽으면 검사 결과 Error와 Event 로그로 실패 사유를 표시합니다.
- 기준 이미지 누락은 VLAD 사전 차단이 아니라 업무 규칙으로 유지합니다. 기준 이미지가 없으면 등록 안내를 표시하고, 저장 후 다시 검사 시작이 가능하도록 합니다.
- dotnet build 전체 솔루션 검증 결과 경고 0개, 오류 0개를 확인했습니다. 앱 출력 폴더의 VisionWorker 하위에 exe/runtimeconfig/native DLL이 배치되는 것도 확인했습니다.
- 검사 탭 상단에 화면 초기화 버튼을 추가했습니다. 검사 중에는 비활성화되며, 검사 완료 후 누르면 캡처 이미지/결과 테두리/Event를 지우고 선택 품목의 기준 이미지와 기준 측정부 상태로 되돌립니다.
- DB 조회/확인과 부품등록의 초록색 추천어는 검색 조건이 없을 때 표시하지 않도록 변경했습니다. 좌측 SEARCH DB도 빈 검색어에서는 추천어를 숨깁니다.
- 검사 시작 후 Error 결과 메시지가 버튼 영역과 겹치지 않도록 버튼 우측 결과 영역을 별도 열로 분리하고 긴 메시지는 줄바꿈되도록 조정했습니다.
- dotnet build 전체 솔루션 검증 결과 경고 0개, 오류 0개를 확인했습니다.
- 현재 프로젝트의 전체 구조를 다시 정리했습니다. `App/Application/Domain/Infrastructure/Vision/VisionWorker` 프로젝트 책임, 검사 시작 Job Flow, DB/Image/History 저장 구조, 카메라/Vision 구성, 남은 미비/추가 개발 항목을 `Docs/03-development/project-structure-2026-06-16.md`에 구조도로 작성했습니다.

## 2026-06-17

- `Docs/05-simulator/barcode-scanner` 경로에 `BarcodeScannerSample` WPF .NET 9.0 MVVM 샘플을 추가했습니다. `Start Reading` 버튼으로 바코드 리딩을 시작한 뒤, Epson ES-C320W 연계 도구 또는 키보드 입력으로 들어온 바코드 문자열을 Enter 입력 시 ListBox에 시간과 함께 누적합니다.
- 샘플은 `MainViewModel`, `RelayCommand`, `BarcodeItem`으로 분리했습니다. 외부 NuGet 패키지는 추가하지 않았고, 이미지 스캔 후 바코드 디코딩이 필요한 경우 `ZXing.Net` 또는 WIA/TWAIN 연동 서비스를 나중에 붙일 수 있도록 README에 확장 방향을 기록했습니다.
- 기본 출력 폴더 빌드는 실행 중인 `BarcodeScannerSample.exe` 잠금 때문에 실패했습니다. 임시 출력 경로 `tmp-build/barcode-scanner-start`로 `dotnet build`를 실행해 경고 0개, 오류 0개를 확인했습니다.
- 바코드 스캐너 샘플을 별도 시뮬레이터로 Visual Studio에서 바로 열 수 있도록 `BarcodeScannerSample.sln`을 추가하고 `BarcodeScannerSample.csproj`를 솔루션에 등록했습니다. 임시 출력 경로 `tmp-build/barcode-scanner-sln`로 솔루션 빌드 결과 경고 0개, 오류 0개를 확인했습니다.
- `Start Reading` 버튼의 동작을 실제 스캔 흐름으로 보강했습니다. WIA `DeviceManager`에서 `EPSON ES-C320W`를 자동 검색하고, 300dpi/회색조/PNG 설정을 적용한 뒤 스캔 이미지를 `Scans` 폴더에 저장하고 ZXing으로 디코딩해 ListBox에 추가합니다.
- 스캐너가 연결되지 않은 상태에서도 검증할 수 있도록 `Decode Image File` 버튼을 추가했습니다. 사용자가 제공한 `바코드.jpg` 파일은 ZXing 별도 검증에서 `2650006854240001`로 디코딩됨을 확인했습니다.
- 임시 출력 경로 `tmp-build/barcode-scanner-auto-epson`로 솔루션 빌드 결과 경고 0개, 오류 0개를 확인했습니다.

## 2026-06-17

- 기존 바코드 디코딩 샘플과 별도로 `Docs/05-simulator/Scanner`에 스캔 용지 OCR 샘플을 새로 만들었습니다. 목적은 바코드 값을 읽는 것이 아니라, 스캔된 용지에서 `검수` 글자 옆 최상단 코드 예: `31S7-12020`를 OCR로 읽어 ListBox에 추가하는 것입니다.
- EPSON ES-C320W를 WIA 장치 목록에서 자동으로 찾고, 페이지 크기 자동 감지, 회색조 기본, 300dpi, PNG 저장 조건을 적용하도록 구성했습니다. WIA 드라이버가 특정 속성을 노출하지 않으면 해당 속성은 드라이버 기본값으로 동작합니다.
- 스캔 이미지는 `Scans\Raw`에 원본 PNG로 저장하고, 0/90/180/270도 후보 이미지를 Windows OCR로 비교한 뒤 최종 정방향 이미지를 `Scans` 폴더에 저장합니다.
- 제공된 `바코드.jpg` 이미지 파일로 OCR-only 검증을 수행했고, `31S7-12020` 코드 추출을 확인했습니다. `ScannerSample.sln` 빌드도 경고 0개/오류 0개로 통과했습니다.
- 실제 EPSON ES-C320W 하드웨어 스캔은 WIA 드라이버 연결 상태에서 추가 확인이 필요합니다.

## 2026-06-17

- Scanner OCR 샘플 실행 중 `CheckReadOnly` 예외가 발생하는 원인을 확인했습니다. `TextBox.Text`는 기본 바인딩 모드가 TwoWay인데, 대상 속성 `LastExtractedCode`는 ViewModel 외부에서 수정하지 못하도록 private setter로 관리하고 있어 WPF가 소스 업데이트를 시도하면서 예외가 발생했습니다.
- 해당 바인딩을 `Text={Binding LastExtractedCode, Mode=OneWay}`로 변경했습니다. 같은 샘플 내에서 TwoWay 문제가 될 수 있는 다른 `TextBox.Text` 바인딩은 추가로 발견되지 않았습니다.
- `ScannerSample.sln` 빌드 결과 경고 0개/오류 0개를 확인했습니다.

## 2026-06-17

- Scanner OCR에서 `AOU-LSLT`가 나온 원인을 확인했습니다. OCR이 라벨 주변 글자/노이즈를 영문-영문 패턴으로 잘못 읽었고, 기존 추출식이 이를 코드로 허용하고 있었습니다.
- 코드 추출 규칙을 `31S7-12020` 계열에 맞게 좁혔습니다. 왼쪽 코드는 문자와 숫자를 모두 포함해야 하고, 오른쪽 값은 숫자 중심이어야 합니다. 실제 스캔 OCR에서 `31 S7-12020`처럼 내부 공백이 생기면 `31S7-12020`으로 정규화합니다.
- 실제 raw 스캔 이미지는 흰 페이지가 대부분이므로, 밝은 라벨 찾기 대신 어두운 글자/바코드 콘텐츠 영역으로 라벨을 crop하도록 수정했습니다. 휴대폰 사진처럼 어두운 배경인 경우에는 기존처럼 밝은 라벨 영역 기준 crop을 사용합니다.
- 제공된 raw 스캔 이미지와 기존 사진 이미지 모두 OCR 결과 `31S7-12020` 추출을 확인했습니다. 최종 저장 이미지는 라벨 중심으로 crop되고 정방향으로 저장됨을 확인했습니다.
- `ScannerSample.sln` 빌드 결과 경고 0개/오류 0개를 확인했습니다.

## 2026-06-18

- VLAD/TensorFlow 네이티브 초기화 중 `cudart64_110.dll` 미탐지와 `0xc0000409` 프로세스 종료를 확인했습니다.
- `cudart64_110.dll`은 NVIDIA CUDA Runtime 11.0 DLL입니다. 현재 테스트 PC는 RTX 4060, NVIDIA Driver 581.86, `nvidia-smi` CUDA 13.0 표시 상태이나 CUDA 11.0 런타임 DLL은 PATH에서 확인되지 않았습니다.
- 조치 방향은 NVIDIA CUDA Toolkit 11.0 Update 1 설치 후 `where cudart64_110.dll`로 인식 여부를 확인하는 것입니다. 이후 `cudnn64_8.dll`, `cublas64_11.dll` 등 추가 TensorFlow GPU 의존성이 요구되는지 이어서 확인합니다.
- 단일 DLL 다운로드 사이트는 사용하지 않고, NVIDIA 공식 설치본 또는 VLAD 담당자가 제공한 배포 세트 기준으로 구성합니다.

- NativeDependencyLoader에 CUDA 11.0 bin 경로 자동 등록과 CUDA 핵심 DLL 선로드를 추가했습니다. 앱 시작 재검증 결과 TensorFlow 로그가 `Successfully opened dynamic library cudart64_110.dll`로 변경되어 `cudart64_110.dll` 로드 문제는 해결됐습니다. 이후 실제 추론 단계에서 `cudnn64_8.dll` 등 추가 GPU DLL 요구 여부를 이어서 확인합니다.

- CUDA Runtime 11.0 로드는 성공했지만, 이어서 WPF 본체가 `0xc0000409`로 종료되는 문제를 확인했습니다. 현재 모델 폴더가 추론 export 구조가 아닌 checkpoint-only 구조라 `VLAD_Custom_Registration` 내부에서 네이티브 종료가 발생할 수 있으므로, WPF 본체 시작 시 VLAD_Ops_Ai_Env_Start 직접 호출을 제거하고 실제 추론은 `VisionWorker.exe` 격리 프로세스로 되돌렸습니다.
- VLAD RTSP Thread의 in-process `EnsureLoaded()` 호출도 기본 비활성화했습니다. 필요한 경우 `AI_VISION_ENABLE_INPROCESS_VLAD_RTSP=1`로만 켭니다. 앱 시작 20초 검증 결과 프로세스는 종료되지 않았고, NVR RTSP 연결 실패 로그만 확인됐습니다.
- 검사 시작 조건을 다시 정리했습니다. 기준 이미지가 일부 없거나 전혀 없는 상태는 검사 하드 차단 조건이 아니라 등록을 유도해야 하는 조건입니다. 따라서 기준 이미지 누락 시 확인 팝업으로 안내하고, 사용자가 계속 진행을 선택하면 검사를 시도하도록 변경했습니다. 부품 기준정보 자체가 없는 경우에는 길이/너비/높이/두께 등 판정 기준이 없으므로 등록 필요 상태로 계속 차단합니다.
- `dotnet build "Tests\AI-Vision IO Inspector\AI.Vision.IOInspector.sln" -c Debug -p:Platform=x64` 결과 경고 0개/오류 0개를 확인했습니다.

## 2026-06-30

- 검사 시작 후 Search DB로 선택한 품목의 기준 측정부 값이 검사 결과 행으로 교체되어 초기화처럼 보이던 문제를 수정했습니다.
- 검사 이력 측정부 표시를 기준 측정부 행 중심으로 유지하고, AI/측정 결과의 측정값과 판정만 병합하도록 정리했습니다.
- 옵션 화면에 현재 연결된 고정 HDD의 전체/사용/여유 용량과 사용률을 표시하도록 추가했습니다.
- 상단 개발용 문구를 제거하고 실행 파일 기준 `버전 / 빌드일시` 표시로 변경했습니다.
- 검사는 이미지 AI 검사와 기준값 비교 검사를 하나의 실행 흐름에서 함께 수행하고 최종 OK/NG를 생성하도록 흐름을 분리했습니다.
- `InspectMat` 전달용 `inspectionContextJson` 형식을 Vision 담당자가 확인할 수 있도록 `Docs/03-development/vision/inspectmat-context-json.md`에 정리했습니다.
- SearchDB Measurement UI는 기준정보 확인용으로 `측정부 / 항목 / 기준값 / 허용값`만 표시하도록 정리했습니다.
- 6대 NVR 카메라 연결 테스트용 `CFG/HD_Config.json`을 추가했습니다. 실제 연결 테스트 시 이 파일명을 `Config.json`으로 바꿔 적용합니다.
- 검사 이미지/로그 삭제 관리 대상 루트를 `DB/Inspection_Data`로 분리하고, `YYYY/MM/DD/HH/History, Image, Log` 구조를 준비했습니다. 검사 결과 요약은 `History`, 이벤트 로그는 `Log`, 캡처 이미지는 `Image` 하위에 저장됩니다.
- 옵션 탭에 HDD 여유공간 기준 자동삭제, 설정기간 이후 자동삭제, 설정기간 기본 365일, 설정 저장 버튼을 추가했습니다. 삭제 후보는 1시간마다 확인하고, 실제 삭제 전 사용자 확인 팝업을 띄우도록 구현했습니다.

## 2026-06-19

- 프로그램 시작 시 `VLAD_Ops_Ai_Env_Start`가 다시 호출되도록 복구했습니다. 단, WPF 본체에서 직접 호출하면 네이티브 SDK가 프로세스를 종료시킬 수 있으므로 `VisionWorker.exe --initialize` 격리 프로세스에서 호출합니다.
- 시작 초기화 로그를 `DB\Logs\vlad-startup.log`에 남기도록 했습니다. 로그에는 `MODEL`, `GPU`, `TF_FORCE_GPU_ALLOW_GROWTH`, `CUDA_DEVICE_ORDER`, `CUDA_VISIBLE_DEVICES`, CUDA/cuDNN DLL 존재 여부, 모델 구조 진단이 포함됩니다.
- 원본 VLAD_Ops와 맞춰 모델 등록 전 `TF_FORCE_GPU_ALLOW_GROWTH=true`, `CUDA_DEVICE_ORDER=PCI_BUS_ID`, `CUDA_VISIBLE_DEVICES=0`을 설정하도록 보강했습니다.
- 검사 시작 시 카메라 Worker 시작과 함께 `VLAD_Ops_RTSP_Thread` 시작 시도를 수행하도록 연결했습니다. WPF 본체 보호를 위해 실제 native RTSP Thread 실행은 `AI_VISION_ENABLE_INPROCESS_VLAD_RTSP=1`일 때만 허용하며, 시작/스킵/실패 사유는 `DB\Logs\vlad-rtsp.log`에 기록합니다.
- 프로그램 시작 시뮬레이션 결과 앱 본체는 30초 이상 유지됐고, `VisionWorker`가 `VLAD_Ops_Ai_Env_Start`를 시도하는 로그가 남았습니다. 다만 Worker는 `cudart64_110.dll` 로드 직후 `ExitCode=-1073740791`로 종료됐습니다.
- 진단 결과 CUDA 11.0 런타임과 cuBLAS는 존재하지만 `cudnn64_8.dll`은 누락되어 있습니다. 또한 현재 `RuntimeData\Models\VLAD\Ex_Weight`에는 `checkpoint`, `ckpt-0.*`, `pipeline.config`만 있어 VLAD_SDK가 직접 읽는 추론 모델 구조가 아닙니다. 필요한 구조는 `nets_model.json + saved_model\saved_model.pb` 또는 `model.onnx/model.pt/model.t7`입니다.
- 검사 흐름 서비스 시뮬레이션 결과 `CaptureAll` 경로에서 RTSP Thread 시작 시도 지점까지 도달했고, 기본 보호 플래그가 꺼져 있어 6개 채널 모두 스킵 로그가 남았습니다. 플래그를 켠 별도 시뮬레이션 프로세스는 `Env_Start` 단계에서 `ExitCode=-1073740791`로 종료되어 실제 `VLAD_Ops_RTSP_Thread` 시작에는 도달하지 못했습니다.
- `dotnet build "Tests\AI-Vision IO Inspector\AI.Vision.IOInspector.sln" -c Debug -p:Platform=x64` 결과 경고 0개/오류 0개를 확인했습니다.
- `VisionWorker`가 `cudart64_110.dll` 로드 직후 `ExitCode=-1073740791`로 종료되는 원인을 재검증했습니다. 현재 PC에는 `cudnn64_8.dll`이 없고, `RuntimeData\Models\VLAD\Ex_Weight`도 checkpoint-only 구조라 네이티브 `VLAD_Ops_Ai_Env_Start`를 호출하면 Worker가 fail-fast로 종료될 수 있습니다.
- `VladRuntimePreflight`를 추가해 네이티브 호출 전에 CUDA/cuDNN DLL과 VLAD 추론 모델 구조를 진단하도록 했습니다. 이 preflight는 기준 이미지 유무 같은 업무 조건을 차단하지 않고, 호출 즉시 Worker가 종료될 수 있는 네이티브 환경 결함만 다룹니다.
- 시작 초기화 Worker는 필수 환경이 부족하면 `ExitCode=3`으로 안전 종료하고 `DB\Logs\vlad-startup.log`에 `WORKER_SKIPPED` 및 서비스 레벨 `SKIPPED`를 남깁니다. 수동 실행 결과 더 이상 `-1073740791`로 종료되지 않았습니다.
- 검사 Worker도 같은 환경에서는 비정상 종료 대신 실패 결과 JSON을 생성하도록 변경했습니다. 최소 request JSON 시뮬레이션 결과 `ExitCode=0`, `IsSuccess=false`, 원인 메시지 반환을 확인했습니다.
- 앱 시작 경로를 8초 실행해 `START_REQUEST -> WORKER_START -> WORKER_SKIPPED -> SKIPPED` 로그가 남는 것을 확인했습니다.
- `dotnet build "Tests\AI-Vision IO Inspector\AI.Vision.IOInspector.sln" -c Debug -p:Platform=x64` 결과 경고 0개/오류 0개를 확인했습니다.
- cuDNN 설치 후 별도 수동 복사 없이 인식할 수 있도록 `NativeDependencyLoader`와 `VladRuntimePreflight`에 `AI_VISION_CUDNN_PATH`, `CUDNN_PATH`, NVIDIA cuDNN 기본 설치 폴더, 실행 PATH 탐색을 추가했습니다. `cudnn64_8.dll`도 CUDA DLL 선로드 대상에 포함했습니다.
- 재시뮬레이션 결과 시작 초기화 Worker는 `ExitCode=3`, 검사 Worker는 `ExitCode=0` 실패 결과 JSON, 앱 시작 경로는 `START_REQUEST -> WORKER_SKIPPED -> SKIPPED`로 확인됐습니다. 현재 PC에는 여전히 `cudnn64_8.dll`이 없어 로그는 `CUDNN_PATH=(empty)`로 남습니다.
- `dotnet build "Tests\AI-Vision IO Inspector\AI.Vision.IOInspector.sln" -c Debug -p:Platform=x64` 결과 경고 0개/오류 0개를 확인했습니다.
- 원본 VLAD_Ops와 현재 사용 경로를 비교해 `VLAD_Ops_Ai_Compat`, `VladFunctionAdapter`, `VladRuntimeContext`가 현재 운영 흐름에서 사용되지 않고 원본에도 없는 중복 호환 계층임을 확인했습니다. 세 파일을 제거하고 공식 진입점을 `VLAD_Ops_Ai`, 세션 관리를 `VladSdkSession`, 결과 파싱을 `VladInferenceResultParser`로 정리했습니다.
- `open-items.md`를 2026-06-19 기준으로 재정리했습니다. 남은 항목은 `O-001`~`O-015`로 통합했고, 완료된 Worker preflight 방어, `SKIPPED` 안전 종료, 실패 결과 JSON 반환, 미사용 VLAD 호환 계층 제거는 정리된 항목으로 분리했습니다.
- `project-structure-2026-06-19.md`의 용량 점검 기준에서 날짜별 ZIP 백업 파일은 삭제하지 않는 보존 대상으로 명시했습니다. 앞으로 용량을 줄일 때는 ZIP이 아니라 재생성 가능한 `bin`, `obj`, `publish` 산출물부터 검토합니다.
- 용량 증가 원인을 점검했습니다. 2026-06-19 ZIP이 커진 주된 이유는 ZIP 중첩이 아니라 기존 Debug/x64 산출물 안에 `tensorflow.dll` 같은 대형 `Native\VLAD` DLL이 App, VisionWorker, App 하위 VisionWorker 폴더에 반복 복사됐기 때문입니다. Debug 빌드는 루트 `Native\VLAD`를 직접 참조하고, `CFG`/`Native\VLAD` 복사는 publish 산출물에서만 수행하도록 변경했습니다. 또한 `RuntimeIdentifier=win-x64`로 고정해 불필요한 다중 플랫폼 runtimes 산출물을 만들지 않도록 했습니다. 재생성 가능한 `bin`, `obj`, `publish`, 시뮬레이터 `bin/obj`만 정리했고 ZIP/DB/기준 이미지/검사 이력/Native/RuntimeData는 보존했습니다. x64 Debug 빌드 결과 경고 0개/오류 0개를 확인했습니다.
- 카메라 설정 로드 기준을 `CFG\Config.json`으로 변경했습니다. 기존 옵션 UI에서 관리하던 `RuntimeData\Camera\camera-config.json`의 6채널 설정은 `CFG\HD_BackupConfig.json`으로 같은 `LAST/CUSTOM/HD/CAMS` 구조에 백업했습니다.
- `VisionWorker.exe --test-config-rtsp` 콘솔 진단 인자를 추가했습니다. 실행 결과 `Config.json`의 `rtsp://210.99.70.120:1935/live/cctv001.stream`을 Top 채널로 읽었고, RTSP 프레임 수신 완료 및 테스트 이미지 저장을 확인했습니다.
- `dotnet build "Tests\AI-Vision IO Inspector\AI.Vision.IOInspector.sln" -c Debug -p:Platform=x64` 결과 경고 0개/오류 0개를 확인했습니다. 중간에 `AI.Vision.IOInspector.Vision` 프로젝트가 `Exe`로 잘못 설정되어 `Main` 진입점 오류가 발생해 `OutputType=Library`로 수정했습니다.
- `VLAD_Ops_Ai_Env_Start`와 `VLAD_Custom_Registration`의 호출 전후 추적 로그를 추가했습니다. 실제 등록이 실행되면 `DB\Logs\vlad-startup.log` 또는 `DB\Logs\vlad-registration.log`에 `ENV_START_ENTER`, `CUSTOM_ID_GENERATED`, `CUSTOM_REGISTRATION_CALL`, `CUSTOM_REGISTRATION_RETURN`, `ENV_START_RETURN` 순서로 기록되고 반환 `VladId`가 16진수 포인터 값으로 남습니다.
- 현재 PC에서 `VisionWorker.exe --initialize`를 실행한 결과 `LASTEXITCODE=3`이고, `vlad-startup.log`에 `REGISTRATION_NOT_STARTED`가 남았습니다. 즉 현재 환경에서는 `cudnn64_8.dll` 누락과 checkpoint-only 모델 구조 때문에 `VLAD_Ops_Ai_Env_Start` 및 `VLAD_Custom_Registration`이 실제 호출되지 않습니다.
- `RuntimeData\Camera\camera-config.json`은 더 이상 런타임 기준 설정으로 사용하지 않으므로 파일을 삭제했습니다. `CameraConfigurationStore`에서도 해당 파일을 legacy 백업 소스로 확인하던 로직을 제거했고, 현재 설정 기준은 `CFG\Config.json`으로 단일화했습니다.
- `vlad-startup.log`의 `cudnn64_8.dll을 찾을 수 없습니다`, `현재 MODEL 경로에는...` 메시지는 VLAD DLL 내부 로그가 아니라 관리 코드의 `VladRuntimePreflight`가 네이티브 호출 전에 남기는 진단 로그입니다. 이 상태에서는 DLL 내부 `VLAD_Custom_Registration`까지 들어가지 않으므로 DLL 내부 콘솔 로그가 나오지 않는 것이 정상입니다.
- `VLAD VisionWorker started`가 `Error=`로 붙어 혼동되던 문제는 Worker 시작 메시지를 표준 오류가 아닌 표준 출력으로 변경해 정리했습니다.


## 2026-06-22

- 메인 솔루션 문서를 현재 구조 기준으로 다시 맞췄습니다. 현재 기준은 Visual Studio 2022, WPF MVVM, .NET Framework 4.7.2, x64 전용입니다.
- README 두 곳을 갱신해 빌드 명령, 출력 폴더, `CFG/DB/Native/VLAD/RuntimeData/Models` 배포 구조, 현재 Vision 실행 흐름을 정리했습니다.
- `open-items.md`를 2026-06-22 기준으로 재정리했습니다. 남은 항목은 VLAD 최종 모델, CUDA/cuDNN/VC++ Runtime, 6채널 RTSP/NVR 검증, VLAD 결과 스키마, pixel-mm 보정, History 보존 정책, 배포 패키지, Git 대용량 파일 정책 등으로 정리했습니다.
- `project-structure-2026-06-22.md`를 새로 작성했습니다. 앱 시작 흐름, 검사 시작 흐름, 프로젝트 책임, 런타임 데이터 위치, 출력 폴더 배포 구조를 한 문서에서 확인할 수 있도록 했습니다.
- Vision 관련 문서를 현재 코드 기준으로 갱신했습니다. WPF 기본 흐름은 별도 `VisionWorker.exe`가 아니라 `VisionInferenceWorker` 전용 스레드에서 `VladVisionInferenceEngine`을 호출하는 구조입니다. `AI.Vision.IOInspector.VisionWorker` 프로젝트는 진단/레거시 용도로 남아 있음을 명시했습니다.
- .NET Framework 4.7.2 환경에서 `Native\VLAD` 하위 관리 DLL을 찾지 못하던 문제는 `RuntimeAssemblyResolver`가 `AssemblyResolve`, `PATH`, `SetDllDirectory`를 등록하는 방식으로 정리했습니다.
- 개발 규칙, 질문, 작업보드, VLAD/IMV 변환 가이드, 카메라/AI 연동 문서에 남아 있던 메인 앱 `.NET 9` 기준 표현을 현재 기준으로 수정했습니다. 단, `Docs/05-simulator` 하위 샘플 프로젝트는 별도 실험용 `.NET 9` 프로젝트이므로 그대로 유지했습니다.
- 프로젝트 브리프, 아키텍처, 데이터 모델, 요구사항 문서도 현재 구현 기준으로 정리했습니다. DBMS는 SQLite로 확정했고, 실제 테이블은 `PartList_*`와 `History_*` 구조로 문서화했습니다. 화면명은 `DB 조회/확인`과 `부품등록/DB관리` 기준으로 수정했습니다.
- `source-analysis.md`와 `screen-map.md`도 현재 화면명 기준으로 정리했습니다. 초기 자료의 `DB 확인`은 `DB 조회/확인`, `DB Update`는 `부품등록/DB관리`의 단일/다중 등록 및 DB 저장 기능으로 반영했습니다.
- 검증으로 `dotnet build "Tests\AI-Vision IO Inspector\AI.Vision.IOInspector.sln" -c Release -p:Platform=x64`를 실행했고 경고 0개/오류 0개를 확인했습니다. Debug 빌드는 실행 중인 `AI.Vision.IOInspector.App` 프로세스가 `AI.Vision.IOInspector.Vision.dll`을 잠그고 있어 실패했으며, 코드 오류가 아니라 파일 잠금 문제로 판단했습니다.

## 2026-06-23

- `VLAD_Inference_Mat` 호출 중 `System.AccessViolationException`이 발생한 문제를 점검했습니다. 현재 구조에서는 앱 시작 시 등록된 VLAD RTSP callback과 검사 시작 시 파일 기반 캡처 추론이 같은 `VladId`로 네이티브 추론 함수를 동시에 호출할 수 있어, VLAD_SDK 내부 메모리 경합 가능성이 있습니다.
- `VLAD_Ops_Ai.NativeInferenceSyncRoot`를 기준으로 `VLAD_Inference_Mat`, `VLAD_InferenceData_Get_Valid_Count`, `VLAD_Custom_InferenceData_V1`, `VladInferenceResultParser.Parse` 구간을 직렬화했습니다.
- `VLAD_Inference_Mat` 또는 결과 파싱 중 보호 메모리 예외가 발생하면 `NativeInferenceBlocked` 상태로 전환해 같은 프로세스의 이후 네이티브 추론을 중지하고, UI에는 검사 실패 메시지가 반환되도록 변경했습니다.
- 노트북 테스트 환경에서 `gpuId = 1`을 사용해야 하므로, 기존 테스트용 GPU 강제값은 유지했습니다.
- `dotnet build "Tests\AI-Vision IO Inspector\AI.Vision.IOInspector.sln" -c Release -p:Platform=x64` 결과 경고 0개/오류 0개를 확인했습니다.

- 부품등록과 DB 조회/확인 이미지 미리보기를 7개 고정 슬롯으로 변경했습니다. Top/Front/Back/Left/Right/Thickness와 측정부 좌표 이미지를 상단 제목으로 구분하며, 이미지가 없을 때도 슬롯 위치가 유지됩니다.
- DB 조회/확인 상단의 등록 기준 이미지 목록 폭을 줄이고 측정부 상세 폭을 늘렸습니다. DB 목록과 이미지 목록은 컬럼 전체 폭이 화면보다 클 때 가로 스크롤이 표시됩니다.
- 검사 완료 화면은 측정 이미지를 전체 영역에 표시하고 등록 기준 이미지를 좌측 상단 1/4에 인셋으로 표시하도록 변경했습니다.
- `ImageViewType.Unclassified`를 추가했지만 카메라/기준 이미지 운영 수량에는 포함하지 않아 기존 6채널 규칙을 유지합니다.
- 잠긴 CSV 파일을 읽거나 같은 이름으로 저장하는 상황을 테스트했습니다. 읽기/쓰기 모두 false와 사용자 안내 메시지를 반환해 앱 예외로 전파되지 않음을 확인했습니다.
- 자동 회귀 검증 결과 등록 미리보기 7슬롯 순서, 잠긴 CSV 처리, 시뮬레이션 카메라 6채널 유지가 모두 정상입니다. Release x64 빌드도 경고 0개/오류 0개입니다.

## 2026-06-24

- `RE: [전달] HD현대사이트솔루션 프로젝트 - 내부 개발 문의사항 전달` 메일을 확인했습니다. AI 담당자 답변 기준으로 길이/너비/높이/두께 고정 세트가 아니라 `측정부1`, `측정부2`처럼 독립 측정부를 지정합니다. 애플리케이션은 Thickness 이미지와 선 좌표를 DLL에 전달하고, Crop/추론/측정은 AI DLL 내부에서 수행하는 경계로 정리했습니다.
- 옵션 카메라 목록은 `CFG\Config.json`의 `CAMS`에 실제 존재하는 행만 표시하도록 변경했습니다. 설정 저장 시 기존 JSON 구조를 보존하고 카메라의 `CAM_X`, `CAM_Y`, `CAM_TYPE`, `CAM_RTSP_IP`만 갱신합니다.
- .NET Framework 4.7.2 격리 테스트에서 `System.Text.Json.Nodes` 저장 경로가 불안정한 것을 확인해 `JavaScriptSerializer` 기반으로 교체했습니다. Config 복사본 읽기/저장/재읽기 결과 CAM0 1개와 기존 4개 키가 그대로 유지됐습니다.
- 단일품목 등록 화면의 기준 이미지와 측정부 위치를 교환했습니다. 측정부는 최대 10개를 추가/삭제할 수 있고 중간 삭제 후 번호를 다시 정렬합니다.
- 측정부 컬럼은 기준값, 허용값, 항목, 위치 표시, X1/Y1/X2/Y2, 단위로 변경했습니다. 항목은 미설정/길이/너비/높이/두께이며 단위는 mm로 고정했습니다.
- Thickness 기준 이미지 위치 지정 창을 추가했습니다. 두 점 선택, 선 표시, 누적 보기, 우클릭 취소, 기본 10색, RGB 직접 입력을 지원합니다. 선은 AI Crop 위치 안내용이며 프로그램이 선 길이로 실제 치수를 계산하지 않습니다.
- SQLite `schema_version=2`와 `PartList_MeasurementPoints`를 추가했습니다. 원본 DB 복사본 마이그레이션 결과 34,226개 측정부, 11,407개 품목, 품목별 최대 4개가 변환됐습니다.
- 신규 측정부 저장 왕복 테스트에서 IndexNo, 항목, 기준값, 허용값, X1/Y1/X2/Y2, 색상, Thickness ViewType이 동일하게 복원되는 것을 확인했습니다.
- Release x64 전체 솔루션 빌드는 경고 0개/오류 0개입니다. Debug 빌드는 실행 중인 앱 프로세스가 DLL을 잠가 복사 단계에서만 실패했습니다.
- 측정부 위치 표시 버튼 클릭 시 `MeasurementPositionWindow.LoadBitmap()`의 `BitmapImage.EndInit()`에서 `ArgumentNullException: 키는 null일 수 없습니다`가 발생하는 문제를 재현했습니다. .NET Framework 4.7.2 WPF의 StreamSource 이미지 캐시가 null Uri를 제거하는 경로가 원인이었습니다.
- 위치 지정 창은 `BitmapDecoder.Create(..., BitmapCacheOption.OnLoad)`로 첫 프레임을 메모리에 적재하도록 변경했습니다. 실제 `01100-51430_Thickness.bmp` 960x720 Bgr24 파일로 창 생성/렌더링/자동 종료 STA 테스트를 실행했고 `ExitCode=0`을 확인했습니다.
- 위치 창 생성이나 이미지 디코딩이 실패하더라도 메인 앱이 종료되지 않도록 다이얼로그 서비스와 ViewModel 호출 경계에 예외 처리를 추가했습니다.
- 옵션 설정 저장이 느렸던 원인은 저장 직후 `RefreshCameraStatuses(true)`가 모든 카메라의 RTSP 연결과 실제 프레임 수신을 UI 스레드에서 순차 실행했기 때문입니다. 설정 저장은 파일 저장/재로드만 수행하고 실제 영상 검증은 상태 새로고침 또는 연결 테스트로 분리했습니다.
- `현재6개저장` 흐름을 `DB\Image\Temp\품번` 임시 저장 방식으로 변경했습니다. 같은 품번을 재촬영하면 해당 Temp 작업본만 지우며 최종 이미지와 OldVer 백업은 DB 저장 전까지 보존합니다.
- 측정부 위치를 확정하면 Thickness 이미지에 현재 등록된 모든 측정부 선을 누적한 `coordinate.png`를 Temp에 생성합니다. DB 저장 시 촬영본과 coordinate를 최종 `DB\Image\분류코드\품번` 폴더로 확정하고 기존 파일은 OldVer로 백업합니다.
- 부품등록 및 DB 조회/확인의 등록 기준 이미지 목록에 등록시간을 추가했습니다. Temp 작업본은 `DB 저장 대기`, 최종본은 DB에 저장되는 `captured_at` 시각을 표시합니다.
- 검사 이벤트 흐름을 1차 이미지 정합성 검사와 2차 측정값 정합성 검사로 구분했습니다. 최종 OK는 두 단계가 모두 정상일 때만 가능합니다.
- 별도 임시 루트에서 Temp 촬영본 생성, coordinate 생성, 최종 파일 확정, 등록시간 설정, Temp 삭제를 검증했습니다. 같은 방향을 두 번 확정했을 때 현재 파일 1개와 OldVer 1개가 유지되는 것도 확인했습니다.
- `dotnet build "Tests\AI-Vision IO Inspector\AI.Vision.IOInspector.sln" -c Release -p:Platform=x64` 결과 경고 0개/오류 0개입니다.
- 검사 완료 화면에서 등록 기준 비교 이미지가 위쪽, 검사 시점 측정 이미지가 아래쪽에 동시에 표시되도록 카메라 타일을 분할했습니다. 검사 전에는 실시간 스트림 중심 화면을 그대로 유지합니다.
- 메인 탭은 기본 WPF 외형을 제거하고 선택 탭 파란색, 비선택 탭 본문 계열 색상, hover 강조를 갖는 전용 스타일로 변경했습니다. 부품등록의 단일/다중품목 탭에는 더 작은 보조 스타일을 적용했습니다.
- UI 변경 후 Release x64 전체 솔루션 빌드 결과 경고 0개/오류 0개입니다.
- Top/Back 등 검사 영상이 끊기는 원인을 점검했습니다. `CFG\Config.json`의 CAM0~CAM5가 모두 `rtsp://210.99.70.120:1935/live/cctv001.stream`으로 같아 UI LibVLC 재생과 VLAD RTSP 등록이 같은 영상을 여러 번 디코딩하고 있었습니다.
- 실행 중인 구버전 앱은 5초 동안 약 7.53 CPU seconds, 평균 약 1.51 logical core, 메모리 약 673MB를 사용했습니다.
- 동일 RTSP URL은 UI에서 최초 채널만 재생하고 중복 채널에는 설정 오류를 표시하도록 변경했습니다. 실제 6대 카메라 연결 시에는 CAM0~CAM5 각각 고유 RTSP URL이 필요합니다.
- 코드 설명과 다르게 VLAD RTSP가 환경변수 미설정 상태에서 기본 활성화되던 조건을 수정했습니다. 이제 `AI_VISION_ENABLE_INPROCESS_VLAD_RTSP=1` 또는 `true`일 때만 등록하고 동일 URL은 한 번만 등록합니다.
- LibVLC 엔진은 프로세스당 하나만 생성하고 채널별 MediaPlayer를 사용하도록 바꿨으며 하드웨어 디코딩 자동 선택 옵션을 추가했습니다. Release x64 빌드 결과 경고 0개/오류 0개입니다.
- 측정부 최대 개수를 5개로 변경하고 기본 선 색상을 빨강, 주황, 노랑, 초록, 파랑으로 통일했습니다. 색상은 기존 `PartList_MeasurementPoints.line_color`에 계속 저장하며 App/Infrastructure/Vision이 같은 공통 정책을 사용합니다.
- Vision 입력 계약에 측정부 IndexNo, 항목, 색상, 기준값, 허용오차, X1/Y1/X2/Y2, 단위를 명시했습니다. 현재는 관리 코드에서 VLAD 엔진까지 전달되며 실제 네이티브 구조체 호출은 AI 담당자의 DLL 함수 계약 확인이 남았습니다.
- 단일품목 등록 상단 DB 조회 영역을 줄이고 우측에 `coordinate.png` 미리보기를 추가했습니다. Temp 좌표 이미지가 있으면 Temp를 우선 표시하고, DB 저장 후에는 최종 품번 폴더의 coordinate 이미지를 표시합니다.
- 다중품목 CSV를 측정부1~5 독립 행 구조로 변경했습니다. 각 측정부는 항목/기준/허용/색상/X1/Y1/X2/Y2를 가지며 단위는 mm로 고정합니다. 일부 좌표만 입력된 CSV 행은 오류로 처리합니다.
- 임시 SQLite DB에 측정부 5개를 저장하고 다시 읽어 기본색 `#E53935/#FB8C00/#FDD835/#43A047/#1E88E5`, 좌표, 기준값이 동일하게 복원되는 것을 확인했습니다.
- 전체 CSV 내보내기 결과는 기본 5개 품목 컬럼과 측정부별 8개 컬럼, 단위 1개를 합쳐 총 46개 헤더를 생성합니다. 생성한 CSV를 다시 불러와 측정부 5개의 항목/기준/허용/색상/좌표/단위가 동일하게 복원되는 것을 확인했습니다.
- `dotnet build "Tests\AI-Vision IO Inspector\AI.Vision.IOInspector.sln" -c Release -p:Platform=x64` 결과 경고 0개/오류 0개를 확인했습니다.

## 2026-06-25

- `InspectCapturedImages` 호출 경로를 다시 추적했습니다. 검사 버튼의 단일 요청이 `InspectionWorkflowService -> VisionAiInferenceService -> VisionInferenceWorker -> VladVisionInferenceEngine.Inspect -> InspectCapturedImages` 순서로 전달됩니다. 1초 실시간 화면 타이머는 `_cameraService.Capture`만 호출하며 `InspectCapturedImages`를 호출하지 않습니다.
- VLAD SDK의 `VLAD_Ops_RTSP_Frame_Proc`는 프레임 콜백마다 `VLAD_Inference_Mat`를 호출하는 별도 연속 추론 경로입니다. 검사 요청 기반 추론과 구분할 수 있도록 `InspectCapturedImages` 요청 Sequence 시작/완료 Debug 로그를 추가했습니다.
- 부품등록 하단 실제 이미지 미리보기는 6개 카메라 기준 이미지로 제한했습니다. 측정부 좌표 이미지는 상단 `coordinate.png` 전용 미리보기에서만 표시합니다.
- DB 조회/확인 상단에서 등록 기준 이미지 목록을 가운데로 옮기고 선택 부품 측정부 상세를 우측의 넓은 영역으로 이동했습니다.
- 검사 화면과 DB 상세의 측정부 결합 표시를 `측정부`와 `항목` 컬럼으로 분리했습니다. 검사 결과 이력처럼 이름만 남아 있는 데이터도 `측정부N - 항목` 형식을 파싱해 동일하게 표시합니다.
- 초기 기본 선택 품목 `01100-51430`의 `PartList_MeasurementPoints`에 측정부 4개가 존재함을 DB에서 확인했습니다. 부품 목록 재로딩 후 선택 객체가 같아도 `ApplySelectedPart()`를 명시적으로 호출해 기준 이미지와 MEASUREMENT를 다시 연결하도록 보강했습니다.
- 측정부가 있고 최종 `coordinate.png`가 존재하면 AI 입력용 Part 복사본에서 Thickness 이미지 경로를 coordinate 이미지로 대체합니다. 원본 Part와 DB 이미지 경로는 변경하지 않습니다.
- Release x64 전체 솔루션 빌드 결과 경고 0개/오류 0개입니다. 측정부 표시 분리 테스트와 원본 Thickness 보존/AI 입력 coordinate 대체 테스트도 통과했습니다.
- coordinate 경로 교체 요구를 다시 확인해 구현 위치를 수정했습니다. 이제 `RunInspection()`에서 조회한 `part.MeasurementRegions`가 1개 이상일 때 `part.Images` 중 `ViewType=Thickness`인 항목의 `FilePath`만 같은 폴더의 `coordinate.png`로 변경합니다.
- Vision 서비스는 전달받은 Part를 그대로 `VisionInspectionInput.Part`에 연결하며 별도 Part 복사나 Thickness 재교체를 수행하지 않습니다. 실제 `01100-51430` 데이터로 측정부가 없을 때 Thickness 유지, 측정부가 있을 때 coordinate 경로 교체를 검증했습니다.
- 좌표 이미지 저장명을 `{품번}_coordinate.png`로 변경했습니다. `01100-51430` 품목은 `01100-51430_coordinate.png`로 저장됩니다. 기존 `coordinate.png`는 읽기 호환만 유지하며 다음 좌표 이미지 저장 시 새 이름이 사용됩니다.
- 기준 이미지와 coordinate 교체 시 `_OldVer_` 백업을 생성하던 코드를 제거했습니다. 임시 파일을 현재 파일명에 직접 교체하며, 기존에 이미 생성된 OldVer 파일은 프로그램이 자동 삭제하지 않습니다.
- 임시 파일 시스템 검증에서 Top 이미지를 두 번 저장해도 OldVer가 생성되지 않고 현재 파일 내용이 교체되는 것과 `01100-51430_coordinate.png`가 생성되는 것을 확인했습니다.
- `DB\Image\Temp`의 임시 파일 삭제 후 빈 품번 폴더가 누적되는 문제를 수정했습니다. 앱 시작, 임시 작업 전체 삭제, coordinate 삭제, 임시 이미지 개별 삭제 경로에서 빈 품번 폴더와 빈 Temp 루트를 함께 정리합니다.
- 임시 루트 테스트에서 기존 빈 하위 폴더 자동 청소, 다른 품번 파일이 있을 때 Temp 유지, 마지막 임시 이미지/coordinate 삭제 후 Temp 제거를 확인했습니다. 실제 프로젝트 Temp의 빈 폴더도 7개에서 파일이 남은 1개로 정리했습니다.
- 부품등록 등록 기준 이미지 영역의 위치 선택 ComboBox를 제거하고 `삭제` 버튼을 `이미지 모두 삭제`로 변경했습니다.
- 이미지 전체 삭제 확인 시 기준 이미지 6장, `{품번}_coordinate.png`, 기존 `coordinate.png`, Temp 작업 파일을 삭제하고 해당 품번의 DB 이미지 행과 측정부 행도 즉시 비웁니다.
- 삭제 확인창은 메인 윈도우를 Owner로 사용하고 중복 표시 상태를 관리합니다. 빠르게 여러 번 요청해도 확인창을 추가 생성하지 않으며 메인 화면 앞으로 활성화합니다.
- 선택한 품목의 원래 품번과 편집 중인 입력 품번이 다르면 이미지 전체 삭제를 차단해 다른 품목의 파일이 잘못 삭제되지 않도록 했습니다.

## 2026-06-29

- 부품등록 단일품목의 등록 기준 이미지 영역에서 버튼을 오른쪽으로 옮기고 `현재 6개 저장`을 `이미지 저장`, `이미지 모두 삭제`를 `이미지 삭제`로 변경했습니다.
- 등록 기준 이미지의 두 버튼은 모두 파란색 `NavButton` 스타일로 통일했습니다. 버튼이 차지하던 상단 공간을 제거해 등록 이미지 Grid에서 No 1~6 행을 한 번에 볼 수 있도록 최소 높이를 확보했습니다.
- 모든 DataGrid 기본 스타일에 가로/세로 스크롤바 자동 표시를 추가하고, 기존 `MEASUREMENT` Grid의 가로 스크롤 비활성 설정도 자동 표시로 변경했습니다.
- 검사 완료 화면은 측정 이미지를 전체로 유지하되, 등록 기준 이미지를 좌측 상단 가로 1/3·세로 1/4 인셋 영역으로 축소했습니다.
- 옵션 화면의 카메라 설정/연결 상태 Grid는 CAM5 수준까지만 보이도록 높이를 제한하고, 하단에 `학습 바로시작`, `예약설정`, 예약시간 입력, `예약 적용`, 학습 상태 메시지를 추가했습니다.
- `IAiInferenceService.StartImageTraining()`과 `IVisionInferenceEngine.StartImageTraining()` 계약을 추가했습니다. 현재 구현은 `VisionAiInferenceService -> VisionInferenceWorker -> VladVisionInferenceEngine` 경로로 VLAD 런타임까지 이벤트를 전달하고, AI 담당자가 실제 DLL 학습 함수를 연결할 수 있는 지점을 `VladVisionInferenceEngine.StartImageTraining()`에 남겼습니다.
- 기준 이미지가 DB 저장으로 최종 등록/변경되거나 이미지 삭제로 DB 이미지가 제거되면 `ImageTrainingPromptWindow` 팝업을 띄워 `학습 지금 실행` 또는 `예약시간 설정`을 선택할 수 있게 했습니다. 예약시간이 되면 같은 `StartImageTraining()` 경로를 호출합니다.
- `dotnet build "Tests\AI-Vision IO Inspector\AI.Vision.IOInspector.sln" -c Release -p:Platform=x64` 결과 경고 0개/오류 0개를 확인했습니다.
- 부품등록 등록 기준 이미지의 `이미지 저장`/`이미지 삭제` 버튼을 측정부 버튼과 같은 가로열 배치로 정리했습니다. 저장은 파란색, 삭제는 붉은색 버튼으로 유지하고 측정부 `추가`/`삭제` 버튼도 같은 스타일 규칙을 적용했습니다.
- 옵션 화면의 Config.json 카메라 Grid 높이를 늘려 CAM0~CAM5가 스크롤 없이 보이도록 조정했습니다.
- 측정부 허용값을 단일 값에서 `Min`, `Max` 입력으로 분리했습니다. 저장 시 `ToleranceMin=-Min`, `ToleranceMax=+Max`로 관리하고, DB 조회와 SearchDB 측정부 표시는 `-Min ~ +Max` 형식으로 통일했습니다.
- SQLite `PartList_MeasurementPoints`에 `tolerance_min`, `tolerance_max` 컬럼을 추가하고 기존 `tolerance` 컬럼은 호환용으로 유지했습니다. 기존 DB는 스키마 초기화 시 새 컬럼을 추가하고 기존 허용값을 `-ABS(tolerance)`, `+ABS(tolerance)`로 채웁니다.
- 다중품목 CSV 내보내기는 `측정부N허용` 대신 `측정부NMin`, `측정부NMax`를 사용합니다. 기존 `측정부N허용` 파일도 불러올 수 있도록 호환 처리를 남겼습니다.
- DB 조회/확인 품목 Grid에 `구분` 컬럼을 추가하고, 선택 부품 측정부 상세 폭을 줄인 만큼 등록 기준 이미지 영역을 넓혔습니다.
- 원본 VLAD_Ops RTSP 흐름을 확인한 결과 검사 단위 Stop 호출은 없고 `VLAD_Rtsp_Info_Client_Registration` 후 콜백이 계속 호출되는 구조였습니다. 현재 프로젝트는 `VLAD_Ops_RTSP_Frame_Proc`에 Stop gate를 추가하고, 검사 시작 시 `StartFrameProcessing()`, 검사 완료 `finally`에서 `StopFrameProcessing()`을 호출하도록 변경했습니다.
- `dotnet build "Tests\AI-Vision IO Inspector\AI.Vision.IOInspector.sln" -c Debug -p:Platform=x64` 결과 경고 0개/오류 0개를 확인했습니다.
- 다중품목 CSV 불러오기에서 측정부 컬럼을 기존 `측정부N허용`, 신규 `측정부NMin/측정부NMax`, 통합 `측정부NMinMax` 형식으로 모두 읽을 수 있게 했습니다. `MinMax`는 `-0.5 ~ +0.7`, `0.5/0.7`, `0.5,0.7`, 단일 `0.5` 형식을 지원하며 단일 값은 하한/상한에 같은 허용값으로 적용합니다.
- `VladVisionInferenceEngine`에 누락된 `StartImageTraining()` 구현을 추가해 `IVisionInferenceEngine` 빌드 오류를 해결했습니다. 현재 VLAD DLL에는 학습 시작 전용 export가 확정되지 않았으므로, `VLAD_Ops_Ai.StartImageTraining()`은 VladId가 유효한지 확인하고 DLL 연결 지점을 로그로 남기는 경계 역할을 합니다.
- 검사 기준정보를 Vision/DLL 쪽으로 넘기기 위한 경계는 `InspectCapturedImages -> InspectMat -> VLAD_Ops_Ai.VLAD_Inference_Mat`가 맞습니다. 관리 객체를 네이티브 DLL에 직접 넘기지 않고 품번/품명/분류/촬영 ViewType/이미지 경로/측정부 IndexNo/항목/색상/기준값/MinMax/X1/Y1/X2/Y2/단위를 JSON 문자열로 구성해 래퍼까지 전달하도록 준비했습니다.
- 현재 `VladNativeMethods.VLAD_Inference_Mat(vladId, rawData, threshold, drawMode)`는 기존 VLAD DLL 4인자 export를 그대로 호출합니다. AI 담당자 DLL에서 기준정보 인자를 추가하면 `VLAD_Ops_Ai.VLAD_Inference_Mat(..., inspectionContextJson)` 내부에서 새 네이티브 export 호출로 교체하면 됩니다.
- `dotnet build "Tests\AI-Vision IO Inspector\AI.Vision.IOInspector.sln" -c Debug -p:Platform=x64` 결과 경고 0개/오류 0개를 확인했습니다.

## 2026-07-02

- 이미지 저장 경로 정책을 `Config.json` 기준으로 정리했습니다. 기준 이미지는 `IMAGE_PATH`, 검사 캡처/이력 이미지는 `OUTPUT_PATH`를 우선 사용하며, Config 키가 비어 있을 때만 프로젝트 내부 `DB\Image`, `DB\Inspection_Data`를 fallback으로 사용합니다.
- 현재 PC에 `H:` 드라이브가 없는 상태에서도 앱 시작 시 바로 죽지 않도록 기준 이미지 서비스 생성자에서 저장 루트 폴더를 즉시 생성하지 않게 했습니다. 실제 저장 시점에는 Config에 지정된 경로를 사용하므로 드라이브/폴더가 준비되어 있어야 합니다.
- `InspectMat Context JSON` 문서에 측정부 1~5개 전달 규칙을 추가했습니다. `measurements[]` 배열 안에 각 측정부의 `measurementRegionId`, `indexNo`, `itemType`, `lineColor`, 기준값, 허용값, `x1/y1/x2/y2`, 단위가 들어가며 AI는 이 값으로 측정부별 측정 위치와 반환 매핑을 구분합니다.
- HDD 여유공간 기준 자동삭제 단위를 기존 1개월에서 1일로 변경했습니다. 가장 오래된 검사일의 00:00부터 다음 날 00:00 이전까지를 삭제 후보 기간으로 계산합니다.
- 삭제 실행 순서는 대상 기간 검사 이미지/History/Log 폴더 삭제, 검사결과 DB 삭제, `OUTPUT_PATH\RetentionLog\yyyyMMdd.log` 삭제 이력 기록, 삭제 후 HDD 여유공간 재확인으로 정리했습니다. UI 메시지에는 삭제 후 여유공간이 함께 표시됩니다.

## 2026-07-03

- `CFG\VladRuntimeSettings.json`을 추가해 `VLAD_SDK.dll` 경로와 이미지 학습 `Study.bat` 경로를 별도 설정 파일로 분리했습니다.
- `VladRuntimeSettings`는 상대 경로를 프로젝트/실행 루트 기준으로 해석합니다. `DllImport`는 런타임 JSON 값을 직접 사용할 수 없으므로 `VLAD_SDK.dll` 이름을 유지하고, JSON의 DLL 파일 경로에서 폴더를 계산해 `SetDllDirectory`로 등록합니다.

## 2026-07-07

- AI 결과 수신 규격을 현재 시점 기준으로 정리했습니다. 결과 문자열은 `isMatched,score,measurement1,measurement2,...,measurementN` 형식이며, 예시 `true,98,100,159,25,47`은 `IsMatched=true`, score 98점, 측정부1~4 측정값 100/159/25/47mm로 해석합니다.
- 현재 코드 구조를 검토했습니다. `AiInferenceResult`와 `VisionInspectionOutput`은 `IsMatched`, `Confidence`, 측정값 목록을 담을 수 있으나, `VladInferenceResultParser`/`VladMeasurementMapper`는 아직 새 콤마 문자열을 구조화하지 않습니다.
- 현재 `MeasurementService`는 `MeasurementRegion.Id` 기준으로 측정값을 비교합니다. 새 결과 문자열은 측정부 순서 기반이므로, 구현 시 `MeasurementRegion.IndexNo` 오름차순 값을 해당 `MeasurementRegion.Id`로 변환하는 단계가 필요합니다.
- `ai-result-contract.md`, `inspectmat-context-json.md`, `camera-ai-integration.md`, `vision-implementation-checklist.md`, `open-items.md`에 AI 결과 문자열 계약과 남은 구현 항목을 반영했습니다.

## 2026-07-08

- AI 결과 문자열 parser를 구현했습니다. `true,98,100,159,25,47`은 `IsMatched=true`, 내부 `Confidence=0.98`, 측정부1~4 측정값 `100/159/25/47mm`로 해석합니다.
- AI 측정값은 `mm` 고정으로 판단합니다. 애플리케이션은 `cm/m/pixel` 단위 변환을 하지 않고, AI 측정값과 DB 기준값/허용오차만 비교합니다.
- `MeasurementUnitConverter`를 제거하고 `MeasurementService`를 단순화했습니다. 측정값이 없으면 `AI 측정값 없음`, 값이 범위 밖이면 `기준 범위 초과`로 처리합니다.
- `VLAD_Custom_InferenceData_V1` RTSP 콜백 경로에서 `detect_str`와 `tlv_info`를 채운 뒤 읽지 않고 해제하던 문제를 수정했습니다. 해제 전 `detectText`와 `Custom_Info_Struct[]`를 관리 메모리에 스냅샷으로 복사합니다.
- 통계 카드 높이를 68로 올려 숫자 잘림을 방지했고, `초기화` 버튼을 추가해 통계 화면 표시값과 OK/NG/Error 상세 그리드를 비울 수 있게 했습니다.
- 이력 CSV 저장 컬럼을 화면 Grid와 동일한 순서로 변경했습니다. CSV에만 있던 검사 이력 `ID`와 예전 측정부별 동적 컬럼은 제거했습니다.

## 2026-07-09

- `Docs/06-training/TrainingProcessMonitor.Wpf`를 분석해 외부 학습 프로그램의 StandardOutput, StandardError, Process.Exited 수신 기능을 메인 Vision 서비스에 이식했습니다.
- 옵션 탭 상단에 학습 바로시작, 1회 예약, 매일 예약을 배치하고 현재 상태/진행률/오류/시작·종료시간과 수신 정보 Grid, Grid 지우기를 추가했습니다.
- 학습 성공 조건을 `DONE` 수신, 종료 코드 0, `ERROR/CANCELED` 미수신으로 정의했습니다.
- 모델 파일 쓰기가 완전히 끝난 Process.Exited 이후에 이전 RTSP callback을 차단하고 `VLAD_Unregistration`, `VLAD_Ops_Ai_Env_Start`, 새 VladId 기준 RTSP 채널 재등록을 수행하도록 구현했습니다.
- 현재 `Tests/ToolsV2/ai_train.bat`가 MZ 헤더를 가진 실행 파일임을 확인해 PE 파일은 직접 실행하고 실제 배치 파일만 `cmd.exe /c`로 실행하도록 보완했습니다. 파일 메타데이터상 실제 원본은 `ExternalTraining.Sample.exe`이므로 현재 파일은 연동 검증용 샘플입니다.
- `dotnet build "Tests\AI-Vision IO Inspector\AI.Vision.IOInspector.sln" -c Debug -p:Platform=x64` 결과 경고 0개/오류 0개를 확인했습니다.
- `Local\AI.Vision.IOInspector.App.SingleInstance` Mutex를 추가해 같은 사용자 세션에서 프로그램 중복 실행을 차단했습니다. 두 번째 프로세스는 VLAD/SQLite 초기화 전에 안내 후 종료합니다.

## 2026-07-13

- `VladVisionSettings.Load()`가 개발용 솔루션 루트의 `CFG\Config.json`을 우선 읽어, 실행 폴더 `bin\x64\Debug\net472\CFG\Config.json`에서 변경한 `GPU_ID`가 실제 VLAD 초기화에 반영되지 않던 문제를 수정했습니다. 이제 실행 폴더 설정 파일이 존재하면 이를 우선 사용하고, 없을 때만 개발 루트 설정으로 fallback합니다.
- 실행 검증에서 `CONFIG_LOADED` 로그가 실행 폴더의 `Config.json`과 `GPU_ID=1`을 기록했고, 이어진 `ENV_START_ENTER` 및 `VLAD_Custom_Registration`도 `GpuId=1`로 성공했습니다. Debug x64 전체 빌드는 경고 0개, 오류 0개입니다.
- `ptxas.exe`는 GPU 번호 설정과 무관하게 CUDA GPU 추론이 실제 실행될 때 VLAD SDK가 로드한 TensorFlow/CUDA 런타임에서 시작됩니다. 현재 C# 코드에는 `ptxas.exe` 직접 실행 코드가 없고, RTSP callback의 프레임별 추론 gate도 호출부가 없어 기본 비활성 상태입니다. 반복 실행은 CUDA 컴파일 캐시/네이티브 DLL 동작을 AI/VLAD 담당자가 확인해야 하며, 응용프로그램에서 창을 강제로 숨기거나 프로세스를 종료하지 않습니다.

- 검사 시작을 실제 실행 파일에서 재현해 검정 콘솔 창의 원인을 확인했습니다. `VLAD_Inference_Mat` 처리 중 TensorFlow/VLAD가 CUDA 11.0의 `ptxas.exe`를 반복 실행하고, WPF 프로세스에 콘솔이 없어 각 `ptxas.exe`가 `conhost.exe`를 새로 만들어 창이 반복 표시됐습니다. 샘플과 현재 C# 코드 모두 `ptxas.exe`/`conhost.exe`를 직접 실행하지 않으므로, 이 동작은 VLAD DLL이 로드한 TensorFlow/CUDA 런타임 영역으로 분류합니다. 화면 숨김 우회책은 제거했으며, AI/VLAD 담당자가 CUDA 컴파일 캐시 및 DLL 내부 프로세스 생성 정책을 확인해야 합니다.
- 전체 솔루션 `Debug | x64` 빌드 중 `VisionWorker\Program.cs` 167행에 남아 있던 잘못된 `#if` 문자를 제거했습니다. 전체 빌드 결과는 경고 0개, 오류 0개입니다.
- Vision 담당자 교육용 흐름 자료를 추가했습니다. 프로그램 시작 Initial, `VladId` 재사용, RTSP callback 최신 프레임 캡처, Search DB 선택, 검사/측정/판정/SQLite 이력 저장 경로를 코드 기준으로 정리했습니다.
- 현재 VLAD DLL의 `VLAD_Inference_Mat` 실제 운영 호출은 4개 인자이며, C# `inspectionContextJson`은 준비/로그 단계만 구현되어 있다는 사실을 명시했습니다. 좌표와 기준값 JSON을 AI 입력에 쓰려면 담당자가 지원 DLL export 또는 별도 API 계약을 제공해야 합니다.
- Vision 담당자 잔건을 모델 구조, 결과 계약, 6대 RTSP, 성능, native memory, GPU/CUDA 배포, 학습 재초기화로 구분해 완료 기준과 함께 기록했습니다.
- Vision 담당자 교육 자료에 인쇄용 CSS 페이지 나눔을 추가했습니다. A4 가로 방향에서 인쇄 대화상자의 `페이지당 2페이지` 설정을 사용하면 설명 단위 페이지를 한 장에 2페이지로 출력할 수 있습니다.

## 2026-07-15

- 프로그램 시작 시 옵션 화면의 카메라 상태가 설정값만 표시되던 흐름을 수정했습니다. 창이 표시된 뒤 백그라운드에서 사용 카메라별 실제 영상 수신을 확인하고, 연결 상태·메시지·확인시각을 옵션 카메라 목록에 반영합니다.
- 개별 카메라의 연결 확인이 실패해도 전체 상태 갱신이 중단되지 않도록 채널별 예외를 처리했습니다. 실패한 채널에는 실제 확인 시각과 실패 원인을 표시하고, 나머지 카메라는 계속 확인합니다.
- 종료 이후 백그라운드 상태 확인 결과가 UI에 반영되지 않도록 ViewModel 종료 상태를 확인합니다.
- 검증: .NET Framework 4.7.2 / x64 Debug 전체 솔루션을 분리 출력 경로에서 빌드했으며 경고 0개, 오류 0개입니다. 일반 Debug 출력 경로는 실행 중인 Visual Studio 프로세스가 잠그고 있어 덮어쓰지 않았습니다.

## 2026-07-15

- 기준이미지 두 번 클릭 팝업을 추가하고, 팝업이 하나만 열리며 Search DB 선택 부품 변경 시 이미지가 갱신되도록 했습니다.
- 측정부가 있고 coordinate 이미지가 있으면 검사 화면의 Thickness 기준이미지로 표시하도록 연결했습니다.
- `CFG\Config.json`의 활성 `CUSTOM.HD` 영역에 검사 Pass/Fail Score 기본 95와 단일품목 유사값 기본 99를 보존 저장하도록 했습니다.
- 실제 문자열 결과 `true/false,score,measurement1...N`의 Score를 최종 판정에 반영하고 검사 완료 화면에 표시했습니다.
- W/H/D 표시 UI를 추가했으며, 현재 SDK 결과에 치수 값이 없어 `-`로 표시합니다.
- 전용 `VLAD_Search_Mat`/`VLAD_Search_Data` export가 확인되지 않아 유사도 UI는 준비하되 일반 검사 함수로 대체 호출하지 않도록 했습니다.
- 검증: .NET Framework 4.7.2 / x64 Debug 전체 솔루션을 분리 출력 경로에서 빌드합니다.

## 2026-07-16

- 기준 이미지 확대 팝업을 닫은 뒤 바로 두 번 클릭했을 때, WPF `Closing`~`Closed` 구간에서 아직 보이는 닫힘 중 창을 재사용하던 문제를 수정했습니다. `Closing` 시점에 참조를 먼저 해제해 즉시 새 창을 생성하며, 닫힌 인스턴스 재표시 예외에도 새 창을 생성합니다. 닫힘 후에는 소유자 메인 창을 다시 활성화해 첫 입력이 창 활성화에만 소비되지 않게 했습니다.
- 옵션의 `학습` 탭 이름을 `검사 설정/학습`으로 변경했습니다.
- 검증: .NET Framework 4.7.2 / x64 Debug 전체 솔루션 빌드 결과 경고 0개, 오류 0개입니다. 검증용 분리 출력 폴더는 확인 후 삭제했습니다.
- 검증: 실제 STA/WPF 수명주기에서 메인 창 소유 팝업을 열고, `Closing` 이벤트 중 즉시 재열기 요청을 실행했습니다. 결과로 가시 팝업 1개를 확인했습니다. 일반 `bin\x64\Debug\net472` 출력에도 전체 솔루션을 빌드했습니다.
- 검사 UI 좌측 상단 기준 이미지는 저장된 `ReferenceImagePath` PNG를 `RtspVideoHost` 내부 STATIC 오버레이에 표시하는 구조임을 확인했습니다. 이 네이티브 HWND의 마우스 입력은 상위 Border의 WPF `MouseBinding`에 안정적으로 전달되지 않습니다.
- 네이티브 STATIC 컨트롤의 `STN_DBLCLK`를 `RtspVideoHost.VideoDoubleClick` 이벤트로 변환하고, MainWindow가 기존 `ShowReferenceImagePopupCommand`를 호출하도록 연결했습니다. VLAD 추론, RTSP 연결 상태, 카메라 프레임 수신과 무관한 UI 입력 경로 수정입니다.
- 검증: `DB\\Image\\T99\\99999\\99999_Top.png`를 실제 네이티브 오버레이에 표시한 STA/WPF 테스트에서 오버레이 가시 상태, 더블클릭 이벤트 2회, 첫/두 번째 팝업 가시 상태 각 1개를 확인했습니다. Visual Studio 디버그 실행 중인 앱이 일반 `bin` DLL을 잠그고 있어, 수정본은 분리 빌드 출력으로 검증했습니다.
- 2026-07-16 - 실행 설정 JSON은 모두 EXE와 같은 `CFG` 폴더만 사용하도록 정리했습니다. 개발 루트의 `.sln` 탐색 및 상위 `CFG` fallback을 제거했고, `Config.json`, `Calibration.json`, `InspectionDataRetention.json`, `VladRuntimeSettings.json`과 이들 설정의 상대 경로는 배포 EXE 폴더를 기준으로 해석합니다. Debug x64 분리 출력 빌드는 경고 0개, 오류 0개로 완료했습니다.
- 2026-07-16 - 검사 설정의 Pass/Fail Score와 단일품목 유사도 Score를 소수점 둘째 자리까지 반올림하고 UI는 항상 `0.00` 형식으로 표시하도록 통일했습니다. Slider는 소수점 입력을 허용하면서 10점 단위 눈금/숫자 레이블을 제공하도록 구성했고, 결과 슬롯의 AI Score와 판정 기준도 같은 형식으로 표시합니다. Debug x64 분리 출력 빌드는 경고 0개, 오류 0개로 완료했습니다.

## 2026-07-20

- `VLAD_HD_InferenceData_Result`의 결과 JSON 파서를 최신 계약으로 갱신했습니다. `imageJudge`, `measurementJudge`, `overallJudge`, `score`, `scoreThreshold`, `failureReasons`와 측정부별 `measurementRegionId/indexNo/itemType/measuredValue/specValue/toleranceMin/toleranceMax/judge/unit`을 `VladInferenceResult`에 보존합니다. 기존 검사 흐름의 `DetectText`는 `overallJudge`, score, 측정값을 기존 형식으로 변환해 유지합니다. 테스트 JSON PASS 응답과 failureReasons가 있는 FAIL 응답을 각각 파싱해 확인했습니다.

- VLAD 초기화/추론 시 일시적으로 나타나는 콘솔 창을 점검했다. C# 코드에서 `cmd.exe`를 직접 실행하는 지점은 `Study.bat` 학습 실행뿐이고, 검사 캡처는 RTSP callback 최신 프레임 저장 경로라 `ffmpeg.exe` 재접속을 사용하지 않는다.
- `VLAD_Custom_Registration` 실행 검증 중 `conhost.exe` 생성이 확인됐으며, `ptxas.exe`는 VLAD DLL 내부 TensorFlow/CUDA PTX 컴파일 실행으로 분류했다. `VladRuntimeSettings`에 `CudaCacheDirectoryPath`를 추가해 등록 전에 `CUDA_CACHE_PATH=%LOCALAPPDATA%\AI-Vision IO Inspector\CudaCache`를 적용하도록 수정했고 실제 폴더 생성 가능 여부를 확인했다.
- 검증: `AI.Vision.IOInspector.Vision` x64 Debug 빌드 성공(경고 0, 오류 0). 전체 솔루션 빌드는 Visual Studio에서 실행 중인 `AI.Vision.IOInspector.App`이 출력 DLL을 점유해 보류됐다.

- 전체 이미지와 Crop 이미지 학습/관리를 위해 `FullImageVladId`와 `CroppedImageVladId`를 프로그램 시작 시 함께 생성하고, 학습 후 재초기화와 앱 종료 시 함께 해제하도록 변경했습니다. 전체 이미지 ID만 RTSP callback을 등록하고 Crop ID는 중복 RTSP 등록을 하지 않습니다. 현재 `VLAD_Ops_RTSP`가 활성 ID 하나의 프레임 캐시만 관리하기 때문입니다.
- 검사, 구버전 결과 파싱, 유사도 검색, 학습 시작 경로가 두 ID를 모두 받도록 C# 구조를 변경했습니다. 외부 학습 프로세스에는 프로세스 내부 포인터인 `IntPtr`를 전달하지 않고 두 ID 준비 상태만 확인합니다.
- `CFG\Config.json`의 선택 항목 `CROP_MODEL`을 추가해 Crop 모델 경로를 별도로 지정할 수 있게 했으며, 값이 없으면 기존 `MODEL` 경로를 두 ID가 함께 사용합니다.
- 현재 배포 `VLAD_SDK.dll`은 단일 ID export만 확인됐으므로 실제 legacy native 호출은 전체 이미지 ID로 유지됩니다. 새 HD DLL이 두 ID ABI를 제공하기 전 Crop ID가 실제 native inference에 사용된다고 판단하지 않습니다.
- 전체/Crop 두 ID runtime 및 HD JSON ABI 계약 문서를 추가·갱신했습니다. .NET Framework 4.7.2 / x64 Debug 전체 솔루션을 분리 출력 경로에서 빌드해 경고 0개, 오류 0개를 확인했습니다.
- Vision 문서의 단일 ID 표기를 다시 점검했습니다. 목표 API는 `VLAD_HD_Inference_Mat(fullImageVladId, croppedImageVladId, rawData, threshold, drawMode, inspectionContextJson)`로 통일하고, `VLAD_Inference_Mat(vladId, rawData, threshold, drawMode)`는 현재 배포 DLL의 레거시 호환 호출이라고 명시했습니다.
- `VladNativeMethods`에 두 ID/UTF-8 JSON 기반 `VLAD_Search_Mat`, `VLAD_Search_Data` P/Invoke overload를 추가했습니다. 기존 단일 ID export 선언은 유지했습니다. `VLAD_HD_Inference_Mat`은 새 export 존재 시 두 ID와 Context JSON을 전달하고, 없으면 `EntryPointNotFoundException`을 한 번 기록한 뒤 기존 4인자 호출로 fallback합니다. 새 HD 결과는 `VLAD_HD_InferenceData_Result` JSON을 읽어 기존 `true/false,score,measurement...` 처리 형식으로 변환합니다.
- 시작 실패를 실제 x64 Debug 실행 폴더에서 재현했습니다. `GPU_ID=1`에서는 `VLAD_Custom_Registration` 반환 전 `0xc0000374`(ntdll native heap corruption)로 프로세스가 종료됐고, `nvidia-smi` 기준 이 PC의 CUDA GPU는 RTX 4060 Laptop GPU index `0` 한 대입니다. `CFG\\Config.json`을 `GPU_ID=0`으로 변경한 검증에서는 등록 호출이 약 11.7초 후 정상 반환하고 RTSP 등록과 메인 창 생성까지 완료했습니다.
- 현재 배포 `VLAD_SDK.dll`은 첫 번째 Custom Registration 뒤 두 번째 등록을 요청하면 네이티브 힙이 손상되는 것을 로그로 확인했습니다. `VladSdkSession`은 기본 호환 모드에서 한 번 생성한 VladId를 전체 이미지/Crop 두 입력 슬롯에 함께 전달하며, `VLAD_Warm_Up`과 `VLAD_Unregistration`도 한 번만 실행합니다. 두 ID를 받는 새 HD API의 C# 호출 구조는 유지합니다.
- AI 담당자가 별도 등록 가능 DLL을 제공한 뒤에만 `CFG\\VladRuntimeSettings.json`의 `UseSeparateVladRegistration`을 `true`로 변경합니다. 현재 기본값 `false`에서는 시작 안정성을 우선합니다.
- 정상 등록 확인 뒤 테스트 프로세스를 강제 종료한 후에는 다음 등록 시도와 제공 `Sample_VLAD_SDK`에서도 동일한 `0xc0000374`가 재현됐습니다. 이 상태에서는 `VLAD_Custom_Registration` 반환 전에 네이티브 프로세스가 종료되어 관리 코드에서 복구할 수 없습니다. 일반 사용에서는 앱의 닫기 동작으로 `VisionRuntimeFactory.ShutdownVladRuntime()`과 `VLAD_Unregistration`이 실행되도록 유지하며, 이미 이 상태가 발생한 PC는 Windows 재시작 후 `GPU_ID=0` 설정으로 재검증해야 합니다.
- `UseTestResultJson` 테스트 모드를 추가했습니다. 실행 EXE의 `CFG\\VladRuntimeSettings.json`에서만 명시적으로 `true`를 설정할 수 있고 기본값은 `false`입니다. true일 때는 네이티브 등록과 추론을 호출하지 않고, `TEST_VLAD_HD_InferenceData_Result`가 InspectionResult JSON을, `TEST_VLAD_Search_Data`가 후보 JSON을 반환해 결과 파싱 이후의 검사·측정값·이력·유사도 UI 흐름을 검증합니다.
- 테스트 함수는 64KiB UTF-8 byte buffer 계약과 널 종료 문자를 동일하게 적용합니다. 직접 검증 결과 검사 JSON은 `requiredResultJsonBytes=887`, `true,97.23,150.00,60.00,290.00,10.00`, 측정값 4개로 변환됐고, 검색 JSON은 `requiredResultJsonBytes=200`, 후보 2개로 파싱됐습니다. 구버전 단일 ID 검색 결과는 테스트 모드가 아닐 때 `VladNativeMethods.VLAD_Search_Data`를 호출하도록 바로잡았습니다.

## 2026-07-24

- `Tests/Backup/AI-Vision IO Inspector-2026-07-13`, `Tests/Backup/AI-Vision IO Inspector-2026-07-20`, 현재 `Tests/AI-Vision IO Inspector`를 파일 및 기능 단위로 비교했습니다.
- 충돌이 없는 백업 변경을 반영하고, `MainWindow.xaml`, `MainWindowViewModel.cs`, `AppBootstrapper.cs`, `RtspVideoHost.cs`, Vision RTSP 수명주기, 실행 Config는 현재 OCR·고해상도·두 VladId 구조를 보존하는 방식으로 병합했습니다.
- 기준 이미지 확대 창과 닫은 직후 재호출 처리, coordinate Thickness 표시, 부품 등록 이미지 유사도 검색, 0~100 검사 Score 설정을 현재 프로젝트에 반영했습니다.
- RTSP callback에서 최신 프레임 캐시와 무관하게 네이티브 추론을 반복하던 코드는 적용하지 않았습니다. 추론은 검사 요청의 명시적 진입점에서 실행합니다.
- 결과 파일 하단 W/D/H와 측정부 측정값 표시는 화면 자리만 존재하고 실제 PNG 합성 코드가 백업에도 없음을 확인했습니다.
- `VLAD_HD_ImageMerge`는 제공 예정 DLL API이므로 현재 가상 구현하지 않고, DLL export와 파일 규칙 확정 후 연결할 잔여 항목으로 남겼습니다.
- 고장 카메라 처리는 `CAM_ENABLED=false` 채널 제외까지만 구현되어 있습니다. 활성 채널의 실행 중 실패를 무음 Skip하면 PASS 오판 가능성이 있어 운영 판정 정책 확인 항목으로 남겼습니다.
- `Directory.Build.props`의 제품/어셈블리/파일 버전을 `1.1.0.0`으로 통일했습니다.
- `.NET Framework 4.7.2`, x64 Debug 전체 솔루션 빌드 결과 경고 0개, 오류 0개를 확인했습니다.

- 소스 버전 관리 경로를 `Codes/Version1_0_0_0`과 `Codes/Version1_1_0_0`으로 분리했습니다.
- 최신 1.1 프로젝트는 `Codes/Version1_1_0_0/AI-Vision IO Inspector`에 복사하고, 2026-07-20 기준 1.0 프로젝트는 `Codes/Version1_0_0_0/Backup/AI-Vision IO Inspector`에 복사했습니다.
- 원본 `Tests`는 사용자가 직접 확인하고 정리할 수 있도록 삭제하지 않고 완전한 상태로 복구해 보존했습니다.
- 1.0의 공통 수정은 1.1로만 선별 반영하고, 1.1 전용 기능은 1.0으로 역반영하지 않는 규칙을 `version-management-2026-07-24.md`에 기록했습니다.
- 두 버전의 x64 Debug 전체 솔루션 빌드는 각각 경고 0개, 오류 0개로 통과했습니다.
- 복사 작업 중 새로 생성된 `AI-Vision IO Inspector_2026-07-24a-Ver1_1_0_0.zip`은 증분 복사 승인 거절에 따라 원본 `Tests`에만 남겼습니다.
- 병합 상세와 기능별 완료 상태는 `version-1.1.0.0-merge-2026-07-24.md`에 기록했습니다.
- 부품등록의 유사도 결과 표시를 공통 하단 Grid에서 각 등록 기준 이미지 하단으로 이동했습니다.
- 각 Top~Thickness 미리보기 모델이 독립적인 후보 목록을 보유하며, 설정된 유사도 기준점수 이상 결과만 점수 내림차순으로 정렬해 최대 3개를 표시합니다.
- API가 반환한 순위는 화면 표시 순서와 다를 수 있으므로 필터·정렬 후 1~3위를 다시 부여합니다.
- 이미지가 없거나 기준점수 이상 후보가 없는 방향은 각각 `이미지 없음`, `기준 00.00 이상 후보 없음`으로 표시합니다.
- `.NET Framework 4.7.2`, x64 Debug 전체 솔루션 빌드 결과 경고 0개, 오류 0개를 확인했습니다.

## 2026-08-03

- 2026-07-27 및 2026-08-03 `leekh` 회신의 VLAD 파라미터 최소화, Thickness 분리, JSON 메모리 관리 의견을 현재 HD API 계약과 대조했습니다.
- 담당자 동의 전 코드와 기존 확정 계약은 변경하지 않고, `VLAD_HD_Inference_Mat`, `VLAD_HD_InferenceData_Result`, `VLAD_Search_Mat`, `VLAD_Search_Data`의 1.1 파라미터 정리 제안서를 작성했습니다.
- 일반 5개 View와 Thickness 입력을 분리하고, Request/Result의 품명을 `partName`으로 통일하며, 설정 Score와 측정부 기준 판정을 AI가 담당하는 방향을 제안했습니다.
- 호출자 소유 UTF-8 버퍼, 결과 용량 및 필요 byte 수 규칙을 명시하고 AI 담당자에게 발송할 검토 요청 메일 초안을 작성했습니다.
- View별 최종 판정은 제품 전체 판정과 혼동되지 않도록 `viewJudge`로 명명하고, 제품 검사 전체 판정이 별도로 필요하면 `inspectionJudge`를 사용하도록 제안서와 메일 초안을 수정했습니다.
- 결과 이미지 하단 표시용 W/D/H를 위해 `dimensions`를 기본 결과에 유지하고, 유사도 후보는 기준 이상 최대 3개로 확정했으며, 최초 화면 지연은 Registration부터 첫 RTSP 프레임과 첫 추론까지 구간별로 측정하도록 문서를 보완했습니다.
- 메일 첨부용 API 문서는 변경 사양 1~7장만 남기고, C# 적용 계획·AI 담당자 확인 질문·승인 조건은 첨부 문서에서 제거해 메일 본문으로 분리했습니다.

## 2026-08-04

- 메일 첨부용 `VLAD HD API 파라미터 변경 사양`을 두 Vlad ID, `rawData`의 `cv::Mat*` 이미지 전달, 일반 View와 Thickness 입력 분리, 측정부 필드 정의까지 상세화했습니다.
- 검사 결과의 `viewJudge`, W/D/H `dimensions`, 오류 JSON과 UTF-8 결과 버퍼 부족 처리 규칙을 명시했습니다.
- 유사도 검색은 `VLAD_Search_Mat`에 이미지 Mat과 기준 Score를 전달하고, `VLAD_Search_Data`에서 기준 이상 후보를 최대 3개만 반환하도록 계약을 정리했습니다.
- 담당자 동의 전이라도 DLL 교체 직후 검증할 수 있도록 프로그램의 관리 코드 준비를 먼저 진행했습니다.
- `VLAD_HD_Inference_Mat` 요청 JSON을 schema 1.1 최소 항목으로 분리했습니다. 일반 View는 `inspectionId/partNo/partName/viewName/scoreThreshold`만 보내고, Thickness만 최대 5개의 `measurementPoints`를 추가합니다.
- `VLAD_HD_InferenceData_Result` 결과의 `status/viewJudge/dimensions/measurements/failureReasons`를 파싱하고, 호출자 소유 UTF-8 버퍼가 부족하면 DLL이 반환한 필요 byte 수로 한 번 재할당 후 재호출하도록 보강했습니다.
- 신규 결과에서는 AI가 반환한 View 판정과 측정부별 `judge`를 최종 판정에 우선 사용합니다. C#에서 Score 또는 허용오차를 다시 판단하지 않으며, 구형 DLL 결과에만 기존 로컬 비교를 fallback으로 유지합니다.
- 유사도 검색 요청은 `viewName/scoreThreshold/topK=3`만 전달하며, 반환 후보는 AI가 정한 순위와 기준 통과 결과를 그대로 최대 3개 표시하도록 C#의 재필터·재정렬을 제거했습니다.
- `dimensions` W/D/H는 Vision 출력, Application 결과, 현재 검사 객체와 검사 화면까지 전달하도록 연결했습니다. 기존 이력 DB 스키마는 이번 준비 단계에서 변경하지 않았습니다.
- .NET Framework 4.7.2 / x64 Debug 전체 솔루션 빌드 결과 경고 0개, 오류 0개입니다. schema 1.1 JSON 파싱과 AI 판정 우선 적용 단위 검증도 통과했습니다.

## 2026-08-05

- `VLAD_HD_Inference_Mat수정-2026-08-05.md`의 최종 계약을 Version 1.1 코드에 적용했습니다.
- 신규 검사/유사도 Mat 호출은 두 VladId, `rawData`, `drawMode`, 고정 UTF-8 요청 JSON만 사용하며 기존 네이티브 `threshold` 인자를 전달하지 않습니다. 판정 기준은 JSON의 `scoreThreshold` 하나로 통일했습니다.
- 검사 요청은 `partNo`, 숫자 `viewName`, `scoreThreshold`, 0으로 채운 `dimensions`, `measurementPoints`만 전달합니다. Thickness 측정부는 최대 5개이며 화면/DB 순서대로 `indexNo=1~5`를 다시 부여합니다.
- 검사 결과는 `partNo`, 숫자 `viewName/viewJudge`, Score, W/D/H, 측정값만 파싱합니다. `viewJudge=0`은 PASS, `1`은 FAIL로 기존 화면/이력 모델에 연결합니다.
- 유사도 요청/결과는 숫자 View, Score 기준, `topK=3`, 후보 품번/Score만 사용합니다. 결과 JSON에서 제외된 품명은 DataStore의 품번 조회로 화면에 채웁니다.
- 유사도 검색은 이번 계약으로 추가된 신규 `VLAD_Search_Mat`와 `VLAD_Search_ResultData`만 직접 호출하며 구형 검색 API 분기나 대체 호출은 사용하지 않습니다.
- C#이 검사/유사도 요청 및 결과용 8192-byte 버퍼를 할당하고 전체를 0으로 초기화합니다. DLL 호출 직후 관리 문자열로 복사한 다음 `finally`에서 해제하며, `detectData/searchData`는 SDK 소유 포인터로 유지합니다.
- .NET Framework 4.7.2/x64 Debug 전체 솔루션 빌드 결과 경고 0개, 오류 0개입니다. 652-byte Thickness 요청을 8192-byte 버퍼에 기록해 널 종료와 잔여 0 초기화를 확인했고, 측정부 5개 제한/IndexNo 정규화/검사 결과/유사도 후보 파싱 smoke test를 통과했습니다.
- 실제 네이티브 호출 검증은 새 계약의 export가 포함된 `VLAD_SDK.dll`로 교체한 뒤 수행해야 합니다. 현재 검증은 DLL 호출 전후 C# 계약과 테스트 JSON 파서 범위입니다.
- `VLAD_HD_ImageMerge(char* inputPath, char* keyId, char* outputPath)` P/Invoke와 Application 서비스 경계를 추가했습니다.
- 기준이미지는 DB 저장 성공 후 Top/Front/Back/Left/Right/Thickness 6장을 병합합니다. SDK 결과는 임시 출력 폴더에서 확인한 뒤 최종 기준 폴더의 `품번.확장자`를 덮어써 이전 병합 파일이 누적되지 않게 했습니다.
- 검사 완료 시에는 같은 시간/품번 폴더에 있던 이전 검사 이미지가 섞이지 않도록 현재 검사 6장만 임시 입력 폴더에 복사한 뒤 한 번 병합합니다. 임시 입력/출력 폴더는 호출 직후 정리합니다.
- 기준 이미지 전체 삭제 시 품번 이름의 병합 이미지도 함께 삭제합니다. 검사 병합 이미지는 기존 `OUTPUT_PATH/연/월/일/시` 폴더 안에 생성되므로 일 단위 검사 데이터 삭제 시 원본 검사 이미지와 함께 제거됩니다.
- 병합 실패는 기준정보 DB 저장이나 검사 결과/이력 저장을 중단하지 않고 등록 메시지 또는 검사 이벤트에 경고로 남깁니다. 현재 배포 `VLAD_SDK.dll`에는 해당 export가 없어 실제 이미지 생성은 새 DLL 적용 후 검증해야 합니다.
