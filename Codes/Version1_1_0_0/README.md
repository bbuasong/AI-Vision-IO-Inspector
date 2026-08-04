# Version 1.1.0.0

`AI-Vision IO Inspector` 폴더가 Version 1.1.0.0의 현재 통합 소스입니다.

2026-08-04 기준으로 VLAD HD API 1.1 준비 사항을 포함합니다.

- View별 최소 요청 JSON과 `scoreThreshold` 전달
- Thickness 측정부 최대 5개 전달
- `viewJudge`, 측정부별 `judge`, `failureReasons` 파싱
- W/D/H 결과 파싱 및 검사 화면 전달
- 유사도 후보의 AI 판정과 순위 유지, 최대 3개 표시
- 결과 JSON 크기에 따른 UTF-8 버퍼 재할당

x64 Debug 빌드에서 경고 0개, 오류 0개를 확인했습니다.
