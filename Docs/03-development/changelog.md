# 변경 이력

프로젝트 문서와 구현의 의미 있는 변경을 기록한다.

| 날짜 | 구분 | 내용 | 관련 문서/작업 |
| --- | --- | --- | --- |
| 2026-05-29 | 문서 | 요구사항 명세서와 업무파악 정리파일을 분석해 프로젝트 문서 구조를 재작성 | `00-project/source-analysis.md` |
| 2026-05-29 | 문서 | 실제 작업 폴더를 `C:\SVN_LinkGenesis\FA_HDX\AI-Vision IO Inspector\Docs`로 정정 | `00-project/file-map.md` |
| 2026-05-29 | 문서 | 기존 Virtual PLC 문서의 형태만 참고해 현재 프로젝트용 구성 문서 생성 | `01-requirements/`, `02-design/`, `03-development/` |
| 2026-05-29 | 개발 | `Tests\AI-Vision IO Inspector`에 WPF MVVM .NET 9 개발용 솔루션 생성 및 1차 검사/등록/DB/이력/통계 기능 구현 | `Tests\AI-Vision IO Inspector\AI.Vision.IOInspector.sln` |
| 2026-05-29 | 개발 | 부품 등록 삭제 동작을 삭제 예정 후 DB 저장 반영 방식으로 수정하고 빌드 경고 제거 | `Tests\AI-Vision IO Inspector\AI.Vision.IOInspector.App\ViewModels\MainWindowViewModel.cs` |
| 2026-05-29 | 개발 | 측정부 기준 데이터를 길이/너비/높이/두께 한 세트로 정리하고 부품 등록 UI의 추가/삭제 동작 표현을 세트 기준으로 변경 | `Tests\AI-Vision IO Inspector\AI.Vision.IOInspector.*` |
| 2026-05-29 | 개발 | 부품 등록 상단 검색의 분류설명 입력 폭을 축소하고 검색 추천 키워드 표시 영역을 추가 | `Tests\AI-Vision IO Inspector\AI.Vision.IOInspector.App\MainWindow.xaml` |
| 2026-05-29 | 개발 | UI 표기를 품번/품명으로 변경하고 검사 이력의 분류/측정/불일치 항목 표시와 CSV 저장 기능 추가 | `Tests\AI-Vision IO Inspector\AI.Vision.IOInspector.*` |
| 2026-05-29 | 개발 | 검사 이력 단일 목록과 키워드 필터, 로컬 JSON 이력/검사 로그 저장소, 보관기간/디스크 여유공간 기준 자동 삭제 정책 추가 | `Tests\AI-Vision IO Inspector\AI.Vision.IOInspector.Infrastructure\Repositories\LocalInspectionRepository.cs` |
| 2026-05-29 | 개발 | 검사 이력 CSV 저장을 현재 검색 결과 기준의 측정부별 측정값/기준값/판정 동적 컬럼 구조로 변경 | `Tests\AI-Vision IO Inspector\AI.Vision.IOInspector.App\ViewModels\MainWindowViewModel.cs` |
| 2026-05-29 | 개발 | 부품 등록 화면의 측정부 세트 폭을 줄이고 기준 이미지 목록/미리보기 영역을 확장 | `Tests\AI-Vision IO Inspector\AI.Vision.IOInspector.App\MainWindow.xaml` |
| 2026-05-29 | 개발 | DB 조회 상세/기준 이미지 배치와 미리보기 추가, 부품 등록 단일/다중 탭 및 CSV 입출력 기능 추가 | `Tests\AI-Vision IO Inspector\AI.Vision.IOInspector.App\*` |
| 2026-05-29 | 개발 | 다중품목 CSV 내보내기를 전체 DB 부품 기준정보 대상으로 변경 | `Tests\AI-Vision IO Inspector\AI.Vision.IOInspector.App\ViewModels\MainWindowViewModel.cs` |
| 2026-05-29 | 개발 | 측정부 세트를 기준값/허용/단위 구조로 확장하고 CSV 컬럼 구조 반영 | `Tests\AI-Vision IO Inspector\AI.Vision.IOInspector.App\*` |
| 2026-05-29 | 개발 | 첫 번째 측정부 표기를 `측정부`로 변경하고 두 번째부터 `측정부2`, `측정부3` 순번을 붙이도록 정리 | `Tests\AI-Vision IO Inspector\AI.Vision.IOInspector.App\*` |
| 2026-05-29 | 수정 | 기준 이미지 삭제 시 WPF 미리보기 파일 잠금으로 발생하는 IOException 방지 및 삭제 예외 메시지 처리 추가 | `Tests\AI-Vision IO Inspector\AI.Vision.IOInspector.App\*` |
| 2026-05-29 | 수정 | 기준 이미지 삭제 후 미리보기 영역이 빈 상태로 유지되도록 자동 재선택 제거 | `Tests\AI-Vision IO Inspector\AI.Vision.IOInspector.App\ViewModels\MainWindowViewModel.cs` |
| 2026-05-29 | 수정 | 기준 이미지 추가 시 파일명 충돌로 기존 이미지를 덮어쓸 수 있는 문제와 파일 접근 예외 처리 보완 | `Tests\AI-Vision IO Inspector\AI.Vision.IOInspector.Infrastructure\Services\LocalReferenceImageFileService.cs` |
| 2026-05-29 | 수정 | 기준 이미지 미리보기 디코딩 실패 시 converter 예외가 UI로 전파되는 문제 보완 | `Tests\AI-Vision IO Inspector\AI.Vision.IOInspector.App\Converters\ImageFilePathConverter.cs` |
| 2026-05-29 | 수정 | 기준 이미지 추가 전 원본 이미지 디코딩 검증을 추가해 손상/미지원 이미지 등록 방지 | `Tests\AI-Vision IO Inspector\AI.Vision.IOInspector.App\ViewModels\MainWindowViewModel.cs` |
| 2026-05-29 | 개발 | 기준 이미지 저장을 위치별 유니크 구조로 변경하고 같은 위치 재등록 시 OldVer 백업 후 교체, 부품 삭제 시 이미지 폴더 삭제 처리 추가 | `Tests\AI-Vision IO Inspector\AI.Vision.IOInspector.*` |
| 2026-05-29 | 수정 | 기준 이미지 목록에서 File 컬럼을 제거하고 Top/Front/Back/Left/Right/Thickness 고정 순서로 표시 | `Tests\AI-Vision IO Inspector\AI.Vision.IOInspector.App\*` |
| 2026-05-29 | 개발 | 단일품목 등록 기준 이미지 영역 확대 및 File/Path 컬럼 폭 조정 | `Tests\AI-Vision IO Inspector\AI.Vision.IOInspector.App\MainWindow.xaml` |
| 2026-05-29 | 개발 | 메인 검사 화면을 6개 카메라 화면 중심으로 재구성하고 기준 이미지/실시간 이미지/상단 PASS·FAIL 배지를 분리 | `Tests\AI-Vision IO Inspector\AI.Vision.IOInspector.App\*` |
| 2026-05-29 | 수정 | 기준 이미지 목록의 긴 절대경로를 `REFERENCE:\\분류코드\품번` 관리경로로 축약 표시 | `Tests\AI-Vision IO Inspector\AI.Vision.IOInspector.App\*` |
| 2026-05-29 | 수정 | 신규입력 측정부 허용 기본값을 `0`으로 변경 | `Tests\AI-Vision IO Inspector\AI.Vision.IOInspector.App\ViewModels\MeasurementSetViewModel.cs` |
| 2026-05-29 | 개발 | SQLite `DB/DataBase.db` 생성, `export_Test.csv` 기준정보 1회성 적재, PartList/History 테이블 저장소 연결 | `Tests\AI-Vision IO Inspector\AI.Vision.IOInspector.Infrastructure\*` |
| 2026-05-29 | 수정 | `export_Test.csv` 런타임 자동 적재 로직 제거, DB 파일 부재 시 빈 스키마만 생성하도록 변경 | `Tests\AI-Vision IO Inspector\AI.Vision.IOInspector.Infrastructure\Repositories\SqliteDatabase.cs` |
| 2026-05-29 | 문서 | 누적형 md 업데이트는 날짜 섹션 아래에 기록하는 원칙 반영 | `Docs\03-development\work-log.md` |
| 2026-05-29 | 문서 | 확정 카메라 사양과 `VLAD Source` 분석 결과를 바탕으로 직접 SDK 우선, RTSP/NVR 보조, AI 측정 계약, 단위 보정, SQLite 확장 권장안을 정리 | `Tests\AI-Vision IO Inspector\AI.Vision.IOInspector.Vision\Docs\camera-ai-integration.md` |
| 2026-05-29 | 수정 | DB와 런타임 데이터 기준 위치를 `Tests\AI-Vision IO Inspector` 솔루션 폴더 내부로 이동하고 앱 경로 해석을 수정 | `Tests\AI-Vision IO Inspector\DB\DataBase.db` |
| 2026-05-29 | 수정 | CSV 내보내기 파일 재불러오기 시 UTF-8 BOM 헤더 처리와 행별 오류 표시를 보완 | `Tests\AI-Vision IO Inspector\AI.Vision.IOInspector.App\ViewModels\MainWindowViewModel.cs` |
| 2026-05-29 | 개발 | 부품등록 기준 이미지 영역에 현재 화면 6개 이미지 일괄 저장 버튼 추가 | `Tests\AI-Vision IO Inspector\AI.Vision.IOInspector.App\MainWindow.xaml` |
