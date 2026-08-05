# VLAD HD API 고정 JSON 버퍼 변경 사양

- 최초 작성일: 2026-08-03
- 최종 수정일: 2026-08-05
- 문서 버전: 1.2
- 상태: C++ AI DLL 연동 협의 사양

## 1. 변경 목적

C# 프로그램과 C++ AI DLL 사이에서 가변 문자열과 불필요한 업무 데이터를 주고받지 않도록 API와 JSON을 단순화한다.

1. C++에서 사용하지 않는 필드는 전달하지 않는다.
2. 문자열 필드는 `partNo`, `viewName`, `viewJudge`처럼 실제 필요한 값만 유지한다.
3. 숫자는 JSON 문자열이 아니라 JSON Number로 전달하고 C++에서 정수 또는 실수 형식으로 읽는다.
4. JSON은 C#이 미리 정한 고정 크기 UTF-8 버퍼에 작성해 전달한다.
5. DLL은 C#이 제공한 결과 버퍼에 JSON을 작성하며 별도의 결과 문자열 메모리를 반환하지 않는다.
6. 일반 View와 Thickness는 동일 API와 동일 최상위 필드 구성을 사용한다.
7. 측정부가 없는 View는 배열을 생략하지 않고 빈 배열 `[]`로 전달한다.

적용 대상 API는 다음과 같다.

- `VLAD_HD_Inference_Mat`
- `VLAD_HD_InferenceData_Result`
- `VLAD_Search_Mat`
- `VLAD_Search_ResultData`

기존 `VLAD_Search_Data` 명칭은 신규 HD 계약에서 `VLAD_Search_ResultData`로 변경한다.

## 2. 공통 버퍼 규격

### 2.1 전체 JSON 버퍼

| 구분 | 고정 버퍼 크기 | 단위 | 비고 |
| --- | ---: | --- | --- |
| 검사 요청 JSON | 8192 | UTF-8 byte | 널 종료 포함 |
| 검사 결과 JSON | 8192 | UTF-8 byte | 널 종료 포함 |
| 유사도 요청 JSON | 8192 | UTF-8 byte | 널 종료 포함 |
| 유사도 결과 JSON | 8192 | UTF-8 byte | 널 종료 포함 |

- C#은 호출 전에 전체 버퍼를 `0`으로 초기화한다.
- JSON 직렬화 결과의 UTF-8 byte 수에 널 종료 1 byte를 더한 값이 8192 이하여야 한다.
- DLL은 전달받은 버퍼 크기를 초과해 읽거나 쓰면 안 된다.
- DLL은 결과 마지막에 반드시 널 종료 문자를 기록한다.
- 결과가 8192 byte를 초과하면 DLL은 버퍼를 넘겨 쓰지 않고 `-2`를 반환한다.
- 신규 계약에서는 실행 중 가변 크기로 재할당하지 않는다. 필드 개수와 배열 최대 개수를 제한해 고정 버퍼 안에서 처리한다.

### 2.2 문자열 필드 최대 크기

문자열 크기는 UTF-8 기준이며 널 종료 문자를 포함한다.

| 필드 | 최대 크기 | 허용 값 또는 설명 |
| --- | ---: | --- |
| `partNo` | 64 byte | 품번. 실제 문자열은 최대 63 byte |
| `viewName` | 16 byte | `Top`, `Front`, `Back`, `Left`, `Right`, `Thickness` |
| `viewJudge` | 8 byte | `PASS`, `FAIL` |
| `candidates[].partNo` | 64 byte | 후보 품번. 실제 문자열은 최대 63 byte |

`schemaVersion`, `inspectionId`, `partName`, `status`, `message`, `itemType`, `lineColor`, `unit`은 신규 계약에서 전달하지 않는다.

### 2.3 숫자와 배열 형식

JSON은 텍스트 포맷이므로 숫자 필드 자체가 고정 byte 구조체로 전달되는 것은 아니다. 아래 표는 JSON 숫자를 파싱한 뒤 C++에서 보관할 형식과, JSON 문자열 안에서 허용할 숫자 토큰의 최대 길이를 함께 정의한다.

