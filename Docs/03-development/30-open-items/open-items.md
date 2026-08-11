# Open Items

기준일: 2026-08-10

이 문서는 아직 구현이 끝나지 않았거나, 구현은 됐지만 실제 장비/AI 담당자 검증이 필요한 항목만 유지합니다. 완료된 내용은 아래 `정리된 항목`에 남기고, 신규 잔여 항목은 날짜 기준으로 계속 갱신합니다.

## 2026-08-10 현장 검증 잔여 항목

| ID | 상태 | 내용 | 다음 확인 | 완료 기준 |
| --- | --- | --- | --- | --- |
| O-032 | 검증필요 | 6개 RTSP URL을 VLC 또는 ffmpeg로 각각 단독 검증한 로그를 확인해야 합니다. | `cam1.txt`~`cam6.txt`에서 연결 성공/실패, 인증 오류, 코덱, 해상도, FPS, timeout, packet loss를 채널별로 정리 | 6개 URL 각각 단독 재생 가능 여부와 실패 사유가 명확히 구분됨 |
| O-033 | 정책/검증필요 | 고장 카메라가 있어도 정상 카메라만으로 검사를 계속할지, 실패 채널을 ERROR로 최종 판정에 반영할지 확인해야 합니다. | 활성 카메라 중 일부 실패 시 최신 프레임만 사용하고 이전 snapshot을 재사용하지 않는지 확인 | 정상 채널은 처리하되 실패 채널은 무음 PASS가 아니며 최종 판정 규칙이 UI/이력/로그에 일치 |
| O-034 | 검증필요 | 측정부가 등록된 제품에서 검사 UI의 Thickness 값과 기준 이미지가 측정부 좌표 이미지인지 확인해야 합니다. | `measurements[].indexNo/measuredValue`가 DB 측정부 IndexNo 순서와 UI Thickness 표시값에 맞는지, Thickness 기준 이미지가 `{품번}_coordinate.png`인지 확인 | 측정부 1~5 값, 판정, 이력, 검사 UI의 Thickness 기준 이미지가 모두 등록 기준과 일치 |
| O-035 | 선택 | 예전 Result/Data 명칭이 README, 주석, 오류 메시지에 일부 남아 있습니다. 실행 경로 문제는 아니지만 혼동 가능성이 있습니다. | `detectData`, `VLAD_Search_Data`, `VLAD_Inference_Mat` 표현 중 현재 MAT JSON 구조와 다른 설명을 정리할지 결정 | 운영/인수 문서와 오류 메시지가 현재 MAT JSON 방식만 설명 |
| O-036 | 선택 | 생성한 프로그램 아이콘 PNG/ICO를 실제 프로젝트 리소스에 적용할지 결정해야 합니다. | `generated-icons/ai-vision-io-inspector-icon.ico`를 WPF 앱 아이콘으로 연결할지 결정 | 실행 파일/창 아이콘이 새 아이콘으로 표시됨 |

## 2026-07-24 Version 1.1.0.0 병합 후 잔여 항목

| ID | 상태 | 내용 | 권장 방향/완료 조건 |
| --- | --- | --- | --- |
| O-027 | 구현 필요 | 결과 화면에는 `DimensionText`가 있으나 실제 W/D/H를 채우거나 결과 PNG 하단에 합성하는 구현은 백업에도 없습니다. | 새 HD 결과 JSON의 `dimensions`를 사용해 원본이 아닌 결과용 복사본에 표시하고 파일 검증 |
| O-028 | 구현 필요 | 측정부 측정값은 파싱·비교·이력에는 반영되지만 결과 PNG에 문자로 그리는 구현은 백업에도 없습니다. | `measurements[]`의 IndexNo/측정값/단위를 결과용 복사본에 표시하고 1~5개 가변 개수 검증 |
| O-029 | DLL 대기 | 제품번호 이름의 6장 합성 이미지를 만드는 `VLAD_HD_ImageMerge` 호출은 아직 없습니다. | x64 DLL export와 입력/출력 규칙 확인 후 DB 저장·검사 완료 시 호출하고 이미지/이력 삭제 시 함께 삭제 |
| O-030 | 정책 결정 필요 | `CAM_ENABLED=false` 채널은 Skip하지만, 활성 카메라가 실행 중 실패하면 현재 검사 오류가 됩니다. | 권장: 나머지는 계속 처리하되 실패 채널을 ERROR로 남기고 최종 결과도 ERROR 처리. 자동 비활성/무음 PASS는 금지 |
| O-031 | 장비/DLL 검증 필요 | 기준 이미지 확대, coordinate Thickness, 유사도 검색, Score 기준 설정을 1.1.0.0에 병합했습니다. | 실제 NVR 6채널과 새 `VLAD_Search_*` export DLL로 반복 검사 및 UI 검증 |

