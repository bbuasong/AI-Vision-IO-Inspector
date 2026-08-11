# VLAD HD API 파라미터 변경 사양

작성일: 2026-08-03  
최종 수정일: 2026-08-04  
문서 버전: 1.1  
상태: AI 담당자 검토용 변경 사양

## 1. 작성 배경

2026-07-27 및 2026-08-03 `leekh` 회신에서 다음 사항을 재검토해 달라는 의견을 받았다.

1. SDK가 실제로 사용하는 항목만 전달한다.
2. 일반 이미지 View와 Thickness 측정부 입력을 구분한다.
3. 가변 JSON의 메모리 할당/해제 주체와 포인터 유효기간을 명확히 한다.
4. Request와 Result의 동일 의미 필드는 같은 이름을 사용한다.

본 문서는 아래 네 API에 적용할 파라미터와 JSON 계약 변경 사양을 정의한다.

- `VLAD_HD_Inference_Mat`
- `VLAD_HD_InferenceData_Result`
- `VLAD_Search_Mat`
- `VLAD_Search_Data`

## 2. 핵심 변경 사양

### 2.1 View 구분

- 일반 이미지 검사: `Top`, `Front`, `Back`, `Left`, `Right`
- 이미지 및 측정부 검사: `Thickness`
- API 함수는 분리하지 않고 `viewName`으로 처리 흐름을 구분한다.
- 일반 이미지 검사에는 `measurementPoints`를 전달하지 않는다.
- Thickness 검사에만 현재 View에서 사용하는 `measurementPoints`를 전달한다.

### 2.2 판정 주체

- 이미지 Score 기준 PASS/FAIL은 AI DLL이 판단한다.
- Thickness 측정부 기준값/허용오차 PASS/FAIL도 AI DLL이 판단한다.
- C# 프로그램은 AI 결과를 동일 기준으로 다시 계산하지 않는다.
- C# 프로그램은 6개 View에서 받은 AI 판정값을 검사 화면과 이력에 표시하고, View 결과를 제품 검사 한 건으로 묶는다.
- 제품 단위 최종 결과는 AI가 반환한 View 판정 중 `FAIL` 또는 `ERROR`가 있는지만 집계한다. Score 및 허용오차를 이용한 독립 재판정은 하지 않는다.

### 2.3 이름 통일

- 품명 필드는 Request와 Result 모두 `partName`으로 통일한다.
- 기존 `productName`은 신규 계약에서 사용하지 않는다.
- 촬영 위치는 모든 API에서 `viewName`으로 통일한다.
- 측정부 목록은 입력에서는 `measurementPoints`, 결과에서는 `measurements`로 사용한다. 입력 기준정보와 출력 측정결과의 의미가 다르므로 이름을 구분한다.

### 2.4 Score 전달

- `VLAD_HD_Inference_Mat`의 JSON에 `scoreThreshold`를 전달한다.
- `VLAD_Search_Mat`의 JSON에도 `scoreThreshold`를 전달한다.
- AI DLL은 전달받은 기준값으로 PASS/FAIL 또는 검색 후보 포함 여부를 판단한다.
- C#은 결과 Score와 기준 Score를 다시 비교해 판정을 변경하지 않는다.
- 기존 네이티브 인자 `threshold`는 VLAD 내부 추론 제어값으로 유지한다. 업무 판정값인 `scoreThreshold`와 같은 값으로 간주하지 않는다.

### 2.5 이미지 전달 방식

- 검사 및 유사도 검색 대상 이미지는 JSON의 파일 경로가 아니라 `rawData`로 전달한다.
- `rawData`는 호출 시점에 유효한 OpenCV `cv::Mat*` 포인터다.
- 이미지의 폭, 높이, 채널 및 픽셀 정보는 `cv::Mat`에서 읽는다.
- JSON은 부품, View, 판정 기준 및 측정부와 같은 업무 컨텍스트만 전달한다.
- DLL은 함수가 반환된 뒤 `rawData` 포인터를 보관하거나 해제하지 않는다.
- C#은 네이티브 함수가 반환될 때까지 원본 `Mat`의 수명을 유지한다.

### 2.6 두 Vlad ID의 역할

| 인자 | 역할 |
| --- | --- |
| `fullImageVladId` | 전체 이미지 학습 모델을 사용하는 VLAD 등록 ID |
| `croppedImageVladId` | Crop 이미지 학습 모델을 사용하는 VLAD 등록 ID |

- 두 ID는 프로그램 초기화 단계에서 각각 등록하고 검사마다 새로 생성하지 않는다.
- 등록된 두 ID는 검사, 유사도 검색 및 학습 연계 과정에서 동일한 수명주기로 재사용한다.
- 두 ID 중 하나라도 유효하지 않으면 네이티브 검사를 시작하지 않고 오류 결과를 반환한다.