| 필드 | C++ 저장 형식 | 저장 크기 | JSON 값 최대 크기 | 범위 또는 개수 |
| --- | --- | ---: | ---: | --- |
| `indexNo` | `int32_t` | 4 byte | 11 byte | 1~5 |
| `topK` | `int32_t` | 4 byte | 11 byte | 현재 3 고정 |
| `rank` | `int32_t` | 4 byte | 11 byte | 1~3 |
| `hasAlternatives` | `bool` | 1 byte | 5 byte | `true`, `false` |
| Score/기준값/허용값/좌표/측정값/W/D/H | `double` | 8 byte | 32 byte | JSON Number |
| `measurementPoints` | 고정 배열 | 항목당 60 byte 이하 | - | 최대 5개 |
| `measurements` | 고정 배열 | 항목당 16 byte 이하 | - | 최대 5개 |
| `candidates` | 고정 배열 | 항목당 80 byte 이하 | - | 최대 3개 |

소수값이 필요한 항목이 있으므로 모든 값을 정수로 강제하지 않는다. 순번과 개수는 정수로, Score와 좌표 및 측정값은 실수로 처리한다.

JSON Number는 지수 표기를 사용하지 않고 최대 32 ASCII byte 안에서 직렬화한다. C#은 소수점 구분자로 항상 `.`을 사용하고, C++은 JSON 파서가 반환한 값을 `double`로 읽는다.

### 2.4 API별 최대 직렬화 크기

| JSON 종류 | 배열 최대 조건 | 예상 최대 크기 | 고정 버퍼 대비 |
| --- | --- | ---: | ---: |
| 검사 요청 | 측정부 5개 | 2048 byte 이하 | 8192 byte의 25% 이하 |
| 검사 결과 | 측정값 5개 | 1024 byte 이하 | 8192 byte의 13% 이하 |
| 유사도 요청 | 후보 없음 | 256 byte 이하 | 8192 byte의 4% 이하 |
| 유사도 결과 | 후보 3개 | 1024 byte 이하 | 8192 byte의 13% 이하 |

위 예상치는 필드명, 구분 문자, 공백, 최대 문자열 및 숫자 토큰을 포함해 여유 있게 잡은 상한이다. 네 API 모두 8192 byte를 사용하면 정상 계약 범위에서 런타임 재할당이 필요하지 않다.

### 2.5 C++ 고정 저장 구조 권장

JSON 파싱 후에는 동적 문자열을 계속 보관하지 말고 아래와 같은 고정 크기 저장 구조로 복사하는 방식을 권장한다.

| 값 | 고정 저장 공간 |
| --- | ---: |
| 품번 | `char partNo[64]` |
| View | `char viewName[16]` |
| View 판정 | `char viewJudge[8]` |
| 측정부 입력 | 최대 5개 고정 배열 + `int32_t measurementPointCount` |
| 측정 결과 | 최대 5개 고정 배열 + `int32_t measurementCount` |
| 유사 후보 | 최대 3개 고정 배열 + `int32_t candidateCount` |

`partNo`와 후보 품번은 영문자, 숫자, `-`, `_`, `.`만 허용한다. 이 제한으로 UTF-8 다중 byte와 JSON escape에 따른 예상 밖의 버퍼 증가를 막는다.

## 3. 메모리 소유권

1. C#이 고정 크기 입력 JSON 버퍼를 할당하고 값을 채운다.
2. DLL은 함수가 실행되는 동안에만 입력 버퍼를 읽는다.
3. DLL은 입력 포인터를 보관하거나 해제하지 않는다.
4. 함수 반환 후 C#이 입력 버퍼를 해제한다.
5. 결과 JSON 버퍼도 C#이 미리 할당해 DLL에 전달한다.
6. DLL은 결과 버퍼에 값만 기록하고 해당 버퍼를 해제하지 않는다.
7. C#은 결과를 관리 문자열로 복사한 후 결과 버퍼를 해제한다.
8. C# 관리 문자열과 JSON 파싱 객체는 .NET GC가 관리하므로 별도의 네이티브 해제가 필요 없다.

`VLAD_HD_Inference_Mat`이 반환하는 `detectData`는 JSON 버퍼와 별개다. 기존 Sample과 VLAD_Ops에는 C#이 `detectData`를 해제하는 API가 없으므로 SDK 소유 포인터로 취급한다. DLL이 추론마다 `detectData`를 신규 할당하는 구조라면 AI 담당자가 동일 DLL의 전용 해제 API를 별도로 제공해야 한다. C#은 임의로 `Marshal.FreeHGlobal(detectData)`를 호출하지 않는다.