## 2026-07-20 VLAD HD JSON 계약 확인 항목

| ID | 상태 | 내용 | 완료 조건 |
| --- | --- | --- | --- |
| O-016 | AI 담당자 확인 필요 | 목표 `VLAD_HD_Inference_Mat(fullImageVladId, croppedImageVladId, rawData, threshold, drawMode, inspectionContextJson)`를 문서로 확정했습니다. 현재 확인된 native `VLAD_Inference_Mat`은 단일 ID 4인자여서 Context JSON과 Crop ID를 실제로 받지 못합니다. | 두 ID x64 DLL export, C/C++ 헤더, UTF-8 한글 Context 콘솔 샘플 확인 |
| O-017 | AI 담당자 확인 필요 | 목표 `VLAD_HD_InferenceData_Result` JSON에는 두 ID, 이미지/측정부/전체 판정, score, 측정값, NG 원인이 포함됩니다. 현재는 `true/false,score,measurement1...N` 문자열 parser만 사용합니다. | 결과 JSON 버퍼 크기 인자, PASS/FAIL/ERROR, 전체/Crop ID별 측정부 1~5개 실데이터 검증 |
| O-018 | AI 담당자 확인 필요 | 목표 `VLAD_Search_Mat`/`VLAD_Search_Data`는 두 ID와 UTF-8 후보 JSON, boolean `hasAlternatives`를 반환합니다. 현재 제공 DLL의 export 여부와 실제 네이티브 호출은 미확정입니다. | 위치별 후보 있음/없음, threshold 경계, 두 ID 전달, 버퍼 경계 DLL 검증 |
| O-026 | AI 담당자 확인 필요 | C#은 프로그램 시작 시 FullImageVladId와 CroppedImageVladId를 함께 생성하고 학습 후 함께 재초기화하도록 변경했습니다. RTSP callback은 전체 이미지 ID에만 등록합니다. | 같은 프로세스의 두 `VLAD_Custom_Registration` 지원 여부, VRAM 사용량, 전체/Crop 모델 경로와 두 ID native 사용 결과 검증 |
| O-019 | 장비 검증 필요 | 기준이미지 팝업, coordinate Thickness 대체, Score 95 설정은 코드에 반영했습니다. | 실제 6채널, 저장 기준이미지, 측정부 1~5개로 화면 및 판정 검증 |

## 남은 항목

