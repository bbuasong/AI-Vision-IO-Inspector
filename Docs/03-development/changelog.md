# 변경 이력

프로젝트 문서와 구현의 의미 있는 변경을 기록한다.

| 날짜 | 구분 | 내용 | 관련 문서/작업 |
| --- | --- | --- | --- |
| 2026-05-28 | 문서 | 요구사항 명세서와 업무파악 정리파일을 분석해 프로젝트 문서 구조를 재작성 | `00-project/source-analysis.md` |
| 2026-05-28 | 문서 | 실제 작업 폴더를 `C:\SVN_LinkGenesis\FA_HDX\AI-Vision IO Inspector\Docs`로 정정 | `00-project/file-map.md` |
| 2026-05-28 | 문서 | 기존 Virtual PLC 문서의 형태만 참고해 현재 프로젝트용 구성 문서 생성 | `01-requirements/`, `02-design/`, `03-development/` |
| 2026-05-28 | 개발 | `Tests\AI-Vision IO Inspector`에 WPF MVVM .NET 9 개발용 솔루션 생성 및 1차 검사/등록/DB/이력/통계 기능 구현 | `Tests\AI-Vision IO Inspector\AI.Vision.IOInspector.sln` |
| 2026-05-28 | 개발 | 부품 등록 삭제 동작을 삭제 예정 후 DB 저장 반영 방식으로 수정하고 빌드 경고 제거 | `Tests\AI-Vision IO Inspector\AI.Vision.IOInspector.App\ViewModels\MainWindowViewModel.cs` |
| 2026-05-28 | 개발 | 측정부 기준 데이터를 길이/너비/높이/두께 한 세트로 정리하고 부품 등록 UI의 추가/삭제 동작 표현을 세트 기준으로 변경 | `Tests\AI-Vision IO Inspector\AI.Vision.IOInspector.*` |
