# VLAD HD JSON 검사 및 유사도 인터페이스 계약

작성일: 2026-07-20  
상태: AI DLL 구현 전의 목표 계약

## 1. 목적과 적용 범위

이 문서는 AI-Vision IO Inspector와 HD 전용 VLAD DLL 사이의 검사 및 유사도 검색 데이터 계약을 정의한다.

- 검사 입력은 `VLAD_HD_Inference_Mat`에 JSON으로 전달한다.
- 검사 결과는 `VLAD_HD_InferenceData_Result`가 JSON으로 반환한다.
- 유사도 검색은 `VLAD_Search_Mat`과 `VLAD_Search_Data`를 사용한다.
- 전체 이미지용 `fullImageVladId`와 Crop 이미지용 `croppedImageVladId`를 모든 목표 HD 검사/결과/검색 호출에 함께 전달한다.
- 모든 score는 `0.00~100.00` 점수 체계이며 소수점 둘째 자리까지 사용한다.
- 모든 실제 측정값과 기준값의 단위는 `mm` 고정이다.

이 문서는 **목표 DLL 계약**이다. 현재 배포된 `VLAD_SDK.dll`의 검증된 검사 export는 아래와 같이 4인자다.

~~~csharp
IntPtr VLAD_Inference_Mat(IntPtr vladId, IntPtr rawData, float threshold, int drawMode);
~~~

> **구분:** 위 4인자 서명은 현재 배포된 `VLAD_SDK.dll`의 **레거시 단일 ID export**다. 목표 `VLAD_HD_Inference_Mat`의 서명이 아니며 Crop 이미지 ID를 받지 않는다. 목표 HD API는 아래 4.1절처럼 `fullImageVladId`와 `croppedImageVladId`를 모두 받는다.

현재 C#은 전체 이미지와 Crop 이미지용 ID를 각각 생성·보관하지만, `inspectionContextJson`은 생성 및 로그 기록만 하고 실제 네이티브 DLL에는 전달되지 않는다. 현재 배포 DLL은 단일 ID export만 확인됐으므로, AI 담당자가 본 문서의 새 두 ID export를 구현한 DLL과 헤더 파일을 제공한 뒤에만 실제 호출을 전환한다.

## 2. 핵심 구분

| 항목 | 의미 | 값 범위 | 비고 |
| --- | --- | --- | --- |
| `threshold` | VLAD 내부 검출 또는 추론 제어용 인자 | DLL 내부 정의 | `scoreThreshold`와 다른 값이다. |
| `scoreThreshold` | 업무상 PASS/FAIL 판단 기준 Score | 0.00~100.00 | Config의 `INSPECTION_PASS_SCORE_THRESHOLD`를 전달한다. |
| `score` | AI가 반환한 이미지 정합 Score | 0.00~100.00 | `score >= scoreThreshold`이면 Score 조건 PASS다. |
| `nominalValue` | DB 기준값 | mm | 측정부별 목표값이다. |
| `toleranceMin/toleranceMax` | DB 허용오차 | mm | 기준값에 더하는 상대 오차다. |

예를 들어 `threshold=0.50`과 `scoreThreshold=95.00`은 함께 사용할 수 있다. 전자는 AI 내부 제어값이고, 후자는 업무 판정 기준이므로 하나의 값으로 통합하면 안 된다.

## 3. 문자 인코딩과 버퍼 원칙

입력 및 출력 JSON에는 `일반부품-구조그룹`, `길이` 같은 한글 값이 포함된다. 따라서 DLL ABI는 다음 원칙을 따른다.

1. 모든 JSON 문자열은 UTF-8 `char*`다.
2. C#에서 ANSI `string` marshaling에 의존하지 않는다. UTF-8 byte buffer를 만들고 네이티브 호출이 반환할 때까지 고정한다.
3. 결과 JSON 버퍼 크기는 **byte 수**로 전달하며, 널 종료 문자 1byte를 포함한다.
4. DLL은 전달받은 버퍼 길이를 넘겨 쓰면 안 된다.
5. C#은 반환 직후 JSON byte buffer를 문자열로 복사하고, 다음 추론 호출 전에 해제한다.

현재 `VladNativeMethods`의 일부 선언은 `CharSet.Ansi`다. 본 계약의 한글 JSON을 실제로 사용하려면 새 HD export는 UTF-8 ABI로 별도 선언해야 한다.