| ID | 영역 | 상태 | 현재 판단 | 다음 작업 | 완료 기준 |
| --- | --- | --- | --- | --- | --- |
| O-001 | VLAD 최종 모델 | 미완료 | `RuntimeData\Models\VLAD\Ex_Weight`에 checkpoint 계열 파일은 있으나, VLAD_SDK가 직접 읽는 최종 추론 export 구조인지 확정되지 않았습니다. | AI 담당자에게 `nets_model.json + saved_model\saved_model.pb` 또는 `model.onnx/model.pt/model.t7` 등 실제 요구 구조 확인 | `VLAD_Custom_Registration`이 유효한 `VladId`를 반환하고 startup log에 성공 기록 |
| O-002 | CUDA/cuDNN/VC Runtime | 미완료 | `Native\VLAD`에 모든 외부 런타임이 포함되어 있지는 않습니다. `cudart64_110.dll`, `cudnn64_8.dll`, `cublas64_11.dll`, VC++ Runtime 배치/설치가 필요할 수 있습니다. | 배포 PC에서 `where cudart64_110.dll`, `where cudnn64_8.dll`, VC++ Runtime 설치 여부 확인 | 앱 시작과 검사 시작 중 네이티브 DLL 누락/0xc0000409 종료 없음 |
| O-003 | VLAD in-process 안정성 | 검증필요 | 현재 WPF 프로세스 안에서 `VLAD_Ops_Ai_Env_Start`를 초기화합니다. 디버깅은 쉬워졌지만 네이티브 DLL이 fail-fast하면 앱이 종료될 수 있습니다. | 최종 모델/런타임 배치 후 앱 시작 30분, 검사 반복 100회 안정성 확인 | 검사 실패가 앱 종료가 아니라 로그/결과 메시지로 남음 |
| O-004 | RTSP/NVR 실제 6채널 | 검증필요 | `CFG\Config.json` 기준으로 카메라 URL을 읽습니다. 일부 채널 수신은 확인했으나 6채널 장시간 스트리밍은 미검증입니다. | Top/Front/Back/Left/Right/Thickness 전 채널 연결, 옵션 UI 상태와 검사 UI 영상 확인 | 6채널 모두 연결됨 표시, 영상 갱신, 캡처 저장 가능 |
| O-005 | Camera 위치 매핑 | 검증필요 | 위치명은 `Top/Front/Back/Left/Right/Thickness` 기준입니다. 실제 물리 카메라와 포트/URL 매핑은 현장 확정 필요입니다. | 옵션 UI와 `CFG\Config.json`의 CAM 순서, RTSP URL, 위치명을 현장 기준으로 확정 | 기준 이미지 저장과 검사 이력 파일명이 올바른 위치명으로 저장 |
| O-006 | VLAD 결과 스키마 | 검증필요 | 2026-07-07 기준 AI 결과 문자열 parser와 `IndexNo -> MeasurementRegion.Id` 매핑은 구현했습니다. 예: `true,98,100,159,25,47`은 측정부1~4의 `100/159/25/47mm`로 해석합니다. | 실제 AI DLL이 결과 문자열을 `DetectText`로 반환하는지, 또는 TLV/별도 export인지 확인하고 실데이터로 파싱 검증 | `IsMatched`, `Confidence`, `MeasurementValues`가 실제 DLL 결과로 채워지고 이력/통계에 반영 |
| O-007 | AI 내부 측정/보정 | AI담당자확인 | 애플리케이션은 pixel-mm 보정이나 단위 변환을 하지 않고, AI가 반환한 숫자를 mm 측정값으로 비교합니다. | AI 담당자에게 카메라 해상도/렌즈/거리 조건에서 AI DLL 내부 측정값이 mm로 반환되는지 확인 | 같은 시편 반복 측정값이 mm 기준 허용 범위 안에 들어옴 |
| O-008 | 기준 이미지 정책 | 부분완료 | 기준 이미지가 없어도 사용자가 계속 진행을 선택하면 검사는 시도합니다. 양산 전 차단/허용 정책 확정 필요입니다. | 운영 기준에서 기준 이미지 필수 여부 결정 | 기준 이미지 없음 처리 정책이 요구사항/화면 메시지/테스트에 일치 |
| O-009 | History 보존 정책 | 검증필요 | `OUTPUT_PATH` 기준 `Inspection_Data\YYYY\MM\DD\HH\History, Image, Log` 구조와 사용자 확인 팝업, 설정기간 삭제, HDD 여유공간 기준 1일 단위 삭제, DB/폴더 삭제, 삭제 이력 로그, 삭제 후 여유공간 재확인 흐름을 구현했습니다. | 실제 저장장치에서 임계치 이하 상황을 만들어 팝업, 폴더 삭제, DB 삭제, `RetentionLog`, 여유공간 재확인을 검증 | 오래된 이미지/로그와 DB 이력이 같은 기간 기준으로 삭제되고 UI 메시지와 삭제 이력 로그가 일치 |
| O-010 | 배포 패키지 | 검증필요 | x64 Release 출력 폴더에 `CFG/DB/Native/VLAD/RuntimeData/Models` 복사 구조는 준비됐습니다. 외부 런타임 포함 여부는 별도 결정 필요입니다. | 클린 PC에서 Release 출력 폴더만 복사해 실행 테스트 | 개발툴 없이 앱 실행, DB 조회, 영상 수신, 검사 시작 가능 |
| O-011 | Git 대용량 파일 | 검토필요 | `Native\VLAD`, 모델, ZIP은 대용량입니다. GitHub 일반 저장소에 직접 올릴지 Git LFS/배포 패키지로 분리할지 결정 필요입니다. | GitHub 정책과 담당자 공유 방식 확정 | clone 후 개발 가능한 최소 세트와 별도 런타임 배포 절차 문서화 |
| O-012 | 통계 UI | 부분완료-검증필요 | Start/End 기간 조건과 검사실적/OK/NG/오류수 조회 흐름은 구현됐습니다. 실제 운영 KPI와 NG 원인 집계 기준은 추가 확인이 필요합니다. | 실제 검사 이력으로 기간 필터, OK/NG/오류 집계, 오류수 집계 검증 | 운영자가 필요한 기간/품번/분류/NG 사유 기준 통계를 일관되게 조회 |
| O-013 | CSV/Excel 운영 | 부분완료 | CSV 기준정보 일괄 반영은 있으나 현장 파일 포맷 최종안과 오류 리포트 형식은 추가 검증 필요입니다. | 실제 운영 CSV/Excel 샘플로 import/export 왕복 테스트 | DB 저장 전 오류 항목을 명확히 표시하고 정상 데이터만 반영 |
| O-014 | 측정부 1~5 독립 구조 | 검증필요 | 길이/너비/높이/두께 1세트 구조는 폐기했고, 측정부1~5 독립 구조로 변경했습니다. 실제 AI 결과 문자열, CSV, DB, 이력 표시가 같은 순서로 동작하는지 검증이 필요합니다. | 실제 품목 데이터로 측정부 추가/삭제, CSV 왕복, 검사 결과 매핑 검증 | 측정부 IndexNo 기준으로 DB/CSV/AI 결과/이력이 모두 같은 값을 가리킴 |
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
| C-011 | 2026-07-07 | AI 결과 문자열 `true/false,score,measurement1...N` parser, score 정규화, `IndexNo -> MeasurementRegion.Id` 매핑, mm 고정 비교 로직 구현 |
| C-012 | 2026-08-10 | `VLAD_HD_Inference_Mat` MAT JSON 방식에서 별도 Result 조회 없이 공용 JSON 버퍼로 요청/결과를 처리하는 구조 확인 및 요청 JSON 보완 완료 확인 |
| C-013 | 2026-08-10 | OCR 미등록 품번에서 등록 여부 확인 팝업 후 단일품목 등록 UI로 전환되고 OCR 품번이 신규 등록번호 입력란에 자동 입력되는 흐름 확인 |

