# 진행 로그

## 2026-05-28

- 프로젝트 초기 문서 구조와 VS Code 설정을 생성했습니다.
- 원본 자료 정리, 요구사항 정리, 설계, 작업 추적을 분리했습니다.
- `Downloads`에 있던 `요구사항 명세서.docx`와 `HD현대사이트솔루션_AI비전_입고검사_업무파악_정리.docx`를 `00-inbox/documents/`에 원본 보관했습니다.
- 두 docx 파일의 본문, 표, 이미지를 `00-inbox/extracted/`에 추출했습니다.
- 요구사항, 프로그램 구조, 데이터 모델, 화면 구성, 개발 로드맵, 신규 요청 처리 절차를 프로젝트 기준으로 갱신했습니다.


- `Tests\AI-Vision IO Inspector`에 Visual Studio 2022 기반 .NET 9 WPF MVVM 개발용 솔루션을 생성하고, Domain/Application/Infrastructure/App 구조로 1차 기능을 구현했습니다.
- `dotnet build AI.Vision.IOInspector.sln --configuration Debug` 결과 경고 0개, 오류 0개로 빌드를 확인했습니다.
- 부품 등록의 삭제 동작을 즉시 삭제에서 삭제 예정 후 `DB 저장` 시 반영되는 흐름으로 수정하고, 빌드 경고 0개/오류 0개를 확인했습니다.
- 측정부 기준 데이터를 세트 단위(측정부 1 길이/너비/높이/두께)로 정리하고, 부품 등록 UI의 추가/삭제 버튼을 세트 단위 표현으로 변경했습니다. 빌드 경고 0개/오류 0개를 확인했습니다.