## 4. VLAD_HD_Inference_Mat

### 4.1 단순화한 네이티브 서명

```c
void* VLAD_HD_Inference_Mat(
    void* fullImageVladId,
    void* croppedImageVladId,
    void* rawData,
    int drawMode,
    const char* requestJsonUtf8,
    int requestJsonBufferSize);
```

변경 사항:

- 기존 `float threshold` 인자를 제거한다.
- 업무 판정 기준은 JSON의 `scoreThreshold` 하나만 사용한다.
- `requestJsonBufferSize`는 항상 `8192`를 전달한다.
- 실제 검사 이미지는 `rawData`의 `cv::Mat*`로 전달하므로 이미지 경로는 JSON에 넣지 않는다.

### 4.2 일반 View 요청

일반 View에서도 Thickness와 같은 최상위 구성을 사용한다. 측정부가 없으므로 `measurementPoints`는 빈 배열이다.

```json
{
  "partNo": "01100-51430",
  "viewName": "Top",
  "scoreThreshold": 95.00,
  "dimensions": {
    "width": 0.00,
    "depth": 0.00,
    "height": 0.00
  },
  "measurementPoints": []
}
```

### 4.3 Thickness 요청

```json
{
  "partNo": "01100-51430",
  "viewName": "Thickness",
  "scoreThreshold": 95.00,
  "dimensions": {
    "width": 0.00,
    "depth": 0.00,
    "height": 0.00
  },
  "measurementPoints": [
    {
      "indexNo": 1,
      "nominalValue": 150.00,
      "toleranceMin": -0.50,
      "toleranceMax": 0.50,
      "x1": 120.50,
      "y1": 240.00,
      "x2": 360.50,
      "y2": 240.00
    }
  ]
}
```

### 4.4 요청 필드

| 필드 | 일반 View | Thickness | 설명 |
| --- | --- | --- | --- |
| `partNo` | 필수 | 필수 | 품번 |
| `viewName` | 필수 | 필수 | 카메라 위치 |
| `scoreThreshold` | 필수 | 필수 | AI PASS/FAIL 기준 Score |
| `dimensions` | 0으로 전달 | 0으로 전달 | 결과와 같은 타입을 유지하기 위한 고정 객체 |
| `measurementPoints` | 빈 배열 | 0~5개 | 측정부 기준값과 좌표 |

`dimensions`를 요청에서 `[]`, 결과에서 객체로 사용하면 같은 키의 JSON 타입이 달라져 C++ 파싱이 복잡해진다. 따라서 요청에서도 객체를 유지하고 `width`, `depth`, `height`를 `0.00`으로 전달한다.

### 4.5 측정부 IndexNo 규칙

1. DB에 저장된 측정부는 화면 표시 순서대로 `1`부터 연속 번호를 사용한다.
2. 중간 측정부를 삭제하면 뒤 측정부의 IndexNo를 앞으로 당긴다.
3. 측정부가 1개이면 `indexNo=1`이다.
4. C#은 검사 요청을 만들 때 현재 DB의 최종 정렬 순서로 IndexNo를 다시 정규화한다.
5. AI DLL은 요청의 IndexNo를 결과 `measurements[].indexNo`에 그대로 반환한다.

## 5. VLAD_HD_InferenceData_Result

### 5.1 단순화한 네이티브 서명

```c
int VLAD_HD_InferenceData_Result(
    void* fullImageVladId,
    void* croppedImageVladId,
    void* detectData,
    char* resultJsonUtf8,
    int resultJsonBufferSize,
    int* writtenResultJsonBytes);
```

제거 인자:

- `rawData`: 결과 JSON 생성에 사용하지 않음
- `classCount`: 결과 JSON 생성에 사용하지 않음
- `customParameterUtf8`: 신규 HD 계약에서 사용하지 않음

`resultJsonBufferSize`는 항상 `8192`다. `writtenResultJsonBytes`에는 널 종료를 포함한 실제 작성 byte 수를 기록한다.

### 5.2 일반 View 결과

