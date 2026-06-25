# Open Items

기준일: 2026-06-24

이 문서는 아직 구현이 끝나지 않았거나, 구현은 됐지만 실제 장비/AI 담당자 검증이 필요한 항목만 유지합니다. 완료된 내용은 아래 `정리된 항목`에 남기고, 신규 잔여 항목은 날짜 기준으로 계속 갱신합니다.

## 남은 항목

| ID | 영역 | 상태 | 현재 판단 | 다음 작업 | 완료 기준 |
| --- | --- | --- | --- | --- | --- |
| O-001 | VLAD 최종 모델 | 미완료 | `RuntimeData\Models\VLAD\Ex_Weight`에 checkpoint 계열 파일은 있으나, VLAD_SDK가 직접 읽는 최종 추론 export 구조인지 확정되지 않았습니다. | AI 담당자에게 `nets_model.json + saved_model\saved_model.pb` 또는 `model.onnx/model.pt/model.t7` 등 실제 요구 구조 확인 | `VLAD_Custom_Registration`이 유효한 `VladId`를 반환하고 startup log에 성공 기록 |
| O-002 | CUDA/cuDNN/VC Runtime | 미완료 | `Native\VLAD`에 모든 외부 런타임이 포함되어 있지는 않습니다. `cudart64_110.dll`, `cudnn64_8.dll`, `cublas64_11.dll`, VC++ Runtime 배치/설치가 필요할 수 있습니다. | 배포 PC에서 `where cudart64_110.dll`, `where cudnn64_8.dll`, VC++ Runtime 설치 여부 확인 | 앱 시작과 검사 시작 중 네이티브 DLL 누락/0xc0000409 종료 없음 |
| O-003 | VLAD in-process 안정성 | 검증필요 | 현재 WPF 프로세스 안에서 `VLAD_Ops_Ai_Env_Start`를 초기화합니다. 디버깅은 쉬워졌지만 네이티브 DLL이 fail-fast하면 앱이 종료될 수 있습니다. | 최종 모델/런타임 배치 후 앱 시작 30분, 검사 반복 100회 안정성 확인 | 검사 실패가 앱 종료가 아니라 로그/결과 메시지로 남음 |
| O-004 | RTSP/NVR 실제 6채널 | 검증필요 | `CFG\Config.json` 기준으로 카메라 URL을 읽습니다. 일부 채널 수신은 확인했으나 6채널 장시간 스트리밍은 미검증입니다. | Top/Front/Back/Left/Right/Thickness 전 채널 연결, 옵션 UI 상태와 검사 UI 영상 확인 | 6채널 모두 연결됨 표시, 영상 갱신, 캡처 저장 가능 |
| O-005 | Camera 위치 매핑 | 검증필요 | 위치명은 `Top/Front/Back/Left/Right/Thickness` 기준입니다. 실제 물리 카메라와 포트/URL 매핑은 현장 확정 필요입니다. | 옵션 UI와 `CFG\Config.json`의 CAM 순서, RTSP URL, 위치명을 현장 기준으로 확정 | 기준 이미지 저장과 검사 이력 파일명이 올바른 위치명으로 저장 |
| O-006 | VLAD 결과 스키마 | 미완료 | `detectData`, `detectText`, TLV 구조에서 길이/너비/높이/두께, 카메라별 Pass/Fail을 어떻게 받는지 최종 스키마가 필요합니다. | AI 담당자에게 샘플 결과 구조체/문자열/클래스 리스트 정의 요청 | `VladInferenceResultParser`가 실제 결과를 측정값과 판정으로 변환 |
| O-007 | Pixel-mm 보정 | 검증필요 | 단위는 mm 고정 방향입니다. bbox 기반 측정 시 픽셀-mm 보정값이 필요합니다. | 카메라별 해상도, 렌즈, 촬영 거리, 보정판 기준으로 `CFG\Calibration.json` 확정 | 같은 시편 반복 측정 오차가 허용 범위 안에 들어옴 |
| O-008 | 기준 이미지 정책 | 부분완료 | 기준 이미지가 없어도 사용자가 계속 진행을 선택하면 검사는 시도합니다. 양산 전 차단/허용 정책 확정 필요입니다. | 운영 기준에서 기준 이미지 필수 여부 결정 | 기준 이미지 없음 처리 정책이 요구사항/화면 메시지/테스트에 일치 |
| O-009 | History 보존 정책 | 미완료 | `DB\History\yyyyMMdd\HH\분류코드\품번_품명_카메라위치_시간` 구조는 준비되어 있습니다. 삭제 정책은 미확정입니다. | 기간 또는 HDD 여유 공간 기준의 자동 삭제 정책 설계 | 오래된 이미지/로그가 설정에 따라 삭제되고 DB 이력과 불일치 없음 |
| O-010 | 배포 패키지 | 검증필요 | x64 Release 출력 폴더에 `CFG/DB/Native/VLAD/RuntimeData/Models` 복사 구조는 준비됐습니다. 외부 런타임 포함 여부는 별도 결정 필요입니다. | 클린 PC에서 Release 출력 폴더만 복사해 실행 테스트 | 개발툴 없이 앱 실행, DB 조회, 영상 수신, 검사 시작 가능 |
| O-011 | Git 대용량 파일 | 검토필요 | `Native\VLAD`, 모델, ZIP은 대용량입니다. GitHub 일반 저장소에 직접 올릴지 Git LFS/배포 패키지로 분리할지 결정 필요입니다. | GitHub 정책과 담당자 공유 방식 확정 | clone 후 개발 가능한 최소 세트와 별도 런타임 배포 절차 문서화 |
| O-012 | 통계 UI | 미완료 | 기본 통계 탭은 있으나 실제 운영 지표, 기간 조건, NG 원인 집계 기준이 부족합니다. | 필요한 생산/검사 KPI 확정 | 기간/품번/분류/NG 사유 기준 통계 조회 가능 |
| O-013 | CSV/Excel 운영 | 부분완료 | CSV 기준정보 일괄 반영은 있으나 현장 파일 포맷 최종안과 오류 리포트 형식은 추가 검증 필요입니다. | 실제 운영 CSV/Excel 샘플로 import/export 왕복 테스트 | DB 저장 전 오류 항목을 명확히 표시하고 정상 데이터만 반영 |
| O-014 | 두께 복수 측정 | 보류 | 현재는 길이/너비/높이/두께 1세트 중심입니다. 두께 복수 측정 요구가 재확정되면 모델/UI/CSV 확장이 필요합니다. | 고객이 두께2 이상 필요 여부 확정 | UI/DB/CSV/History가 복수 두께를 일관되게 처리 |
| O-015 | Scanner OCR 샘플 | 별도검증 | `Docs\05-simulator\Scanner`는 메인 앱과 별도 .NET 9 샘플입니다. Windows OCR/PaddleOCR 병행 판단은 추가 실험 중입니다. | 실제 Epson ES-C320W 스캔 20장 이상으로 OCR 정확도 비교 | 대상 품번 영역 추출률과 오인식 보정률 기준 충족 |

