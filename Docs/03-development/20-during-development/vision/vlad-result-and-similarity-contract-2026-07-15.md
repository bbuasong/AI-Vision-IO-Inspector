# VLAD 검사 결과 및 유사도 검색 계약

작성일: 2026-07-15  
최종 수정: 2026-07-20

## 적용한 변경

- 검사 화면의 기준이미지를 두 번 클릭하면 단일 팝업 창에서 Top, Front, Back, Left, Right, Thickness 이미지를 이전/다음으로 확인한다.
- Search DB 선택 부품이 변경되면 열려 있는 팝업도 새 부품 기준이미지로 갱신한다.
- 측정부가 있고 `{품번}_coordinate.png`가 존재하면 검사 화면과 Thickness 기준이미지 자리에 coordinate 이미지를 사용한다. 없으면 일반 Thickness 이미지를 사용한다.
- 옵션 > 학습 화면에 검사 Pass/Fail Score(기본 95), 단일품목 유사값(기본 99)을 추가하고 `CFG\Config.json`의 활성 `CUSTOM.HD` 섹션에 저장한다.
- 검사 완료 화면에는 실제 AI Score가 있는 경우 `Score: 값 / 기준값`을 PASS/FAIL 색상으로 표시한다. 현재 DLL이 Score를 반환하지 않으면 `Score: -`로 표시한다.
- Event Grid는 가로/세로 스크롤을 모두 사용한다.
- 결과 이미지 하단 W/H/D 표시 영역을 추가했다. 현재 DLL 반환 계약에 치수 값이 없어 `W: - H: - D: -`로 표시한다.

## VLAD 입력 계약: 목표 HD ABI와 현재 DLL

목표 HD DLL의 관리 진입점은 전체 이미지와 Crop 이미지의 등록 ID를 모두 받는다.

```csharp
VLAD_HD_Inference_Mat(
    fullImageVladId,
    croppedImageVladId,
    rawData,
    threshold,
    drawMode,
    inspectionContextJson)
```

- `fullImageVladId`: 전체 크기 이미지 모델용 `VLAD_Custom_Registration` 반환 ID다.
- `croppedImageVladId`: Crop 이미지 모델용 별도 등록 ID다.
- 두 ID는 앱 시작 시 함께 생성하고 학습 후 재초기화 및 종료 시 함께 해제한다.
- RTSP callback은 현재 `VLAD_Ops_RTSP`의 활성 ID 캐시 제약 때문에 전체 이미지 ID만 등록한다.

그러나 현재 확인된 배포 `VLAD_SDK.dll` native export는 아래 **레거시 단일 ID 4인자**다.

```csharp
VLAD_Inference_Mat(fullImageVladId, rawData, threshold, drawMode)
```

따라서 현행 C#의 `VLAD_HD_Inference_Mat(fullImageVladId, croppedImageVladId, ...)` 호환 래퍼는 두 ID를 받지만, 실제 레거시 native 호출에는 전체 이미지 ID만 전달한다. `inspectionContextJson`도 현재는 생성 및 로그 기록만 하며 DLL에 전달되지 않는다. AI 담당자가 두 ID와 JSON을 받는 HD export 또는 동등한 등록 API를 제공해야 실제 AI 입력이 된다.

### 생성하는 Context JSON

```json
{
  "schemaVersion": "1.0",
  "requestType": "Inspection",
  "inspectionId": "20260715_103000_001",
  "partNo": "01100-51430",
  "productName": "Sample Part",
  "categoryNo": "K26",
  "categoryName": "일반부품-구조그룹",
  "partType": "일반",
  "viewName": "Thickness",
  "captureTime": "2026-07-15T10:30:00.0000000+09:00",
  "capturedImagePath": "C:\\Inspection_Data\\...\\Thickness.png",
  "scoreThreshold": 95.0,
  "measurementPoints": [
    {
      "measurementRegionId": 1,
      "indexNo": 1,
      "itemType": "길이",
      "viewType": "Thickness",
      "lineColor": "#FF0000",
      "nominalValue": 150.0,
      "toleranceMin": -0.5,
      "toleranceMax": 0.5,
      "x1": 120.5,
      "y1": 240.0,
      "x2": 360.5,
      "y2": 240.0,
      "unit": "mm"
    }
  ]
}
```

한 검사에 측정부가 1~5개이면 `measurementPoints` 배열에 1~5개 객체가 함께 들어간다.

## 현재 실제 VLAD 결과 계약

현재 애플리케이션이 실제로 파싱하는 문자열은 다음과 같다.

```text
isMatched,score,measurement1,measurement2,...,measurementN
```

예: `true,98,100,159,25,47`

- `isMatched`: 이미지 정합 여부.
- `score`: 0~100 범위 Score. `INSPECTION_PASS_SCORE_THRESHOLD` 이상이어야 Score 조건을 통과한다.
- `measurement1..N`: `MeasurementRegion.IndexNo` 오름차순과 동일한 mm 측정값.
- 최종 PASS는 이미지 정합, Score 조건(Score가 제공된 경우), 모든 측정부 기준값/허용오차 비교를 함께 통과해야 한다.

## AI 담당자와 확정할 결과 JSON

```json
{
  "schemaVersion": "1.0",
  "resultType": "InspectionResult",
  "inspectionId": "20260715_103000_001",
  "partNo": "01100-51430",
  "partName": "Sample Part",
  "viewName": "Thickness",
  "captureTime": "2026-07-15T10:30:00+09:00",
  "overallJudge": "PASS",
  "score": 97.23,
  "scoreThreshold": 95.0,
  "dimensions": {
    "width": 100.0,
    "height": 120.0,
    "depth": 30.0,
    "unit": "mm"
  },
  "measurements": [
    {
      "indexNo": 1,
      "measuredValue": 10.25,
      "specValue": 10.0,
      "lowerTolerance": -0.1,
      "upperTolerance": 0.2,
      "judge": "PASS",
      "unit": "mm"
    }
  ],
  "message": "Inspection completed successfully."
}
```

## 단일품목 유사도 검색

현재 UI에는 `유사도 체크`, `결과 초기화`, 후보 목록 DataGrid를 준비했다. 하지만 제공된 DLL/샘플에는 아래 export가 없다.

```text
VLAD_Search_Mat
VLAD_Search_Data
```

일반 검사인 `VLAD_Inference_Mat`으로 DB 유사 제품 검색을 대체하면 결과 의미가 달라지므로 호출하지 않는다. 현재는 필요한 SDK 기능이 없다는 메시지를 표시한다.

AI 담당자가 제공해야 할 계약은 아래와 같다.

```json
{
  "schemaVersion": "1.0",
  "requestType": "SimilaritySearch",
  "searchId": "20260715_103000_001",
  "sourcePartNo": "NEW_ITEM",
  "viewName": "Top",
  "scoreThreshold": 99.0,
  "topK": 3,
  "targetDatabase": "ReferenceImageDB"
}
```

반환값은 위치별 상위 3개 후보의 `rank`, `partNo`, `partName`, `score`를 포함해야 한다. UI에는 기준값 이상인 후보만 표시한다.

## 남은 작업

1. AI DLL의 Context 수신 API와 검사 결과 JSON 반환 형식을 확정한다.
2. JSON의 `dimensions`와 측정부 결과를 현재 모델로 파싱해 W/H/D 표시를 실제 값으로 채운다.
3. `VLAD_Search_Mat`, `VLAD_Search_Data`가 제공되면 단일품목 유사도 검색 후보를 DataGrid에 연결한다.
4. 실제 6채널/모델 환경에서 Score 기준값 95와 유사도 기준값 99의 적정성을 검증한다.
