# Version 1.0.0.0

`AI-Vision IO Inspector` 폴더가 Version 1.0.0.0의 빌드 기준 소스입니다.

2026-08-04에 다음 공통 VLAD HD API 계약 변경을 역적용했습니다.

- 일반 View 요청 JSON 최소화
- Thickness 측정부 최대 5개 전달
- AI의 `viewJudge`와 측정부별 `judge`를 최종 판정에 사용
- W/D/H 결과 파싱 및 화면 전달
- 유사도 검색 결과의 AI 순위 유지, 최대 3개 표시
- 가변 길이 UTF-8 결과 버퍼 재할당 처리

Version 1.1.0.0 전용 UI와 기능은 이 버전에 추가하지 않습니다. 어셈블리 및 파일 버전은 `1.0.0.0`으로 유지합니다.
