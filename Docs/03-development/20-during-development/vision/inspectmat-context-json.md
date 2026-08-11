# InspectMat Context JSON

작성일: 2026-06-30
최종 수정: 2026-07-20

## 목적

> 2026-07-20 기준 목표 DLL의 JSON ABI와 필드 정의는 `vlad-hd-json-interface-contract-2026-07-20.md`를 우선한다. 이 문서는 현재 C#이 생성하는 Context JSON과 기존 4인자 `VLAD_Inference_Mat` 호환 상태를 기록한다.

`VladVisionInferenceEngine.InspectMat(IntPtr rawMatPointer, float threshold, int drawMode, VisionInspectionInput input, CapturedImage capturedImage)`는 검사 대상 부품의 기준정보와 현재 Mat 이미지 정보를 `inspectionContextJson` 문자열로 구성한다.

현재 C#은 `VLAD_HD_Inference_Mat(fullImageVladId, croppedImageVladId, rawData, threshold, drawMode, inspectionContextJson)` 경계까지 두 ID와 Context JSON을 준비한다. 다만 배포 VLAD SDK의 실제 export는 레거시 4인자 `VLAD_Inference_Mat(vladId, rawData, threshold, drawMode)` 흐름을 유지한다. 따라서 현재 native 호출에는 `fullImageVladId` 하나만 전달되고 Crop ID와 JSON은 로그/전환 준비 상태다. AI 담당자가 두 ID와 기준정보 JSON을 받는 DLL export를 추가하면 `VLAD_Ops_Ai.VLAD_HD_Inference_Mat(...)` 한 곳에서 새 네이티브 호출로 전환한다.

## JSON Format

`measurements`는 한 번의 검사에 등록된 측정부 목록이다. 현재 사양은 측정부 최대 5개이며, 측정부가 1개이면 객체 1개, 5개이면 객체 5개가 같은 배열에 들어간다. `InspectMat`은 촬영 이미지별로 호출되지만, 각 호출에 같은 측정부 목록과 현재 이미지 정보(`capturedViewType`, `capturedImagePath`)를 함께 전달한다.

```json
{
  "partNo": "01100-51430",
  "partName": "Sample Part",
  "categoryCode": "K26",
  "categoryDescription": "일반부품-구조그룹",
  "partType": "일반",
  "capturedViewType": "Thickness",
  "capturedImagePath": "C:\\...\\DB\\History\\2026\\06\\30\\...\\01100-51430_Thickness.png",
  "measurements": [
    {
      "measurementRegionId": 1,
      "indexNo": 1,
      "itemType": "길이",
      "viewType": "Thickness",
      "lineColor": "#FF0000",
      "nominalValue": 150.0,
      "toleranceMin": -0.5,
      "toleranceMax": 0.5,
      "tolerance": 0.5,
      "x1": 120.5,
      "y1": 240.0,
      "x2": 360.5,
      "y2": 240.0,
      "unit": "mm"
    }
  ]
}
```

## 필드 설명

| 필드 | 의미 |
| --- | --- |
| `partNo` | 품번 |
| `partName` | 품명 |
| `categoryCode` | 분류코드 |
| `categoryDescription` | 분류설명 |
| `partType` | 구분 |
| `capturedViewType` | 현재 Mat로 넘기는 이미지 위치. `Top`, `Front`, `Back`, `Left`, `Right`, `Thickness` 중 하나 |
| `capturedImagePath` | 현재 Mat 원본 이미지 파일 경로 |
| `measurements[].measurementRegionId` | DB 측정부 ID. 현재 Application의 기준값 비교는 이 ID로 연결된다. 단, 2026-07-07 결과 문자열은 ID를 포함하지 않으므로 `indexNo` 순서값을 이 ID로 다시 매핑해야 한다. |
| `measurements[].indexNo` | 화면에 표시되는 측정부 번호 |
| `measurements[].itemType` | 길이/너비/높이/두께/미설정 등 측정부 항목 |
| `measurements[].viewType` | 측정부 위치 기준 이미지 |
| `measurements[].lineColor` | 작업자가 지정한 측정부 선 색상 |
| `measurements[].nominalValue` | 기준값 |
| `measurements[].toleranceMin` | 하한 허용오차. 내부 값은 음수로 전달한다. |
| `measurements[].toleranceMax` | 상한 허용오차. 내부 값은 양수로 전달한다. |
| `measurements[].tolerance` | `abs(toleranceMin)`과 `abs(toleranceMax)` 중 큰 값. 대칭 허용값만 필요한 AI 쪽 편의 필드다. |
| `measurements[].x1/y1/x2/y2` | Thickness 기준 이미지에서 작업자가 표시한 시작/끝 좌표 |
| `measurements[].unit` | 기준 단위. 현재 사양은 `mm` 고정 |

## 측정부 매핑 규칙