## 정리된 항목

| ID | 정리일 | 내용 |
| --- | --- | --- |
| C-001 | 2026-05-29 | SQLite `DB\DataBase.db` 기준 PartList/History 구조 생성 |
| C-002 | 2026-06-08 | 부품 삭제/다중품목 교체 시 기준 이미지 파일 자동 삭제 금지 정책 적용 |
| C-003 | 2026-06-08 | 분류코드와 분류설명 불일치 저장 차단 팝업 적용 |
| C-004 | 2026-06-11 | 검사 캡처 이미지 `DB\History\yyyyMMdd\HH\분류코드` 구조 적용 |
| C-005 | 2026-06-15 | Search DB 작업대와 DB 조회/부품등록 냉장고 검색 상태 분리 |
| C-006 | 2026-06-19 | `RuntimeData\Camera\camera-config.json` 제거, `CFG\Config.json` 기준으로 단일화 |
| C-007 | 2026-06-19 | 사용하지 않는 `VLAD_Ops_Ai_Compat`, `VladFunctionAdapter`, `VladRuntimeContext` 제거 |
| C-008 | 2026-06-22 | `ProcessIsolatedAiInferenceService`, `VladStartupInitializationService` 제거 후 in-process `VisionInferenceWorker` 흐름으로 문서 기준 정리 |
| C-009 | 2026-06-22 | `RuntimeAssemblyResolver`로 `Native\VLAD` 하위 관리 DLL 탐색 문제 대응 |
| C-010 | 2026-06-22 | Debug/Release x64 빌드 출력에 `CFG/DB/Native/VLAD/RuntimeData/Models` 복사 구조 확인 |

