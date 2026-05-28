# Inbox

메일, 첨부 문서, 음성 파일, 고객 전달 자료 등 원본 또는 원본에 가까운 자료를 보관합니다.

## 폴더

| 경로 | 용도 |
| --- | --- |
| `mail/` | 원본 메일, 메일 본문 복사본, `.eml`, `.msg`, `.txt` |
| `attachments/` | 메일 첨부 파일 |
| `documents/` | 요구사항 명세서, 업무파악 문서 등 전달받은 문서 |
| `audio/` | 전화/회의 녹음 파일 |
| `transcripts/` | 음성 파일 전사본 |
| `summaries/` | 접수 자료별 요약 Markdown |
| `templates/` | 요약 문서 템플릿 |
| `extracted/` | docx/pdf 등에서 기계적으로 추출한 텍스트와 이미지 |

## 운영 규칙

- 원본 자료는 수정하지 않습니다.
- 해석, 요약, 액션 아이템은 `summaries/`와 관련 프로젝트 문서에 기록합니다.
- 접수 내역은 `intake-log.md`에 먼저 남깁니다.
- 처리 절차는 `intake-workflow.md`를 따릅니다.