## 관리 원칙

- 매 작업일마다 남은 항목의 상태를 `미완료`, `부분완료`, `검증필요`, `보류`, `완료` 중 하나로 정리합니다.
- 완료된 항목은 삭제하지 않고 `정리된 항목`으로 옮깁니다.
- 고객/AI 담당자 확인이 필요한 항목은 완료로 처리하지 않습니다.

## 2026-06-24 측정부 구조 변경 후 잔여 항목

- `O-016` 관리 코드에는 측정부 `IndexNo/항목/색상/기준값/허용오차/X1/Y1/X2/Y2/단위` 입력 DTO가 준비됐습니다. 이 DTO를 실제 VLAD 네이티브 함수에 넘길 함수명, 구조체 메모리 배치, 호출 시점을 AI 담당자와 확정해야 합니다. Crop과 추론 방식은 DLL 내부 책임입니다.
- `O-017` AI DLL 출력 구조 중 기본 문자열 계약은 2026-07-07에 `isMatched,score,measurement1...N`으로 정리했습니다. 남은 일은 실제 DLL 반환 위치(`detectText`, TLV, 별도 export) 확인과 코드 parser 연결입니다.
- `O-018` 실제 부품의 Thickness 기준 이미지에서 측정부 5개 누적 표시, 확대 좌표 정확도, 고해상도 이미지 성능을 현장 해상도로 검증해야 합니다.
- `O-019` 다중품목 CSV의 측정부1~5 항목/기준/허용/색상/X1/Y1/X2/Y2/단위 왕복 형식은 구현했습니다. 실제 운영 CSV로 내보내기-불러오기-DB 저장 후 값이 동일한지 현장 데이터 검증이 필요합니다.
- `O-020` 실제 6채널 카메라로 `현재6개저장`을 반복 실행해 Temp 초기화, 6장 미리보기, DB 저장 후 최종 경로/등록시간/품번_coordinate 파일 교체와 OldVer 미생성을 현장 장비 기준으로 검증해야 합니다.
- `O-021` 파일 시스템과 SQLite는 하나의 공통 트랜잭션을 사용할 수 없습니다. 디스크 오류 또는 SQLite 저장 실패를 강제로 발생시켜 Temp 유지와 최종 파일/DB 메타데이터의 복구 정책을 추가 검증해야 합니다.
- `O-022` 현재 `CFG\Config.json`의 CAM0~CAM5가 모두 같은 테스트 RTSP URL입니다. 실제 6대 카메라 설치 시 Top/Front/Back/Left/Right/Thickness별 고유 NVR 채널 URL을 확정하고 장시간 동시 재생 CPU/GPU/네트워크 사용량을 검증해야 합니다.
- `O-023` 검사 완료 시 측정 이미지 전체 화면과 좌측 상단 1/4 기준 이미지 인셋이 실제 4:3/6:5 카메라 해상도에서 주요 검사 영역을 가리지 않는지 현장 UI로 확인해야 합니다.
- `O-024` 외부 학습 프로세스의 StandardOutput/StandardError/Exited 수신과 `DONE + ExitCode 0` 이후 `VLAD_Unregistration -> VLAD_Ops_Ai_Env_Start -> RTSP 재등록` 흐름은 구현했습니다. 실제 `ai_train.bat`가 `DONE|100|완료`를 출력하는지와 새 모델로 추론 결과가 바뀌는지 현장 검증이 필요합니다.
- `O-025` 현재 설정의 `Tests\ToolsV2\ai_train.bat`는 확장자와 달리 MZ 헤더를 가진 PE 실행 파일이며, 메타데이터상 원본은 `ExternalTraining.Sample.exe`, 제품명은 `Training Process Sample`입니다. 현재 파일은 모니터 검증용 샘플이므로 실제 AI 학습 프로그램으로 교체하고 파일명/배포 형식을 AI 담당자와 확정해야 합니다.