## 4. 검사 입력 API

### 4.1 네이티브 ABI

~~~c
void* VLAD_HD_Inference_Mat(
    void* fullImageVladId,
    void* croppedImageVladId,
    void* rawData,
    float threshold,
    int drawMode,
    const char* inspectionContextJsonUtf8);
~~~

### 4.1.1 C# P/Invoke 반영 상태

2026-07-20 기준 `AI.Vision.IOInspector.Vision.LegacyVlad.VladNativeMethods`에는 아래 목표 HD ABI 선언을 추가했다. 기존 단일 ID P/Invoke 선언은 삭제하거나 이름을 변경하지 않는다.

```csharp
IntPtr VLAD_HD_Inference_Mat(
    IntPtr fullImageVladId,
    IntPtr croppedImageVladId,
    IntPtr rawData,
    float threshold,
    int drawMode,
    IntPtr inspectionContextJsonUtf8);

int VLAD_HD_InferenceData_Result(
    IntPtr fullImageVladId,
    IntPtr croppedImageVladId,
    IntPtr detectData,
    IntPtr rawData,
    IntPtr classCount,
    IntPtr resultJsonUtf8,
    int resultJsonCapacity,
    out int requiredResultJsonBytes,
    IntPtr customParameterUtf8);

IntPtr VLAD_Search_Mat(
    IntPtr fullImageVladId,
    IntPtr croppedImageVladId,
    IntPtr rawData,
    float threshold,
    int drawMode,
    IntPtr searchContextJsonUtf8);

int VLAD_Search_Data(
    IntPtr fullImageVladId,
    IntPtr croppedImageVladId,
    IntPtr searchData,
    IntPtr resultJsonUtf8,
    int resultJsonCapacity,
    out int requiredResultJsonBytes);
```

- `inspectionContextJsonUtf8`, `searchContextJsonUtf8`, `resultJsonUtf8`, `customParameterUtf8`는 ANSI `string` marshaling이 아닌 널 종료 UTF-8 byte buffer다.
- `VLAD_Search_Mat`, `VLAD_Search_Data`는 기존 DLL의 단일 ID export와 native 이름이 같으므로, C#에서는 인자 수가 다른 overload로 함께 선언한다.
- 새 export가 현재 DLL에 없으면 첫 호출의 `EntryPointNotFoundException`을 기록하고, 검사/검색은 기존 단일 ID 호환 경로로 유지한다. DLL을 교체한 뒤 앱을 재시작하면 다시 HD export를 확인한다.
- 새 HD 추론 호출이 성공한 경우 결과는 `VLAD_HD_InferenceData_Result` JSON으로 읽는다. 이때 구버전 `VLAD_Custom_InferenceData_V1` Draw/TLV 결과 파서를 함께 호출하지 않는다.

| 인자 | 설명 |
| --- | --- |
| `fullImageVladId` | 전체 이미지용 `VLAD_Custom_Registration`이 반환한 핸들. 현재 RTSP callback도 이 ID에만 등록한다. |
| `croppedImageVladId` | Crop 이미지용 `VLAD_Custom_Registration`이 반환한 별도 핸들. 현재 RTSP callback은 등록하지 않는다. |
| `rawData` | 현재 카메라 프레임의 OpenCV `Mat` 포인터 |
| `threshold` | VLAD 내부 추론 제어값. `scoreThreshold`와 별개 |
| `drawMode` | 기존 Sample_VLAD_SDK와 같은 draw 제어값 |
| `inspectionContextJsonUtf8` | 아래 검사 Context JSON의 UTF-8 널 종료 문자열 |

`rawData` Mat은 `VLAD_HD_InferenceData_Result`가 끝날 때까지 유효해야 한다. `detectData`는 해당 Mat 호출의 결과 핸들이므로 같은 검사 흐름 안에서만 사용한다.

### 4.2 inspectionContextJson 형식

아래 형식이 기준 형식이다. Windows 경로의 역슬래시는 JSON에서 반드시 `\\`로 이스케이프한다.