## 3. 메모리 소유권 계약

가변 JSON을 사용하더라도 DLL이 문자열 메모리를 소유하거나 반환하지 않도록 아래 규칙을 적용한다.

1. 입력 JSON은 C# 호출자가 널 종료 UTF-8 버퍼로 할당한다.
2. DLL은 함수가 실행되는 동안에만 입력 포인터를 읽는다.
3. DLL은 입력 포인터를 보관하거나 직접 해제하지 않는다.
4. 함수 반환 후 C# 호출자가 입력 버퍼를 해제한다.
5. 결과 JSON은 C# 호출자가 `resultJsonUtf8` 버퍼와 `resultJsonCapacity`를 제공한다.
6. DLL은 제공된 용량을 초과해 기록하지 않는다.
7. 결과 버퍼가 부족하면 DLL은 `requiredResultJsonBytes`를 채우고 버퍼 부족 코드를 반환한다.
8. C#은 필요한 크기로 한 번만 다시 할당해 결과 API를 재호출한다.
9. `resultJsonCapacity`와 `requiredResultJsonBytes`는 널 종료 문자를 포함한 UTF-8 byte 단위다.
10. 결과 API의 정수 반환값은 성공, 버퍼 부족, 처리 오류를 서로 구분할 수 있어야 한다.

이 구조에서는 SDK가 결과 문자열용 메모리를 신규 할당해 C#에 반환하지 않으므로 SDK/C# 사이의 `Free` 책임과 Memory Leak 위험을 줄일 수 있다.

## 4. VLAD_HD_Inference_Mat

### 4.1 네이티브 서명

```c
void* VLAD_HD_Inference_Mat(
    void* fullImageVladId,
    void* croppedImageVladId,
    void* rawData,
    float threshold,
    int drawMode,
    const char* inspectionContextJsonUtf8);
```

함수 서명은 유지하고 JSON 항목만 최소화한다.

| 인자 | 설명 |
| --- | --- |
| `fullImageVladId` | 전체 이미지 모델용 등록 ID |
| `croppedImageVladId` | Crop 이미지 모델용 등록 ID |
| `rawData` | 현재 검사 이미지의 `cv::Mat*` 포인터 |
| `threshold` | VLAD 내부 추론 제어값 |
| `drawMode` | VLAD 결과 이미지 Drawing 제어값 |
| `inspectionContextJsonUtf8` | 널 종료 UTF-8 검사 컨텍스트 JSON |

### 4.2 일반 이미지 View 입력

적용 View: `Top`, `Front`, `Back`, `Left`, `Right`

```json
{
  "schemaVersion": "1.1",
  "inspectionId": "20260803_090000_001",
  "partNo": "01100-51430",
  "partName": "Sample Part",
  "viewName": "Top",
  "scoreThreshold": 95.00
}
```

### 4.3 Thickness 입력

```json
{
  "schemaVersion": "1.1",
  "inspectionId": "20260803_090000_001",
  "partNo": "01100-51430",
  "partName": "Sample Part",
  "viewName": "Thickness",
  "scoreThreshold": 95.00,
  "measurementPoints": [
    {
      "measurementRegionId": 1,
      "indexNo": 1,
      "itemType": "길이",
      "lineColor": "#FF0000",
      "nominalValue": 150.00,
      "toleranceMin": -0.50,
      "toleranceMax": 0.50,
      "x1": 120.50,
      "y1": 240.00,
      "x2": 360.50,
      "y2": 240.00,
      "unit": "mm"
    }
  ]
}
```

`measurementPoints`는 최대 5개이며, `viewName == Thickness`일 때만 전달한다.

| 측정부 필드 | 설명 |
| --- | --- |
| `measurementRegionId` | DB 측정부 레코드 식별자 |
| `indexNo` | 화면과 결과에서 사용하는 측정부 순번. 1~5 |
| `itemType` | 길이, 너비, 높이, 두께 등 이력 표시용 측정 항목 |
| `lineColor` | 좌표 이미지에서 측정부를 식별하는 `#RRGGBB` 색상 |
| `nominalValue` | 기준값 |
| `toleranceMin` | 기준값에 더하는 하한 허용값 |
| `toleranceMax` | 기준값에 더하는 상한 허용값 |
| `x1`, `y1` | 측정부 시작점 좌표 |
| `x2`, `y2` | 측정부 끝점 좌표 |
| `unit` | 측정 단위. 현재 `mm` 고정 |

### 4.4 입력 필드 분류