## 관리 원칙

- 매 작업일마다 남은 항목의 상태를 `미완료`, `부분완료`, `검증필요`, `보류`, `완료` 중 하나로 정리합니다.
- 완료된 항목은 삭제하지 않고 `정리된 항목`으로 옮깁니다.
- 고객/AI 담당자 확인이 필요한 항목은 완료로 처리하지 않습니다.
## 2026-06-24 측정부 구조 변경 후 잔여 항목

- `O-016` 관리 코드에는 측정부 `IndexNo/항목/색상/기준값/허용오차/X1/Y1/X2/Y2/단위` 입력 DTO가 준비됐습니다. 이 DTO를 실제 VLAD 네이티브 함수에 넘길 함수명, 구조체 메모리 배치, 호출 시점을 AI 담당자와 확정해야 합니다. Crop과 추론 방식은 DLL 내부 책임입니다.
- `O-017` AI DLL이 측정부 `IndexNo`, 실제 측정값, 단위(mm), 처리 성공 여부, 오류 메시지를 반환하는 출력 구조를 확정해야 합니다.
- `O-018` 실제 부품의 Thickness 기준 이미지에서 측정부 5개 누적 표시, 확대 좌표 정확도, 고해상도 이미지 성능을 현장 해상도로 검증해야 합니다.
- `O-019` 다중품목 CSV의 측정부1~5 항목/기준/허용/색상/X1/Y1/X2/Y2/단위 왕복 형식은 구현했습니다. 실제 운영 CSV로 내보내기-불러오기-DB 저장 후 값이 동일한지 현장 데이터 검증이 필요합니다.
- `O-020` 실제 6채널 카메라로 `현재6개저장`을 반복 실행해 Temp 초기화, 6장 미리보기, DB 저장 후 최종 경로/등록시간/품번_coordinate 파일 교체와 OldVer 미생성을 현장 장비 기준으로 검증해야 합니다.
- `O-021` 파일 시스템과 SQLite는 하나의 공통 트랜잭션을 사용할 수 없습니다. 디스크 오류 또는 SQLite 저장 실패를 강제로 발생시켜 Temp 유지와 최종 파일/DB 메타데이터의 복구 정책을 추가 검증해야 합니다.
- `O-022` 현재 `CFG\Config.json`의 CAM0~CAM5가 모두 같은 테스트 RTSP URL입니다. 실제 6대 카메라 설치 시 Top/Front/Back/Left/Right/Thickness별 고유 NVR 채널 URL을 확정하고 장시간 동시 재생 CPU/GPU/네트워크 사용량을 검증해야 합니다.
- `O-023` 검사 완료 시 측정 이미지 전체 화면과 좌측 상단 1/4 기준 이미지 인셋이 실제 4:3/6:5 카메라 해상도에서 주요 검사 영역을 가리지 않는지 현장 UI로 확인해야 합니다.
