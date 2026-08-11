# VLAD HD API 1.1 구현 준비 결과

- 작성일: 2026-08-04
- 적용 소스: `Codes/Version1_1_0_0/AI-Vision IO Inspector`
- 목표: AI 담당자 DLL 승인·배포 전에 C# 요청 생성, 결과 파싱, 판정 반영 경로를 준비합니다.
- 기준 사양: `vlad-hd-api-parameter-revision-proposal-2026-08-03.md`

## 1. 적용 범위

1. `VLAD_HD_Inference_Mat` 요청 JSON을 schema 1.1 최소 항목으로 생성합니다.
2. 일반 View 5개와 Thickness 입력을 분리합니다.
3. `VLAD_HD_InferenceData_Result`의 UTF-8 JSON을 파싱합니다.
4. AI의 `viewJudge`와 측정부별 `judge`를 C# 최종 판정에 사용합니다.
5. 유사도 검색은 AI가 반환한 기준 이상 후보를 최대 3개 사용합니다.
6. 구형 DLL용 단일 ID/문자열 결과 처리 경로는 호환 fallback으로 유지합니다.

## 2. 검사 요청

일반 View 요청은 다음 값만 전달합니다.

- `schemaVersion`
- `inspectionId`
- `partNo`
- `partName`
- `viewName`
- `scoreThreshold`

Thickness 요청에는 위 공통 값과 `measurementPoints`를 추가합니다. 측정부는 최대 5개이며 각 항목은 다음 값만 포함합니다.

- `measurementRegionId`
- `indexNo`
- `itemType`
- `lineColor`
- `nominalValue`
- `toleranceMin`
- `toleranceMax`
- `x1`, `y1`, `x2`, `y2`
- `unit`

이미지는 JSON 경로가 아니라 `rawData`의 `cv::Mat*`로 전달합니다.

## 3. 검사 결과

신규 결과에서 사용하는 주요 값은 다음과 같습니다.

- `status`: API 처리 상태
- `viewName`: 결과를 생성한 카메라 위치
- `viewJudge`: 현재 View의 AI 최종 판정
- `score`: 현재 View의 AI Score
- `dimensions`: 결과 이미지 하단 표시에 사용하는 W/D/H
- `measurements[].measuredValue`: 측정부 측정값
- `measurements[].judge`: 측정부별 AI 판정
- `failureReasons`: 실패 원인 코드 목록
- `message`: 사용자/로그 표시 메시지

`viewJudge`와 측정부 `judge`가 있는 신규 결과에서는 C#이 Score와 허용오차를 다시 비교해 판정을 바꾸지 않습니다. 구형 결과에서만 기존 로컬 비교를 수행합니다.

## 4. 결과 버퍼

1. C# 호출자가 UTF-8 결과 버퍼를 할당합니다.
2. DLL은 필요한 전체 byte 수를 `requiredResultJsonBytes`로 반환합니다.
3. 최초 버퍼가 부족하면 C#은 필요한 크기로 재할당하고 같은 결과 API를 한 번 더 호출합니다.
4. 파싱이 끝난 뒤 C#이 버퍼를 해제합니다.

이 규칙은 고정 길이 `StringBuilder` 잘림과 DLL/호출자 간 메모리 소유권 혼동을 방지합니다.

## 5. 유사도 검색

검색 요청은 `schemaVersion`, `viewName`, `scoreThreshold`, `topK=3`만 전달합니다. 검색 이미지는 `rawData`의 `cv::Mat*`입니다.

C#은 AI가 반환한 후보를 다시 Score 필터링하거나 재정렬하지 않습니다. DLL이 기준 이상 후보를 순위 순서로 최대 3개 반환한다는 계약을 따릅니다.

## 6. 검증 결과

- .NET Framework 4.7.2 / x64 Debug 전체 솔루션 빌드: 경고 0개, 오류 0개
- schema 1.1 검사 JSON 파싱: `viewName`, `viewJudge`, W/D/H, 측정부 `judge` 확인
- AI 판정 우선 적용: DB 허용 범위를 벗어난 모의 측정값이어도 AI `judge=PASS`이면 PASS 유지 확인
- 구형 결과 fallback: 기존 로컬 Score/허용오차 비교 코드 보존

## 7. 신규 DLL 수령 후 필수 검증

1. 실제 export 함수명과 P/Invoke 인자 순서를 헤더와 대조합니다.
2. 두 Vlad ID가 별도 등록 ID인지 같은 ID 재사용인지 확인합니다.
3. 일반 View와 Thickness 요청 JSON을 DLL 로그에서 확인합니다.
4. PASS/FAIL/ERROR 결과와 측정부 0~5개를 검증합니다.
5. 64KiB를 넘는 결과로 동적 버퍼 재호출을 검증합니다.
6. 유사도 후보 0개, 1개, 3개, 3개 초과를 검증합니다.
7. 6개 View 중 누락·고장·ERROR가 있을 때 제품 전체 판정 정책을 확정합니다.

## 8. 현재 제한

- 현재 배포 `VLAD_SDK.dll`이 신규 export를 제공하지 않으면 구형 fallback이 실행됩니다.
- W/D/H는 현재 검사 화면까지 전달되지만 기존 SQLite 검사 이력에는 별도 저장하지 않습니다.
- 신규 DLL을 받기 전에는 ABI와 실제 AI 판정 정확도를 완료로 처리하지 않습니다.
