# InspectMat Context JSON

작성일: 2026-06-30
최종 수정: 2026-07-02

## 목적

`VladVisionInferenceEngine.InspectMat(IntPtr rawMatPointer, float threshold, int drawMode, VisionInspectionInput input, CapturedImage capturedImage)`는 검사 대상 부품의 기준정보와 현재 Mat 이미지 정보를 `inspectionContextJson` 문자열로 구성한다.

현재 VLAD SDK의 실제 export는 기존 4인자 `VLAD_Inference_Mat(vladId, rawData, threshold, drawMode)` 흐름을 유지한다. AI 담당자가 기준정보 JSON을 받는 DLL export를 추가하면 `VLAD_Ops_Ai.VLAD_Inference_Mat(..., inspectionContextJson)` 한 곳에서 새 네이티브 호출로 교체한다.

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
| `measurements[].measurementRegionId` | DB 측정부 ID. AI가 측정값을 반환할 때 이 ID로 돌려주면 기준값 비교와 바로 연결된다. |
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

- AI는 `measurements[]`를 순회하면서 `measurementRegionId` 또는 `indexNo`로 측정부를 구분한다.
- `x1`, `y1`, `x2`, `y2`는 해당 측정부의 시작점/끝점 좌표다. 좌표가 등록된 측정부는 AI 쪽에서 해당 좌표를 기준으로 측정 위치를 찾을 수 있다.
- `viewType`은 좌표가 표시된 기준 이미지 위치다. 현재 측정부 위치 표시는 Thickness 이미지를 기준으로 관리하므로 일반적으로 `Thickness`가 들어간다.
- `capturedViewType`은 현재 `VLAD_Inference_Mat`에 넘기는 Mat 이미지 위치다. AI는 필요하면 `capturedViewType`과 `measurements[].viewType`이 같은 항목만 사용해도 된다.
- 측정 결과를 반환할 때는 `measurementRegionId`를 같이 돌려주는 구조가 가장 안전하다. 그래야 품목마다 측정부1의 의미가 달라도 DB 기준값 비교와 정확히 연결된다.

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
