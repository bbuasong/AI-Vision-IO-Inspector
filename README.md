# AI-Vision IO Inspector

HD현대사이트솔루션 AI 비전 입고검사 업무를 기준으로 정리한 WPF MVVM 개발 프로젝트입니다.

## 폴더 구조

- `Docs/`: 요구사항, 업무 분석, 설계, 작업 로그, 회의/요청 정리 문서
- `Tests/AI-Vision IO Inspector/`: Visual Studio 2022 기반 .NET 9 WPF MVVM 개발/검증용 솔루션
- `src/`: 빌드와 기능 검증이 끝난 안정 코드 관리 예정 영역

## 현재 개발 기준

- UI: C# WPF, MVVM, .NET 9.0, Visual Studio 2022 호환
- 구조: Domain / Application / Infrastructure / App 분리
- 문서: `Docs/AGENTS.md`와 `Docs/03-development/` 기준으로 작업 이력 관리
- 빌드 확인: `Tests/AI-Vision IO Inspector/AI.Vision.IOInspector.sln` 기준 `dotnet build`

## Git 관리 원칙

- `bin/`, `obj/`, `.vs/` 등 빌드/IDE 산출물은 커밋하지 않습니다.
- 고객 원본/분석 문서는 `Docs/00-inbox/`와 프로젝트 문서 구조에서 관리합니다.
- 기능 변경 후 빌드가 통과한 상태를 커밋 기준으로 삼습니다.
