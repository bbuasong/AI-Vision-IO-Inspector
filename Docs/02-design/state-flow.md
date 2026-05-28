# 상태 흐름

검사 실행, 부품 등록, 엑셀 업로드의 주요 상태를 정의한다.

## 검사 상태

```mermaid
stateDiagram-v2
  [*] --> Idle
  Idle --> PartLookup: 라벨/품번 입력
  PartLookup --> Ready: 기준정보 조회 성공
  PartLookup --> Error: 기준정보 없음/DB 오류
  Ready --> Capturing: 검사 시작
  Capturing --> Inferencing: 촬영 성공
  Capturing --> Error: 촬영 실패
  Inferencing --> Measuring: AI 추론 성공
  Inferencing --> Error: AI 추론 실패
  Measuring --> Judging: 측정 완료
  Measuring --> Error: 측정 실패
  Judging --> Saving: OK/NG 판정
  Saving --> Completed: 저장 성공
  Saving --> Error: 저장 실패
  Completed --> Idle: 다음 검사
  Error --> Idle: 확인 후 초기화
```

## 검사 상태 정의

| 상태 | 의미 | 사용자 표시 |
| --- | --- | --- |
| Idle | 입력 대기 | 대기 |
| PartLookup | 기준정보 조회 중 | 조회중 |
| Ready | 기준정보 준비 완료 | 검사 가능 |
| Capturing | 카메라 촬영 중 | 촬영중 |
| Inferencing | AI 추론 중 | 판정중 |
| Measuring | 치수 측정 중 | 측정중 |
| Judging | OK/NG 판정 중 | 판정중 |
| Saving | 결과 저장 중 | 저장중 |
| Completed | 검사 완료 | OK 또는 NG |
| Error | 오류 발생 | 오류 메시지 |

## 부품 등록 상태

```mermaid
stateDiagram-v2
  [*] --> Editing
  Editing --> Validating: 저장 요청
  Validating --> Saving: 검증 성공
  Validating --> Editing: 검증 실패
  Saving --> Saved: 저장 성공
  Saving --> Editing: 저장 실패
  Saved --> Editing: 추가 수정
```

## 엑셀 업로드 상태

```mermaid
stateDiagram-v2
  [*] --> FileSelected
  FileSelected --> Parsing
  Parsing --> Validating
  Validating --> Preview: 검증 완료
  Preview --> Importing: 반영 요청
  Importing --> Imported: 반영 성공
  Importing --> Failed: 반영 실패
  Failed --> Preview: 오류 확인
```