| 필드 | 일반 View | Thickness | 사용 목적 |
| --- | --- | --- | --- |
| `schemaVersion` | 필수 | 필수 | 계약 버전 |
| `inspectionId` | 필수 | 필수 | 6개 View 검사 연계 |
| `partNo` | 필수 | 필수 | 부품/모델 식별 |
| `partName` | 필수 | 필수 | 품명 식별. `productName` 사용 금지 |
| `viewName` | 필수 | 필수 | 카메라 위치 및 처리 분기 |
| `scoreThreshold` | 필수 | 필수 | AI 이미지 PASS/FAIL 기준 |
| `measurementPoints` | 전달하지 않음 | 필요 시 필수 | 측정부 위치 및 판정 기준 |

### 4.5 신규 계약에서 제외하는 입력 필드

| 제외 필드 | 제외 이유 |
| --- | --- |
| `requestType` | 함수 이름으로 검사 요청임을 구분할 수 있음 |
| `categoryNo`, `categoryName`, `partType` | AI가 사용하지 않는 업무/UI 정보 |
| `captureTime` | C# 검사 이력에서 관리 |
| `capturedImagePath` | 실제 영상은 `rawData` Mat으로 전달 |
| `viewType` | Thickness 측정부만 전달하므로 최상위 `viewName`과 중복 |
| `tolerance` | `toleranceMin`, `toleranceMax`와 중복 |

위 항목은 신규 계약에서 전달하지 않는다.

## 5. VLAD_HD_InferenceData_Result

### 5.1 네이티브 서명

```c
int VLAD_HD_InferenceData_Result(
    void* fullImageVladId,
    void* croppedImageVladId,
    void* detectData,
    void* rawData,
    void* classCount,
    char* resultJsonUtf8,
    int resultJsonCapacity,
    int* requiredResultJsonBytes,
    const char* customParameterUtf8);
```

| 인자 | 설명 |
| --- | --- |
| `fullImageVladId` | 전체 이미지 모델용 등록 ID |
| `croppedImageVladId` | Crop 이미지 모델용 등록 ID |
| `detectData` | `VLAD_HD_Inference_Mat` 반환 포인터 |
| `rawData` | 결과와 연결된 원본 `cv::Mat*` 포인터 |
| `classCount` | DLL 계약에 따른 클래스 정보 포인터 |
| `resultJsonUtf8` | C#에서 할당한 결과 JSON byte 버퍼 |
| `resultJsonCapacity` | 널 종료 문자를 포함한 버퍼 byte 용량 |
| `requiredResultJsonBytes` | 결과에 필요한 전체 UTF-8 byte 수 |
| `customParameterUtf8` | 추가 사용자 파라미터. 사용하지 않으면 빈 UTF-8 문자열 |

### 5.2 일반 이미지 View 결과

```json
{
  "schemaVersion": "1.1",
  "inspectionId": "20260803_090000_001",
  "partNo": "01100-51430",
  "partName": "Sample Part",
  "viewName": "Top",
  "status": "SUCCESS",
  "viewJudge": "PASS",
  "score": 97.23,
  "scoreThreshold": 95.00,
  "dimensions": {
    "width": 100.00,
    "depth": 30.00,
    "height": 120.00,
    "unit": "mm"
  },
  "message": ""
}
```

### 5.3 Thickness 결과

```json
{
  "schemaVersion": "1.1",
  "inspectionId": "20260803_090000_001",
  "partNo": "01100-51430",
  "partName": "Sample Part",
  "viewName": "Thickness",
  "status": "SUCCESS",
  "viewJudge": "PASS",
  "imageJudge": "PASS",
  "measurementJudge": "PASS",
  "score": 97.23,
  "scoreThreshold": 95.00,
  "dimensions": {
    "width": 100.00,
    "depth": 30.00,
    "height": 120.00,
    "unit": "mm"
  },
  "measurements": [
    {
      "measurementRegionId": 1,
      "indexNo": 1,
      "measuredValue": 150.10,
      "judge": "PASS"
    }
  ],
  "failureReasons": [],
  "message": ""
}
```

### 5.4 오류 결과

```json
{
  "schemaVersion": "1.1",
  "inspectionId": "20260803_090000_001",
  "partNo": "01100-51430",
  "partName": "Sample Part",
  "viewName": "Thickness",
  "status": "ERROR",
  "viewJudge": "ERROR",
  "imageJudge": "ERROR",
  "measurementJudge": "ERROR",
  "score": 0.00,
  "scoreThreshold": 95.00,
  "dimensions": {
    "width": null,
    "depth": null,
    "height": null,
    "unit": "mm"
  },
  "measurements": [],
  "failureReasons": [
    "INFERENCE_ERROR"
  ],
  "message": "Inference failed."
}
```

### 5.5 결과 규칙