- AI 입력 JSON에는 `measurementRegionId`와 `indexNo`를 모두 전달한다.
- `x1`, `y1`, `x2`, `y2`는 해당 측정부의 시작점/끝점 좌표다. 좌표가 등록된 측정부는 AI 쪽에서 해당 좌표를 기준으로 측정 위치를 찾을 수 있다.
- `viewType`은 좌표가 표시된 기준 이미지 위치다. 현재 측정부 위치 표시는 Thickness 이미지를 기준으로 관리하므로 일반적으로 `Thickness`가 들어간다.
- `capturedViewType`은 현재 `VLAD_Inference_Mat`에 넘기는 Mat 이미지 위치다. AI는 필요하면 `capturedViewType`과 `measurements[].viewType`이 같은 항목만 사용해도 된다.
- 2026-07-07 기준 AI 반환 문자열에는 `measurementRegionId`가 포함되지 않는다. 따라서 애플리케이션은 `measurements[]`를 `indexNo` 오름차순으로 정렬한 뒤, 반환된 측정값을 같은 순서로 매핑해야 한다.
- 내부 `MeasurementService`는 `MeasurementRegion.Id`를 키로 비교하므로, 문자열 파서 또는 mapper는 `IndexNo -> MeasurementRegion.Id` 변환을 끝낸 뒤 `AiInferenceResult.MeasurementValues[region.Id]`를 채워야 한다.

## AI Result Format

2026-07-07 기준 AI 담당자와 맞출 결과 문자열 계약은 다음과 같다.

```text
isMatched,score,measurement1,measurement2,...,measurementN
```

예를 들어 측정부가 4개이면 다음처럼 전달된다.

```text
true,98,100,159,25,47
```

| 토큰 | 의미 | 내부 매핑 |
| --- | --- | --- |
| `isMatched` | 이미지 AI 검사 OK/PASS 여부. `true/false`, `OK/NG`, `PASS/FAIL`은 파서에서 같은 의미로 처리한다. | `VisionInspectionOutput.IsMatched`, `AiInferenceResult.IsMatched` |
| `score` | AI 판정 score. `98`처럼 0~100 범위로 올 수 있다. | 내부 `Confidence`는 0~1로 정규화하고, 화면/로그 표시 시 필요하면 0~100으로 표시한다. |
| `measurement1..N` | 측정부 IndexNo 순서의 실제 측정값. 단위는 현재 사양상 `mm` 고정이며 `100`은 `100mm`로 해석한다. | `MeasurementRegion.IndexNo` 순서로 찾아 해당 `MeasurementRegion.Id`의 측정값으로 저장한다. |

처리 규칙은 다음과 같다.

- 측정부 값은 `part.MeasurementRegions`의 `IndexNo` 오름차순과 같은 순서로 온다고 본다.
- AI가 값 4개를 반환했고 DB 측정부가 4개이면 각각 측정부1~4에 매핑한다.
- AI 값이 부족하면 부족한 측정부는 `AI 측정값 없음`으로 처리한다.
- AI 값이 더 많으면 초과 값은 검사 로그에 남기고 비교에서는 무시한다.
- 문자열 파싱 실패 또는 `isMatched=false`는 이미지 AI 검사 NG로 본다. 단, 측정값이 같이 온 경우에는 이력 분석을 위해 기준값 비교 결과도 남긴다.

## 현재 코드 검토 결과

- `AiInferenceResult`, `VisionInspectionOutput`은 `IsMatched`, `Confidence`, `Measurements`를 담을 수 있어 출력 모델 자체는 사용 가능하다.
- `VladInferenceResultParser`는 `DetectText`를 보관하고, `VladMeasurementMapper`가 `true,98,...` 문자열을 구조화한다.
- `VladMeasurementMapper`는 측정부 값을 `MeasurementRegion.IndexNo` 오름차순으로 읽어 `MeasurementRegion.Id`에 매핑한다.
- `VisionAiInferenceService`와 `MeasurementService`는 `MeasurementRegionId` 기준으로 값을 넘기고 비교한다.
- `MeasurementService`는 AI가 반환한 숫자를 mm 측정값으로 보고, 별도 단위 변환 없이 DB 기준값/허용오차와 비교한다.
## 검사 흐름

```text
CaptureAll(part)
  -> 이미지 획득

RunAiInspection(part, capturedImages)
  -> VLAD 이미지 AI 검사 결과와 측정정보 획득

CompareReference(part, aiResults)
  -> DB 기준값/허용값과 AI 측정값 비교

BuildFinalInspectionResult(part, aiResults, compareResults)
  -> 이미지 AI 결과와 기준값 비교 결과를 함께 보고 최종 OK/NG 생성
```

주의할 점은 이미지 AI 검사와 기준값 비교가 별도 버튼이나 별도 검사 단계가 아니라, 하나의 검사 실행 안에서 함께 처리된다는 점이다.
