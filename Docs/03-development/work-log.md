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
- `Tests\AI-Vision IO Inspector\Native\README.md`, `Tests\AI-Vision IO Inspector\AI.Vision.IOInspector.Vision\Docs\native-deployment.md`, `scripts\publish-win-x64.ps1`을 추가해 네이티브 DLL 배포 위치와 self-contained win-x64 publish 기준을 문서화했습니다. 대용량 벤더 DLL은 GitHub 일반 커밋 대상에서 제외하도록 `.gitignore`에 Native 바이너리 제외 규칙을 추가했습니다.
- 실행 중인 `AI.Vision.IOInspector.App` 프로세스가 출력 DLL을 잠그고 있어 종료 후 재빌드했습니다. WPF 증분 빌드 산출물(`App.g.cs`, `MainWindow.g.cs`)이 일시적으로 누락되어 `dotnet build -t:Rebuild`로 생성 파일을 다시 만들었고, 최종 빌드는 경고 0개/오류 0개로 통과했습니다. `scripts\publish-win-x64.ps1`로 `publish\win-x64-test` self-contained 배포 폴더 생성을 확인했습니다. 생성된 앱 EXE는 x64이며, .NET 런타임 DLL과 `DB\DataBase.db`, `Native` 폴더가 함께 배포됩니다.
- AI/카메라 담당자 전용 구현 영역으로 `AI.Vision.IOInspector.Vision` 프로젝트를 솔루션에 추가했습니다. App은 `VisionRuntimeFactory`를 통해 `ICameraService`, `IAiInferenceService` 구현체를 받도록 연결했고, 현재 검사 시뮬레이션은 `SimulatedVisionInferenceEngine`으로 이전했습니다. AI 엔진이 측정값 단위와 raw pixel 값을 반환할 수 있도록 `AiInferenceResult`를 확장하고, `MeasurementService`는 `mm`, `cm`, `m` 단위 변환 후 기준값과 비교하도록 보완했습니다. `dotnet build -t:Rebuild` 결과 경고 0개/오류 0개를 확인했고, `dotnet publish -c Release -r win-x64 --self-contained true`도 새 Vision 프로젝트를 포함해 정상 완료했습니다.
- Vision 프로젝트에 실행 뼈대를 보강했습니다. `VisionInferenceWorker` 전용 background thread로 AI 추론을 분리하고, `VisionCameraCoordinator`를 카메라 중심 조율 클래스로 추가했습니다. 기존 VLAD/IMV 담당자가 대응하기 쉽도록 `LegacyVlad`와 `ImvCamera` 폴더에 `VLAD_Registration`, `VLAD_Inference_Mat`, `OpenDevice`, `StartGrabbing`, `GetFrame`, `ReleaseFrame`, `StopGrabbing` 흐름과 대응되는 Adapter 뼈대를 추가했습니다. 실제 SDK 호출은 아직 구현하지 않았고, 누락 방지를 위해 `vision-project-boundary.md`에 대응표와 현재 미구현 범위를 기록했습니다.
- 기존 함수명을 그대로 검색할 수 있도록 `VladFunctionAdapter`, `ImvFunctionAdapter` facade를 추가했습니다. 또한 `vlad-imv-conversion-guide.md`를 작성해 기존 `VLAD_Ops_Ai.cs`, `Camera_Control.cs`, IMV 샘플 코드의 함수가 현재 Vision 프로젝트의 어떤 클래스/메소드로 이동해야 하는지 변환 순서와 미구현 항목까지 정리했습니다.
- 비전 영역을 한국 담당자가 바로 읽을 수 있도록 `AI.Vision.IOInspector.Vision` 프로젝트의 XML/인라인 주석과 README 설명을 한국어 중심으로 정리했습니다. 기존 VLAD/IMV 함수명, SDK명, 클래스명은 검색성과 담당자 대응을 위해 그대로 유지했습니다.
- Vision 담당자가 여러 상위 md 파일을 모두 열 필요가 없도록 `camera-ai-integration.md`, `vision-project-boundary.md`, `vlad-imv-conversion-guide.md`, `native-deployment.md`를 `AI.Vision.IOInspector.Vision\Docs` 아래로 이동하고, 읽는 순서를 `Docs\README.md`에 정리했습니다.
- VSLD/VLAD 코드의 카메라별 Thread 구조를 현재 프로젝트에 맞춰 `VisionCameraCaptureWorker`, `VisionCameraCaptureRequest`, `IVisionCameraCaptureExecutor`로 보강했습니다. `VisionCameraCoordinator`는 Top/Front/Back/Left/Right/Thickness Worker를 생성하고 `CaptureAll` 요청을 분배합니다. 실제 SDK가 없으므로 현재 촬영 실행은 기존 `ConfiguredCameraService`를 사용하며, 빌드 경고 0개/오류 0개를 확인했습니다.
- Vision 담당자가 빠뜨리기 쉬운 실제 SDK 연결, RTSP/NVR 정책, 트리거, pixel-to-mm 보정, VLAD 결과 파싱, 이벤트 이미지 보관 정책을 `AI.Vision.IOInspector.Vision\Docs\vision-implementation-checklist.md`에 정리했습니다.
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