~~~json
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
  "capturedImagePath": "C:\\Inspection_Data\\2026\\07\\15\\10\\Image\\K26\\01100-51430_Thickness.png",
  "scoreThreshold": 95.00,
  "measurementPoints": [
    {
      "measurementRegionId": 1,
      "indexNo": 1,
      "itemType": "길이",
      "viewType": "Thickness",
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
~~~

### 4.3 최상위 필드

| 필드 | 형식 | 필수 | 설명 |
| --- | --- | --- | --- |
| `schemaVersion` | string | 예 | 계약 버전. 현재 `1.0` |
| `requestType` | string | 예 | 검사 요청은 항상 `Inspection` |
| `inspectionId` | string | 예 | 6개 View를 묶는 한 번의 검사 식별자 |
| `partNo` | string | 예 | 품번 |
| `productName` | string | 예 | 품명. 결과의 `partName`과 같은 값을 의미한다. |
| `categoryNo` | string | 예 | 분류코드 |
| `categoryName` | string | 예 | 분류설명 |
| `partType` | string | 예 | 구분 |
| `viewName` | string | 예 | 현재 Mat의 위치. `Top/Front/Back/Left/Right/Thickness` 중 하나 |
| `captureTime` | string | 예 | ISO 8601 형식의 촬영 시각. 시간대 포함 |
| `capturedImagePath` | string | 예 | 현재 Mat가 저장된 검사 이미지 경로 |
| `scoreThreshold` | number | 예 | 현재 검사 Score 기준. 0.00~100.00 |
| `measurementPoints` | array | 예 | 현재 부품의 측정부 목록. 최대 5개 |

현재 C#은 호환성 필드 `tolerance`도 함께 만든다. 새 DLL은 `toleranceMin`과 `toleranceMax`를 우선 사용하고, 알 수 없는 추가 필드는 무시해야 한다.

### 4.4 measurementPoints 항목

| 필드 | 형식 | 필수 | 설명 |
| --- | --- | --- | --- |
| `measurementRegionId` | integer | 예 | DB 측정부 PK. 결과에도 그대로 되돌려야 한다. |
| `indexNo` | integer | 예 | 화면의 측정부 번호. 1부터 시작하며 부품별로 순차적이다. |
| `itemType` | string | 예 | `길이/너비/높이/두께/미설정` 중 하나 |
| `viewType` | string | 예 | 좌표가 정의된 기준 이미지 위치. 일반적으로 `Thickness` |
| `lineColor` | string | 예 | `#RRGGBB` 또는 `#AARRGGBB` 색상 |
| `nominalValue` | number | 예 | 기준값(mm) |
| `toleranceMin` | number | 예 | 하한 상대 허용오차(mm). 일반적으로 음수 |
| `toleranceMax` | number | 예 | 상한 상대 허용오차(mm). 일반적으로 양수 |
| `x1/y1/x2/y2` | number 또는 null | 예 | 기준 이미지 pixel 좌표. 실제 길이를 자동 산출하라는 뜻이 아니라 AI가 측정할 위치를 아는 기준선이다. |
| `unit` | string | 예 | 항상 `mm` |

여러 View 호출에 같은 `measurementPoints` 배열이 들어갈 수 있다. AI는 `viewType == viewName`인 측정부만 사용하고 다른 View의 측정부는 무시한다. Thickness 외 View에서는 `measurements: []`를 반환할 수 있다.

## 5. 검사 결과 API

### 5.1 네이티브 ABI

기존 `VLAD_Custom_InferenceData_V1`는 호환성 유지를 위해 그대로 남긴다. 새 JSON 결과 export의 이름은 아래로 확정하는 방안을 권장한다.

~~~c
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
~~~

사용자 제안의 `StringBuilder resultJson`만으로는 DLL이 쓸 수 있는 안전한 버퍼 길이를 알 수 없다. 따라서 `resultJsonCapacity`와 `requiredResultJsonBytes` 인자를 반드시 추가한다.

| 인자 | 설명 |
| --- | --- |
| `fullImageVladId` | 전체 이미지용 등록 후 재사용하는 VLAD 핸들 |
| `croppedImageVladId` | Crop 이미지용 등록 후 재사용하는 VLAD 핸들 |
| `detectData` | `VLAD_HD_Inference_Mat` 반환 포인터 |
| `rawData` | 같은 검사에 사용한 OpenCV Mat 포인터 |
| `classCount` | 기존 SDK 호환 인자. 새 JSON 계약에서 불필요하면 `IntPtr.Zero`를 허용하도록 DLL에서 정의 |
| `resultJsonUtf8` | DLL이 결과 JSON을 기록할 호출자 소유 UTF-8 버퍼 |
| `resultJsonCapacity` | `resultJsonUtf8` 전체 byte 크기. 널 종료 포함 |
| `requiredResultJsonBytes` | 출력 전용 포인터. DLL이 필요한 전체 byte 수(UTF-8, 널 종료 포함)를 기록한다. 버퍼가 작으면 이 값으로 1회만 재시도한다. |
| `customParameterUtf8` | 확장용 JSON. 미사용이면 `{}`를 전달 |

반환값은 `1`이면 성공, `0`이면 DLL 처리 실패, `-2`이면 결과 버퍼 부족, `-3`이면 잘못된 인자로 정의한다. `-2`일 때 DLL은 `resultJsonCapacity`를 넘겨 쓰지 않고 `requiredResultJsonBytes`만 채운다. 실패 원인은 가능한 한 작은 Error JSON 또는 DLL 로그로 남긴다.

### 5.2 검사 결과 JSON

`overallJudge` 하나만으로는 이미지 NG와 측정값 NG를 구분할 수 없다. 따라서 `imageJudge`, `measurementJudge`, `failureReasons`를 함께 반환한다.

~~~json
{
  "schemaVersion": "1.0",
  "resultType": "InspectionResult",
  "inspectionId": "20260715_103000_001",
  "partNo": "01100-51430",
  "partName": "Sample Part",
  "viewName": "Thickness",
  "captureTime": "2026-07-15T10:30:00+09:00",
  "imageJudge": "PASS",
  "measurementJudge": "PASS",
  "overallJudge": "PASS",
  "score": 97.23,
  "scoreThreshold": 95.00,
  "dimensions": {
    "width": 100.00,
    "height": 120.00,
    "depth": 30.00,
    "unit": "mm"
  },
  "measurements": [
    {
      "measurementRegionId": 1,
      "indexNo": 1,
      "itemType": "길이",
      "measuredValue": 10.25,
      "specValue": 10.00,
      "toleranceMin": -0.10,
      "toleranceMax": 0.20,
      "judge": "PASS",
      "unit": "mm"
    }
  ],
  "failureReasons": [],
  "message": "Inspection completed successfully."
}
~~~

다음은 이미지 Score와 측정값이 함께 실패한 예다. 사용자가 제시한 예시의 `15.25`, 기준 `10.00`, 허용범위 `-0.10~+0.20` 조합은 `judge: FAIL`이 맞다.

~~~json
{
  "schemaVersion": "1.0",
  "resultType": "InspectionResult",
  "inspectionId": "20260715_103000_001",
  "partNo": "01100-51430",
  "partName": "Sample Part",
  "viewName": "Thickness",
  "captureTime": "2026-07-15T10:30:00+09:00",
  "imageJudge": "FAIL",
  "measurementJudge": "FAIL",
  "overallJudge": "FAIL",
  "score": 17.23,
  "scoreThreshold": 95.00,
  "dimensions": {
    "width": 100.00,
    "height": 120.00,
    "depth": 30.00,
    "unit": "mm"
  },
  "measurements": [
    {
      "measurementRegionId": 1,
      "indexNo": 1,
      "itemType": "길이",
      "measuredValue": 15.25,
      "specValue": 10.00,
      "toleranceMin": -0.10,
      "toleranceMax": 0.20,
      "judge": "FAIL",
      "unit": "mm"
    }
  ],
  "failureReasons": [
    "ImageScoreBelowThreshold",
    "MeasurementOutOfTolerance"
  ],
  "message": "Image score and measurement value are out of specification."
}
~~~

### 5.3 검사 결과 필드

| 필드 | 설명 |
| --- | --- |
| `imageJudge` | 현재 View의 이미지 정합 판정. `PASS/FAIL/ERROR` |
| `measurementJudge` | 현재 View에서 수행한 측정부 비교의 종합 판정. 측정부가 없으면 `NOT_APPLICABLE` |
| `overallJudge` | 현재 View의 최종 판정. `imageJudge`와 `measurementJudge`가 모두 PASS일 때만 PASS |
| `score` | 현재 View의 이미지 AI Score |
| `scoreThreshold` | 요청에 사용한 Score 기준값을 그대로 echo |
| `dimensions` | AI가 제공 가능한 W/H/D 참고값. 측정부 판정의 근거를 대체하지 않는다. 값이 없으면 속성을 생략하거나 null을 반환한다. |
| `measurements` | 현재 View에서 판정한 측정부 목록 |
| `failureReasons` | 기계 판독용 NG 원인 배열. 사람이 읽는 상세 내용은 `message`에 기록 |
| `message` | 운영자/이력 표시용 설명 |

애플리케이션은 6개 View 결과를 같은 `inspectionId`로 모아 최종 검사 이력을 만든다. 제품 전체 최종 PASS는 모든 필수 View의 `overallJudge`와 모든 측정부 판정이 PASS일 때만 허용한다.

### 5.4 C# 결과 파서 매핑

`VladInferenceResultParser`는 위 JSON을 `VladInferenceResult`로 파싱한다. 기존 화면과 이력 흐름이 사용하는 `DetectText`는 하위 호환을 위해 `overallJudge`, `score`, `measurements[].measuredValue`만 `true/false,score,value...` 형식으로 변환한다.

새 결과 계약의 세부 값은 별도 속성으로 보존한다. 따라서 이미지 NG와 측정부 NG의 구분은 `DetectText`가 아닌 아래 속성을 사용해야 한다.

| JSON 필드 | C# 속성 |
| --- | --- |
| `imageJudge` | `VladInferenceResult.ImageJudge` |
| `measurementJudge` | `VladInferenceResult.MeasurementJudge` |
| `overallJudge` | `VladInferenceResult.OverallJudge` |
| `score`, `scoreThreshold` | `VladInferenceResult.Score`, `ScoreThreshold` |
| `measurements[]` | `VladInferenceResult.Measurements` (`measurementRegionId`, `indexNo`, `itemType`, `measuredValue`, `specValue`, `toleranceMin`, `toleranceMax`, `judge`, `unit`) |
| `failureReasons[]` | `VladInferenceResult.FailureReasons` |

`imageJudge`, `measurementJudge`, `overallJudge`, `failureReasons`는 결과 JSON의 필수 필드다. `failureReasons`는 정상 PASS에서 반드시 빈 배열 `[]`로 반환한다.

## 6. 유사도 검색 API

### 6.1 검색 시작

~~~c
void* VLAD_Search_Mat(
    void* fullImageVladId,
    void* croppedImageVladId,
    void* rawData,
    float threshold,
    int drawMode,
    const char* searchContextJsonUtf8);
~~~

최소 `searchContextJson`은 아래와 같다.

~~~json
{
  "viewName": "Thickness",
  "scoreThreshold": 99.00
}
~~~

`viewName`은 필수이며 `Top/Front/Back/Left/Right/Thickness` 중 하나다. `scoreThreshold`는 후보 표시 기준이다. 필요해지면 `schemaVersion`, `requestType`, `topK`를 하위 호환 확장 필드로 추가할 수 있다.

### 6.2 검색 결과 읽기

~~~c
int VLAD_Search_Data(
    void* fullImageVladId,
    void* croppedImageVladId,
    void* searchData,
    char* resultJsonUtf8,
    int resultJsonCapacity,
    int* requiredResultJsonBytes);
~~~

`resultJsonCapacity`은 JSON이 아니라 결과 버퍼의 byte 크기다. `requiredResultJsonBytes`와 `-2` 반환 규칙은 검사 결과 API와 동일하게 적용한다.

후보가 있는 경우:

~~~json
{
  "schemaVersion": "1.0",
  "resultType": "SimilaritySearchResult",
  "viewName": "Top",
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
  "message": "2 candidates satisfy the score threshold."
}
~~~

후보가 없는 경우:

~~~json
{
  "schemaVersion": "1.0",
  "resultType": "SimilaritySearchResult",
  "viewName": "Top",
  "hasAlternatives": false,
  "candidates": [],
  "message": "No candidate satisfies the score threshold."
}
~~~

`hasAlternatives`는 문자열 `"true"`나 `"false"`가 아닌 JSON boolean `true` 또는 `false`다. `candidates`는 `scoreThreshold` 이상인 후보만 포함하며, `hasAlternatives`는 `candidates`가 비어 있지 않을 때만 true다.

유사 후보 `candidates`는 검사 결과 JSON에 넣지 않는다. 검사 API는 판정과 측정값만 반환하고, 유사도 후보는 `VLAD_Search_Data`에서만 반환해야 검사 이력과 등록 전 유사도 확인의 의미가 섞이지 않는다.

## 7. JSON 크기 및 resultJson 버퍼 규칙

### 7.1 64KiB 상한

검사 결과와 검색 결과는 **카메라 View별로 각각 한 번씩** 반환한다. 6대 카메라 결과를 하나의 JSON에 합쳐 반환하지 않는다. 따라서 다음 상한을 적용한다.

| 항목 | 계약 값 | 이유 |
| --- | --- | --- |
| `resultJsonCapacity` 기본값 | `65536` byte | UTF-8 널 종료를 포함한 View별 결과 버퍼 크기 |
| 최대 JSON payload | `65535` byte | 마지막 1 byte는 `\0` 널 종료 문자 |
| `measurementPoints` / `measurements` 최대 | 5개 | 현재 부품 측정부 최대 개수 |
| 검색 `candidates` 최대 | 20개 | 후보 목록이 무제한으로 증가하는 것을 방지 |
| `failureReasons` 최대 | 16개 | 기계 판독용 원인 목록의 상한 |
| `message` 최대 | UTF-8 2048 byte | 이력 표시용 메시지의 상한 |

문서의 예시 데이터를 기준으로 UTF-8 byte 수를 측정한 결과는 다음과 같다.

| 시나리오 | 측정부/후보/메시지 조건 | UTF-8 크기 |
| --- | --- | --- |
| 검사 Context | 측정부 5개 | 약 1.6KiB |
| 검사 결과 | 측정부 5개, 실패 원인 2개, 긴 메시지 | 약 2.8KiB |
| 검색 결과 | 후보 20개, 긴 한글 메시지 | 약 3.6KiB |

따라서 계약 상한을 지키는 정상 응답에는 64KiB가 충분하다. 다만 raw image/base64, 전체 모델 로그, 제한 없는 detection 배열, 제한 없는 후보 목록을 JSON에 넣으면 이 전제가 깨진다. 이미지 자체는 JSON이 아니라 `capturedImagePath` 같은 파일 경로로만 전달하고, 상세 네이티브 로그는 별도 로그 파일로 남긴다.

### 7.2 UTF-8과 StringBuilder 주의점

`StringBuilder`의 Capacity는 .NET 문자 수 관점이고, 본 계약의 네이티브 버퍼는 UTF-8 byte 수 관점이다. `일반부품-구조그룹`처럼 한글이 포함되면 두 값이 같지 않다. 또한 현재의 `CharSet.Ansi` P/Invoke는 UTF-8 결과를 보장하지 않는다.

새 HD API는 `StringBuilder`를 직접 marshaling 하지 않는다. C#이 `Marshal.AllocHGlobal(65536)`으로 byte buffer를 할당하고, 호출 전 0으로 초기화한 뒤, 반환 후 널 종료 전까지의 byte를 `Encoding.UTF8.GetString`으로 읽는다. `try/finally`에서 버퍼를 해제한다. 이 방식이면 DLL과 C#이 같은 byte 단위를 사용한다.

현재 구버전 `VladInferenceResultParser`의 `new StringBuilder(8192)`는 기존 `VLAD_Custom_InferenceData_V1` 텍스트/CSV 결과용이다. 해당 구 export는 버퍼 길이 인자를 받지 않으므로 새 JSON 결과 계약에 재사용하면 안 된다. 새 HD JSON export에는 반드시 본 절의 명시적 byte 버퍼 API를 사용한다.

### 7.3 버퍼 부족 처리

1. C#은 우선 64KiB 버퍼로 호출한다.
2. DLL이 결과 전체를 쓸 수 있으면 `1`을 반환하고 `requiredResultJsonBytes`에 실제 필요 byte 수를 기록한다.
3. 버퍼가 작으면 DLL은 버퍼 범위를 넘겨 쓰지 않고 `-2`를 반환하며, 필요한 byte 수를 `requiredResultJsonBytes`에 기록한다.
4. C#은 필요한 크기가 64KiB 이하인 경우에만 정확한 크기로 1회 재시도한다.
5. 필요한 크기가 64KiB를 초과하면 앱은 `ERROR` 이력을 남기고, DLL/AI 담당자는 후보 수나 메시지 등 payload를 줄여 계약 상한 안으로 수정한다. 운영 중 무제한 버퍼 확장은 금지한다.

`requiredResultJsonBytes`는 널 종료 문자를 포함해야 하며, `resultJsonCapacity` 이하로 성공한 경우에도 실제 사용량 검증을 위해 기록한다.

## 8. 애플리케이션 매핑 규칙

1. `inspectionId`는 CaptureAll 시작 시 한 번 생성하고 6개 View 호출에서 동일하게 유지한다.
2. 전체 이미지와 Crop 이미지용 ID는 프로그램 시작 시 함께 생성하고, 학습 완료 재초기화 시 함께 해제/재생성한다.
3. 현재 `VLAD_Ops_RTSP`는 활성 ID 하나만 보관하므로 RTSP callback은 `fullImageVladId`에만 등록한다.
4. 결과의 `measurementRegionId`를 최우선 키로 사용해 DB `PartMeasurementPoint`에 매핑한다.
5. 구버전 DLL처럼 `measurementRegionId`가 없으면 `indexNo` 오름차순을 임시 호환 키로 사용한다.
6. `specValue`, 허용오차, 단위는 결과 표시용 snapshot이다. 최종 DB 기준 비교는 검사 시작 시 읽은 Part 기준정보를 원본으로 한다.
7. AI가 측정값을 mm로 반환하면 앱은 cm/m/pixel 변환을 하지 않는다.
8. `score`와 `scoreThreshold`는 소수점 둘째 자리로 저장 및 표시한다.
9. JSON 구문 오류, 필수 필드 누락, `inspectionId` 불일치, 알 수 없는 Judge 값은 `ERROR`로 처리하고 원본 JSON을 이벤트 로그에 남긴다.

## 9. 구현 전 확인 목록

| ID | 확인 사항 | 완료 기준 |
| --- | --- | --- |
| I-001 | 새 DLL export 이름과 인자 순서 | `dumpbin /exports VLAD_SDK.dll` 및 C/C++ 헤더가 문서와 일치 |
| I-002 | CallingConvention 및 x64 ABI | C# P/Invoke와 DLL 헤더가 일치하고 AccessViolation 없음 |
| I-003 | UTF-8 한글 JSON | `일반부품-구조그룹`, `길이`, `유사제품1` 왕복 문자열이 깨지지 않음 |
| I-004 | 결과 버퍼 경계 | 정상/긴 message/후보 다수 결과에서 버퍼 초과 없음 |
| I-005 | PASS/FAIL/ERROR | 이미지 NG, 측정부 NG, 둘 다 NG, 측정부 없음, JSON 오류를 각각 검증 |
| I-006 | 6개 View 병합 | 동일 inspectionId로 저장되고 제품 전체 판정이 정확함 |
| I-007 | 유사도 검색 | 후보 있음/없음, threshold 경계값, Top~Thickness 위치별 결과를 검증 |
| I-008 | 64KiB 버퍼 경계 | 5개 측정부, 후보 20개, 한글 message, `-2` 재시도와 64KiB 초과 ERROR를 검증 |
| I-009 | 두 VLAD ID | 전체/Crop ID가 모두 생성되고, Crop ID가 RTSP callback을 중복 등록하지 않으며, 새 DLL이 두 ID를 실제 native 호출에 사용함을 콘솔로 검증 |

## 10. 현재 코드 전환 시점

현재의 `VLAD_Inference_Mat`, `VLAD_Custom_InferenceData_V1`, `TEST_VLAD_Custom_InferenceData_V1` 호출을 임의로 이름만 바꾸면 기존 DLL에서 `EntryPointNotFoundException` 또는 메모리 접근 오류가 발생할 수 있다.

다음 순서로 전환한다.

1. AI 담당자가 전체/Crop ID 두 개를 받는 새 export가 포함된 x64 DLL, C/C++ 헤더, 간단한 콘솔 샘플을 제공한다.
2. 콘솔 샘플에서 입력 JSON, PASS 결과 JSON, FAIL 결과 JSON, 후보 있음/없음 JSON을 검증한다.
3. C#에 새 P/Invoke와 UTF-8 buffer wrapper를 추가한다.
4. 기존 함수는 호환 wrapper로 남기고 새 HD API를 별도 경로로 연결한다.
5. 실제 6채널 검사와 DB 이력 저장을 검증한 뒤에만 기존 CSV 결과 parser를 제거한다.

따라서 본 문서의 함수명은 **AI DLL 구현 요청을 위한 확정 목표 이름**이며, 현재 배포 DLL의 기존 export 이름을 즉시 변경한다는 의미는 아니다.
