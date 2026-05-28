# 요구사항 추적표

요구사항이 어느 원본 자료에서 나왔고, 어떤 설계/작업과 연결되는지 추적한다.

## 출처 코드

| 코드 | 자료 |
| --- | --- |
| SRC-001 | `00-inbox/documents/요구사항 명세서.docx` |
| SRC-002 | `00-inbox/documents/HD현대사이트솔루션_AI비전_입고검사_업무파악_정리.docx` |

## 추적표

| 요구사항 ID | 출처 | 연결 설계 | 연결 작업 | 상태 |
| --- | --- | --- | --- | --- |
| FR-001 | SRC-001, SRC-002 | `02-design/data-model.md` | T-010 | 요구 |
| FR-002 | SRC-001 | `02-design/screen-map.md` | T-020 | 요구 |
| FR-003 | SRC-001 | `02-design/data-model.md` | T-030 | 요구 |
| FR-004 | SRC-002, SRC-001 이미지 | `02-design/architecture.md` | T-080 | 추가 요청 |
| FR-005 | SRC-001, SRC-002 | `02-design/architecture.md` | T-040 | 확인 필요 |
| FR-006 | SRC-001 | `02-design/architecture.md` | T-050 | 요구 |
| FR-007 | SRC-001, SRC-002 | `02-design/architecture.md` | T-060 | 요구 |
| FR-008 | SRC-001, SRC-002 | `02-design/data-model.md` | T-070 | 요구 |
| FR-009 | SRC-001, SRC-002 | `02-design/architecture.md` | T-040 | 요구 |
| FR-010 | SRC-002 | `02-design/screen-map.md` | T-090 | 추가 요청 |
| FR-011 | SRC-002 | `02-design/data-model.md` | T-100 | 요구 |
| FR-012 | SRC-001, SRC-002 | `02-design/data-model.md` | T-100 | 요구 |
| FR-013 | SRC-001 | `02-design/architecture.md` | T-110 | 요구 |
| FR-014 | SRC-002 | `02-design/screen-map.md` | T-120 | 추가 요청 |
| FR-015 | SRC-001 | `02-design/architecture.md` | T-060 | 확인 필요 |

## 변경 관리 규칙

- 신규 요청이 들어오면 요구사항 ID를 추가한다.
- 기존 요구사항 변경은 기존 행의 상태와 출처를 갱신하고 `03-development/decisions.md`에 변경 이유를 남긴다.
- 확인 필요 요구사항은 `03-development/questions.md`의 질문 ID와 연결한다.

