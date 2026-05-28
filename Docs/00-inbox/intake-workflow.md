# 신규 요청 처리 절차

메일, 전화, 음성 파일, 첨부 문서를 받았을 때 Markdown 문서로 누락 없이 관리하기 위한 절차다.

## 1. 원본 보관

| 자료 유형 | 저장 위치 | 파일명 권장 형식 |
| --- | --- | --- |
| 메일 | `00-inbox/mail/` | `YYYY-MM-DD_발신자_제목.eml` |
| 첨부 파일 | `00-inbox/attachments/` | 원본 파일명 유지 |
| 전달 문서 | `00-inbox/documents/` | 원본 파일명 유지 |
| 전화/회의 녹음 | `00-inbox/audio/` | `YYYY-MM-DD_대상_주제.ext` |
| 전사본 | `00-inbox/transcripts/` | `YYYY-MM-DD_대상_주제.transcript.md` |
| 요약본 | `00-inbox/summaries/` | `YYYY-MM-DD_대상_주제.summary.md` |

## 2. 접수 로그 등록

`00-inbox/intake-log.md`에 접수 ID, 원본 위치, 주요 내용, 후속 작업, 상태를 기록한다.

접수 ID 형식:

```text
IN-YYYYMMDD-001
```

## 3. 요약 작성

자료 유형에 따라 `templates/`의 템플릿을 복사해서 요약본을 작성한다.

요약본에는 반드시 다음을 포함한다.

- 원본 파일 위치
- 접수일
- 요청자 또는 발신자
- 핵심 요청
- 요구사항 변경 여부
- 작업해야 할 내용
- 확인 질문
- 반영할 문서

## 4. 프로젝트 문서 반영

| 내용 | 반영 위치 |
| --- | --- |
| 확정 요구사항 | `01-requirements/requirements.md` |
| 요구사항 출처 | `01-requirements/requirement-traceability.md` |
| 설계 변경 | `02-design/` |
| 작업 항목 | `03-development/task-board.md` |
| 미확정 질문 | `03-development/questions.md` |
| 확정 결정 | `03-development/decisions.md` |
| 회의/통화 기록 | `04-meetings/` |

## 5. 처리 완료 기준

신규 요청은 다음이 모두 완료되어야 완료 상태로 본다.

- 원본 파일 위치가 기록됨
- 요약 Markdown이 작성됨
- 요구사항/작업/질문/설계 중 필요한 문서에 반영됨
- 후속 확인이 필요한 항목은 담당자와 질문 상태가 명시됨


