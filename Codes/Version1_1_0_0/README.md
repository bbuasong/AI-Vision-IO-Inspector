# Version 1.1.0.0

`AI-Vision IO Inspector` 폴더가 Version 1.1.0.0의 현재 통합 소스입니다.

2026-08-04 기준으로 VLAD HD API 1.1 준비 사항을 포함합니다.

- View별 최소 요청 JSON과 `scoreThreshold` 전달
- Thickness 측정부 최대 5개 전달
- `viewJudge`, 측정부별 `judge`, `failureReasons` 파싱
- W/D/H 결과 파싱 및 검사 화면 전달
- 유사도 후보의 AI 판정과 순위 유지, 최대 3개 표시
- 결과 JSON 크기에 따른 UTF-8 버퍼 재할당
- 메인 화면 표시 후 Epson OCR API 비동기 선기동 및 상태 표시
- 검사 단계별 진행 이벤트와 RTSP 스트리밍 유지 오버레이
- 검사 결과 화면·이력의 `PASS / FAIL / ERROR` 구분
- OCR 미등록 품번의 사용자 확인 후 부품 등록 연결
- 프로그램 종료 시 OCR 작업자 우선 정리

2026-08-04 x64 Debug 빌드에서 경고 0개, 오류 0개를 확인했습니다.