- `status`: API 실행 상태인 `SUCCESS` 또는 `ERROR`
- `viewJudge`: 해당 View 하나에 대한 AI 최종 판정인 `PASS`, `FAIL`, `ERROR`
- `imageJudge`: Thickness 이미지 판정
- `measurementJudge`: Thickness 측정부 전체 판정. 측정부가 없으면 `NOT_APPLICABLE`
- `measurements[].judge`: 측정부별 AI 판정
- `failureReasons`: AI가 판단한 실패 원인 코드 배열
- C#은 위 판정을 다시 계산하지 않고 그대로 사용한다.
- `dimensions`: 결과 이미지의 이미지 영역을 침범하지 않는 하단 정보 영역에 표시할 W/D/H와 단위이다. 판정 근거 여부와 관계없이 기본 결과에 포함한다.
- `dimensions.width`, `dimensions.depth`, `dimensions.height`는 각각 W, D, H 표시값이며 `dimensions.unit`은 기본 `mm`이다.
- W/D/H를 산출하지 못한 경우에도 `dimensions` 객체와 각 필드는 생략하지 않고 값에 `null`을 반환한다. C#은 `null`을 `-`로 표시한다.
- `specValue`, `toleranceMin`, `toleranceMax`, `unit`은 C#이 이미 보유한 기준정보이므로 결과에서 반복 반환하지 않는다.

기존 `overallJudge`는 제품 전체 판정으로 오해할 수 있으므로 `viewJudge`로 변경한다. `viewJudge`는 현재 View 하나에 대한 AI 최종 판정이다. 6개 View를 집계한 제품 검사 전체 판정이 별도로 필요하면 `inspectionJudge`를 사용한다.

## 6. VLAD_Search_Mat

### 6.1 네이티브 서명

```c
void* VLAD_Search_Mat(
    void* fullImageVladId,
    void* croppedImageVladId,
    void* rawData,
    float threshold,
    int drawMode,
    const char* searchContextJsonUtf8);
```

- 검색 대상 이미지는 `rawData`의 `cv::Mat*`로 전달한다.
- 이미지 파일 경로는 JSON에 포함하지 않는다.
- 전체 이미지 모델과 Crop 이미지 모델은 각각의 Vlad ID를 통해 사용한다.

### 6.2 입력 JSON

```json
{
  "schemaVersion": "1.1",
  "viewName": "Top",
  "scoreThreshold": 99.00,
  "topK": 3
}
```

| 필드 | 설명 |
| --- | --- |
| `schemaVersion` | 검색 계약 버전 |
| `viewName` | 같은 위치 이미지끼리 검색하기 위한 필수값 |
| `scoreThreshold` | AI가 후보 포함 여부를 판단하는 기준 |
| `topK` | 기준 이상 후보 중 반환할 최대 개수. 현재 3 |

## 7. VLAD_Search_Data

### 7.1 네이티브 서명

```c
int VLAD_Search_Data(
    void* fullImageVladId,
    void* croppedImageVladId,
    void* searchData,
    char* resultJsonUtf8,
    int resultJsonCapacity,
    int* requiredResultJsonBytes);
```

- `searchData`는 `VLAD_Search_Mat`이 반환한 검색 결과 포인터다.
- 결과 JSON 메모리 규칙은 3장의 호출자 소유 UTF-8 버퍼 계약을 따른다.

### 7.2 후보가 있는 결과 JSON

```json
{
  "schemaVersion": "1.1",
  "viewName": "Top",
  "status": "SUCCESS",
  "hasAlternatives": true,
  "candidates": [
    {
      "rank": 1,
      "partNo": "A001",
      "partName": "유사제품1",
      "score": 99.52
    },
    {
      "rank": 2,
      "partNo": "B013",
      "partName": "유사제품2",
      "score": 99.12
    }
  ],
  "message": ""
}
```

### 7.3 후보가 없는 결과 JSON

```json
{
  "schemaVersion": "1.1",
  "viewName": "Top",
  "status": "SUCCESS",
  "hasAlternatives": false,
  "candidates": [],
  "message": ""
}
```

### 7.4 오류 결과 JSON

```json
{
  "schemaVersion": "1.1",
  "viewName": "Top",
  "status": "ERROR",
  "hasAlternatives": false,
  "candidates": [],
  "message": "Similarity search failed."
}
```

### 7.5 결과 규칙

- AI는 `scoreThreshold` 이상으로 판정한 후보만 `candidates`에 포함한다.
- 후보는 Score 내림차순으로 정렬하고 최대 3개를 반환한다.
- `hasAlternatives`는 PASS 후보가 하나 이상일 때만 `true`다.
- 기준 이상 후보만 반환하므로 후보별 `judge`는 중복 정보로 보고 반환하지 않는다.
- 요청에서 이미 전달한 `scoreThreshold`는 결과 JSON에서 반복 반환하지 않는다.
- C#은 후보 Score를 기준값과 다시 비교하거나 후보를 제거하지 않는다.
- 후보 품명은 `partName`으로 통일한다.
