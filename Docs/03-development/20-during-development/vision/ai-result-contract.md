# AI Result Contract

작성일: 2026-07-07
최종 수정: 2026-07-20

## 목적

> 이 문서는 현재 C#이 파싱하는 구형 CSV 결과 문자열 계약이다. AI DLL이 JSON 결과 export를 제공할 때의 목표 계약은 `vlad-hd-json-interface-contract-2026-07-20.md`를 우선한다.

이 문서는 VLAD/AI DLL에서 애플리케이션으로 반환하는 검사 결과 문자열의 현재 계약을 정리한다. 입력 기준정보 JSON은 `inspectmat-context-json.md`를 기준으로 하고, 이 문서는 AI가 반환하는 결과값 해석에 집중한다.

유사 제품 후보의 순위, 품번, 품명, 유사도 Score는 이 검사 결과 문자열에 포함하지 않는다. 해당 기능은 `VLAD_Search_Data`의 JSON 결과를 사용하며, 상세 계약은 `vlad-similarity-search-dll-contract-2026-07-16.md`를 따른다.

## 결과 문자열

AI 결과는 콤마(`,`)로 구분된 문자열로 받는다.

```text
isMatched,score,measurement1,measurement2,...,measurementN
```

예시:

```text
true,98,100,159,25,47
```

위 예시는 다음 의미다.

| 위치 | 값 | 의미 |
| --- | --- | --- |
| 0 | `true` | 이미지 AI 검사 OK/PASS |
| 1 | `98` | AI score 98점 |
| 2 | `100` | 측정부1 측정값, `100mm`로 해석 |
| 3 | `159` | 측정부2 측정값, `159mm`로 해석 |
| 4 | `25` | 측정부3 측정값, `25mm`로 해석 |
| 5 | `47` | 측정부4 측정값, `47mm`로 해석 |

## 매핑 규칙

- `isMatched`는 `true/false`를 기본으로 한다. 필요하면 `OK/NG`, `PASS/FAIL`, `1/0`도 파서에서 허용할 수 있다.
- `score`가 `98`처럼 1보다 큰 값이면 0~100 점수로 보고 내부 `Confidence`에는 `0.98`로 정규화한다.
- 측정부 값은 현재 부품의 `MeasurementRegion.IndexNo` 오름차순과 같은 순서로 온다.
- 측정부 값의 단위는 `mm` 고정이다. 예를 들어 AI가 `100`을 반환하면 애플리케이션은 `100mm`로 본다.
- 애플리케이션은 AI 측정값에 대해 `cm`, `m`, pixel-mm 변환을 수행하지 않는다. 보정과 실제 길이 산출은 AI DLL 내부 책임이다.
- DB 기준값 비교에는 `MeasurementRegion.Id`가 필요하므로, 파서 또는 mapper는 `IndexNo` 순서값을 해당 region의 `Id`로 변환해야 한다.

## 예외 처리 규칙

| 상황 | 처리 |
| --- | --- |
| 문자열이 비어 있음 | AI 결과 없음으로 검사 실패 처리 |
| `isMatched` 파싱 실패 | 이미지 AI 검사 NG 및 로그 기록 |
| `score` 파싱 실패 | `Confidence=0`, 로그 기록 |
| 측정부 값 부족 | 부족한 측정부는 `AI 측정값 없음`으로 NG 처리 |
| 측정부 값 초과 | 초과 값은 로그에 남기고 기준값 비교에서는 무시 |
| 숫자 파싱 실패 | 해당 측정부만 `AI 측정값 파싱 실패`로 NG 처리 |

## 현재 코드와의 차이

현재 코드에는 결과를 담을 모델은 준비되어 있다.

| 모델 | 준비 상태 |
| --- | --- |
| `VisionInspectionOutput.IsMatched` | 사용 가능 |
| `VisionInspectionOutput.Confidence` | 사용 가능 |
| `VisionInspectionOutput.Measurements` | 사용 가능 |
| `AiInferenceResult.IsMatched` | 사용 가능 |
| `AiInferenceResult.Confidence` | 사용 가능 |
| `AiInferenceResult.MeasurementValues` | 사용 가능 |

2026-07-07 현재 `VladMeasurementMapper`가 `DetectText`에서 `true,98,...` 형식을 파싱한다.

1. 첫 번째 토큰은 `IsMatched`로 변환한다.
2. 두 번째 score는 0~100이면 내부 `Confidence` 0~1 값으로 정규화한다.
3. 이후 측정부 값은 `MeasurementRegion.IndexNo` 오름차순으로 읽어 `MeasurementRegion.Id` 키의 `MeasurementValues`로 변환한다.
4. `MeasurementService.CompareMeasurements()`는 단위 변환 없이 AI 측정값(mm)과 DB 기준값/허용오차(mm)를 비교한다.

남은 항목은 실제 AI DLL이 반환하는 문자열 위치가 `DetectText`가 맞는지와, 실제 현장 결과값이 이 계약을 안정적으로 따르는지 검증하는 일이다.

## 유사도 검색 결과와의 구분

`Detect_Str`은 검사 판정과 측정부 값만 담는 고정 순서 CSV다. 후보 수가 가변적이고 품번/품명 문자열을 포함하는 유사도 검색 결과는 CSV에 결합하지 않는다.

```text
일반 검사: VLAD_Custom_InferenceData_V1 -> Detect_Str CSV
유사도 검색: VLAD_Search_Data -> resultJson JSON
```

현재 프로그램은 유사도 JSON에서 `viewName`, `candidates[].rank`, `candidates[].partNo`, `candidates[].partName`, `candidates[].score`를 파싱해 후보 DataGrid에 표시한다.
