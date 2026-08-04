# Versioned Source

이 폴더는 AI-Vision IO Inspector의 버전별 소스를 관리합니다.

| 경로 | 프로그램 버전 | 용도 |
| --- | --- | --- |
| `Version1_0_0_0/AI-Vision IO Inspector` | 1.0.0.0 | 1.0 기준선과 공통 수정 유지 |
| `Version1_1_0_0/AI-Vision IO Inspector` | 1.1.0.0 | 현재 통합 개발 버전 |

두 버전은 Visual Studio 2022, .NET Framework 4.7.2, Windows x64를 기준으로 빌드합니다.

```powershell
dotnet build "AI.Vision.IOInspector.sln" -c Debug -p:Platform=x64
```

실행 중 생성되는 DB, 검사 이미지, 모델 파일, 빌드 산출물과 대용량 Epson OCR 런타임은 Git에서 제외합니다. 배포 PC에는 별도 관리하는 런타임 패키지를 배치해야 합니다.