```json
{
  "partNo": "01100-51430",
  "viewName": "Top",
  "viewJudge": "PASS",
  "score": 97.23,
  "scoreThreshold": 95.00,
  "dimensions": {
    "width": 100.00,
    "depth": 30.00,
    "height": 120.00
  },
  "measurements": []
}
```

### 5.3 Thickness 결과

```json
{
  "partNo": "01100-51430",
  "viewName": "Thickness",
  "viewJudge": "PASS",
  "score": 97.23,
  "scoreThreshold": 95.00,
  "dimensions": {
    "width": 100.00,
    "depth": 30.00,
    "height": 120.00
  },
  "measurements": [
    {
      "indexNo": 1,
      "measuredValue": 150.10
    }
  ]
}
```

### 5.4 결과 규칙

- `viewJudge`는 현재 View의 AI 판정이며 `PASS` 또는 `FAIL`만 사용한다.
- 별도의 `judge` 필드는 `viewJudge`와 의미가 중복되므로 추가하지 않는다.
- AI DLL은 업무상 `ERROR` 판정을 반환하지 않는다.
- 이상한 이미지 또는 측정값도 AI가 계산한 Score와 측정값으로 반환한다.
- DLL 호출 실패, 잘못된 포인터, 버퍼 부족 같은 기술 오류는 JSON 판정이 아니라 API 반환값과 C# 로그로 관리한다.
- 일반 5개 View의 `measurements`는 빈 배열이다.
- Thickness는 요청받은 측정부 수만큼 `indexNo`, `measuredValue`만 반환한다.
- W/D/H는 `dimensions.width`, `dimensions.depth`, `dimensions.height`로 반환한다.
- 단위는 프로그램 전체에서 `mm` 고정이므로 JSON에서 제외한다.

### 5.5 API 반환값

| 반환값 | 의미 |
| ---: | --- |
| `1` | 결과 JSON 작성 성공 |
| `0` | 결과 데이터 없음 |
| `-1` | 잘못된 인자 또는 포인터 |
| `-2` | 고정 결과 버퍼 8192 byte 초과 |

AI 업무 판정의 오류 여부와 API 실행 오류를 혼동하지 않는다. `viewJudge`에는 `PASS/FAIL`만 들어가며 API 실행 실패는 반환값으로 구분한다.

## 6. VLAD_Search_Mat

### 6.1 네이티브 서명

```c
void* VLAD_Search_Mat(
    void* fullImageVladId,
    void* croppedImageVladId,
    void* rawData,
    int drawMode,
    const char* requestJsonUtf8,
    int requestJsonBufferSize);
```

- 기존 별도 `threshold` 인자는 사용하지 않는다.
- 유사도 기준은 JSON의 `scoreThreshold`만 사용한다.
- `requestJsonBufferSize`는 항상 `8192`다.

### 6.2 요청 JSON

```json
{
  "viewName": "Top",
  "scoreThreshold": 99.00,
  "topK": 3,
  "hasAlternatives": false,
  "candidates": []
}
```

`hasAlternatives`와 `candidates`는 Result와 같은 최상위 형식을 유지하기 위해 요청에도 기본값으로 포함한다.

## 7. VLAD_Search_ResultData

### 7.1 네이티브 서명

```c
int VLAD_Search_ResultData(
    void* fullImageVladId,
    void* croppedImageVladId,
    void* searchData,
    char* resultJsonUtf8,
    int resultJsonBufferSize,
    int* writtenResultJsonBytes);
```

`resultJsonBufferSize`는 항상 `8192`다. 메모리 소유권과 반환값은 `VLAD_HD_InferenceData_Result`와 같은 규칙을 사용한다.

### 7.2 결과 JSON

```json
{
  "viewName": "Top",
  "scoreThreshold": 99.00,
  "topK": 3,
  "hasAlternatives": true,
  "candidates": [
    {
      "rank": 1,
      "partNo": "A001",
      "score": 99.52
    },
    {
      "rank": 2,
      "partNo": "B013",
      "score": 99.12
    }
  ]
}
```

유사품이 없는 경우:

```json
{
  "viewName": "Top",
  "scoreThreshold": 99.00,
  "topK": 3,
  "hasAlternatives": false,
  "candidates": []
}
```

결과 규칙:

- `scoreThreshold` 이상 후보만 반환한다.
- 후보는 Score 내림차순으로 정렬한다.
- 후보는 최대 `topK=3`개다.
