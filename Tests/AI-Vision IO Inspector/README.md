# AI-Vision IO Inspector - Tests 개발용

이 폴더는 HD현대사이트솔루션 AI 비전 기반 입고검사 시스템의 개발/검증용 WPF MVVM 프로젝트입니다.

## 위치

```text
C:\SVN_LinkGenesis\FA_HDX\AI-Vision IO Inspector\Tests\AI-Vision IO Inspector
```

안정화된 코드는 이후 상위 `Src` 폴더로 승격해서 관리합니다.

## 실행 환경

- Visual Studio 2022
- .NET SDK 9.0
- WPF
- MVVM 구조
- 외부 MVVM/DI 패키지 없음

## 솔루션

```text
AI.Vision.IOInspector.sln
```

Visual Studio 2022에서 위 솔루션 파일을 열면 됩니다.

## 프로젝트 구조

| 프로젝트 | 역할 |
| --- | --- |
| `AI.Vision.IOInspector.Domain` | 부품, 측정부, 검사 이력, 판정 결과 등 순수 모델 |
| `AI.Vision.IOInspector.Application` | 검사 흐름, 판정, 통계, 기준정보 업무 서비스 |
| `AI.Vision.IOInspector.Infrastructure` | 메모리 저장소, 카메라/AI/파일/엑셀 시뮬레이션 어댑터 |
| `AI.Vision.IOInspector.App` | WPF 화면, ViewModel, 수동 Bootstrapper |

## 현재 구현 기능

- 부품 기준정보 목록 표시
- 메인 검사 화면
- 6방향 이미지 슬롯 표시
- 카메라 촬영 시뮬레이션
- AI 추론 시뮬레이션
- 측정부 기준값/측정값 비교
- OK/NG/Error 판정 분리
- 검사 이벤트 로그 표시
- 부품 등록/수정 기본 흐름
- DB 검색/샘플 엑셀 Import 흐름
- 검사 이력 조회
- 통계 요약 표시

## 빌드

```powershell
dotnet build AI.Vision.IOInspector.sln --configuration Debug
```

## 실행

```powershell
dotnet run --project AI.Vision.IOInspector.App\AI.Vision.IOInspector.App.csproj
```

## 구현 원칙

- 무분별한 람다식 사용 금지
- delegate 최소화
- .NET Framework 4.5 스타일에 익숙한 사용자가 읽기 쉬운 명시적 코드 선호
- 주요 프로그램 흐름과 함수에는 의도 파악용 주석 작성
- 카메라, AI, DB, 엑셀은 Adapter/Repository 경계 뒤에 배치