## 2026-07-07 AI 결과 문자열 규격 반영 후 잔여 항목

- AI 결과 문자열 parser와 `IndexNo -> MeasurementRegion.Id` 매핑은 구현했습니다.
- 남은 일은 실제 AI DLL 결과가 `DetectText` 문자열로 들어오는지 확인하는 것입니다. 만약 TLV 또는 별도 export로 반환된다면 같은 parser에 연결되는 입력 위치만 바꾸면 됩니다.
- AI 측정값은 `mm` 고정으로 처리합니다. 앱은 `cm/m/pixel` 단위 변환을 하지 않고 기준값/허용오차와 직접 비교합니다.
- 측정부 값이 부족하면 해당 측정부는 `AI 측정값 없음`으로 NG 처리됩니다. 값이 더 많으면 현재 매핑 가능한 측정부 개수까지만 사용하고 초과 값은 비교에 사용하지 않습니다.

## 2026-08-04 VLAD HD API 1.1 준비 후 잔여 항목

| ID | 상태 | 내용 | 다음 조치 | 완료 기준 |
| --- | --- | --- | --- | --- |
| O-026 | 구현완료·DLL대기 | 일반 View/Thickness 분리 및 schema 1.1 최소 요청 JSON을 관리 코드에 적용했습니다. | 담당자 DLL과 헤더에서 함수명·인자 순서·UTF-8 규칙 대조 | 실제 DLL이 요청 JSON을 수신하고 6개 View 추론 완료 |
| O-027 | 구현완료·현장검증 | `viewJudge`와 측정부별 `judge`를 AI 판정으로 우선 적용하고 구형 결과 fallback을 보존했습니다. | PASS/FAIL/ERROR 실데이터로 최종 판정 및 이력 확인 | C# 재판정 없이 AI 결과와 UI/이력 판정이 일치 |
| O-028 | 구현완료·DLL대기 | 결과 버퍼 부족 시 `requiredResultJsonBytes` 기준으로 재할당 후 한 번 재호출합니다. | 64KiB 초과 모의 결과와 실제 DLL 응답으로 검증 | 결과 잘림·메모리 누수 없이 전체 JSON 파싱 |
| O-029 | 부분완료 | W/D/H는 현재 검사 화면까지 전달되지만 기존 SQLite 이력에는 별도 컬럼으로 저장하지 않습니다. | W/D/H 이력 보존 필요 여부 확정 후 DB 마이그레이션 결정 | 재실행 후 이력에서도 W/D/H 조회 가능하거나 미보존 정책 확정 |
| O-030 | 구현완료·DLL대기 | 유사도 검색은 기준 이상 후보를 AI 순위 그대로 최대 3개 사용합니다. | 신규 `VLAD_Search_Mat/Data` DLL로 0개/1개/3개/초과 후보 검증 | 각 View의 순위·품번·품명·Score가 DLL 결과와 일치 |
| O-031 | 검증필요 | 제품 전체 최종 판정은 현재 처리한 모든 View의 `viewJudge`가 PASS일 때 PASS입니다. | 카메라 Skip 및 일부 View ERROR 정책을 담당자와 확정 | 누락·고장·ERROR View의 최종 판정 규칙 확정 및 테스트 |
