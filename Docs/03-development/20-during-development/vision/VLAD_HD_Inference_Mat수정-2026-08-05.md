# VLAD HD API 고정 JSON 버퍼 변경 사양

- 최초 작성일: 2026-08-03
- 최종 수정일: 2026-08-05
- 문서 버전: 1.2
- 상태: C++ AI DLL 연동 협의 사양

## 1. VLAD_HD_Inference_Mat

### 1.1 네이티브 서명

```c
void* VLAD_HD_Inference_Mat(
    void* fullImageVladId,
    void* croppedImageVladId,
    void* rawData,
    int drawMode,
    const char* requestJsonUtf8);
```

변경 사항:

- 기존 `float threshold` 인자를 제거한다.
- 업무 판정 기준은 JSON의 `scoreThreshold` 하나만 사용한다.
- `requestJsonUtf8`는 널 종료된 UTF-8 JSON이며 DLL은 읽기만 한다.
- C#은 입력 JSON이 8192 byte를 넘지 않는지 호출 전에 확인한다.
- 실제 검사 이미지는 `rawData`의 `cv::Mat*`로 전달하므로 이미지 경로는 JSON에 넣지 않는다.

### 1.2 일반 View 요청

일반 View에서도 Thickness와 같은 최상위 구성을 사용한다. 측정부가 없으므로 `measurementPoints`는 빈 배열이다.

```json
{
  "partNo": "01100-51430",
  "viewName": 1,
  "scoreThreshold": 95.00,
  "dimensions": {
    "width": 0.00,
    "depth": 0.00,
    "height": 0.00
  },
  "measurementPoints": []
}
```

### 1.3 Thickness 요청

```json
{
  "partNo": "01100-51430",
  "viewName": 6,
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

### 1.4 ViewCode

요청 JSON의 `viewName`은 다음 정수 코드를 사용한다.

| `viewName` | 위치 |
| ---: | --- |
| `1` | Top |
| `2` | Front |
| `3` | Back |
| `4` | Left |
| `5` | Right |
| `6` | Thickness |

### 1.5 요청 필드

| 필드 | 일반 View | Thickness | 설명 |
| --- | --- | --- | --- |
| `partNo` | 필수 | 필수 | 품번 |
| `viewName` | 필수 | 필수 | 카메라 위치 코드 `1~6` |
| `scoreThreshold` | 필수 | 필수 | AI PASS/FAIL 기준 Score |
| `dimensions` | 0으로 전달 | 0으로 전달 | 결과와 같은 타입을 유지하기 위한 고정 객체 |
| `measurementPoints` | 빈 배열 | 0~5개 | 측정부 기준값과 좌표 |

`dimensions`를 요청에서 `[]`, 결과에서 객체로 사용하면 같은 키의 JSON 타입이 달라져 C++ 파싱이 복잡해진다. 따라서 요청에서도 객체를 유지하고 `width`, `depth`, `height`를 `0.00`으로 전달한다.

### 1.6 측정부 IndexNo 규칙

1. DB에 저장된 측정부는 화면 표시 순서대로 `1`부터 연속 번호를 사용한다.
2. 중간 측정부를 삭제하면 뒤 측정부의 IndexNo를 앞으로 당긴다.
3. 측정부가 1개이면 `indexNo=1`이다.
4. C#은 검사 요청을 만들 때 현재 DB의 최종 정렬 순서로 IndexNo를 다시 정규화한다.
5. AI DLL은 요청의 IndexNo를 결과 `measurements[].indexNo`에 그대로 반환한다.

## 2. VLAD_HD_InferenceData_Result

### 2.1 네이티브 서명

```c
void VLAD_HD_InferenceData_Result(
    void* fullImageVladId,
    void* croppedImageVladId,
    void* detectData,
    char* resultJsonUtf8);
```

`resultJsonUtf8`는 C#이 `0`으로 초기화한 8192 byte 버퍼다. C++은 이 버퍼에 널 종료된 결과 JSON을 기록한다.

### 2.2 일반 View 결과

```json
{
  "partNo": "01100-51430",
  "viewName": 1,
  "viewJudge": 0,
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

### 2.3 Thickness 결과

```json
{
  "partNo": "01100-51430",
  "viewName": 6,
  "viewJudge": 0,
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

### 2.4 ViewJudge

결과 JSON의 `viewJudge`는 다음 정수 코드를 사용한다.

| `viewJudge` | 판정 |
| ---: | --- |
| `0` | PASS |
| `1` | FAIL |

### 2.5 결과 항목 Size

| 결과 JSON 항목 | JSON 최대 Size | C++ 지정 형태 | 비고 |
| --- | ---: | --- | --- |
| `partNo` | 64 byte | `char[64]` | 널 종료 포함 |
| `viewName` | 1 byte | `int32_t` (4 byte) | `1~6` |
| `viewJudge` | 1 byte | `int32_t` (4 byte) | `0=PASS`, `1=FAIL` |
| `score` | 32 byte | `double` (8 byte) | JSON Number |
| `scoreThreshold` | 32 byte | `double` (8 byte) | JSON Number |
| `dimensions.width` | 32 byte | `double` (8 byte) | mm |
| `dimensions.depth` | 32 byte | `double` (8 byte) | mm |
| `dimensions.height` | 32 byte | `double` (8 byte) | mm |
| `measurements` | 최대 5개 | 고정 배열 | 일반 View는 빈 배열 |
| `measurements[].indexNo` | 1 byte | `int32_t` (4 byte) | `1~5` |
| `measurements[].measuredValue` | 32 byte | `double` (8 byte) | mm |

### 2.6 결과 규칙

- `viewJudge`는 현재 View의 AI 판정이며 `0=PASS`, `1=FAIL`이다.
- AI DLL은 업무상 `ERROR` 판정을 반환하지 않는다.
- 이상한 이미지 또는 측정값도 AI가 계산한 Score와 측정값으로 반환한다.
- 일반 5개 View의 `measurements`는 빈 배열이다.
- Thickness는 요청받은 측정부 수만큼 `indexNo`, `measuredValue`만 반환한다.
- W/D/H는 `dimensions.width`, `dimensions.depth`, `dimensions.height`로 반환한다.
- 단위는 프로그램 전체에서 `mm` 고정이므로 JSON에서 제외한다.

## 3. VLAD_Search_Mat

### 3.1 네이티브 서명

```c
void* VLAD_Search_Mat(
    void* fullImageVladId,
    void* croppedImageVladId,
    void* rawData,
    int drawMode,
    const char* requestJsonUtf8);
```

- 기존 별도 `threshold` 인자는 사용하지 않는다.
- 유사도 기준은 JSON의 `scoreThreshold`만 사용한다.
- `requestJsonUtf8`는 널 종료된 UTF-8 JSON이며 DLL은 읽기만 한다.

### 3.2 요청 JSON

```json
{
  "viewName": 1,
  "scoreThreshold": 99.00,
  "topK": 3,
  "hasAlternatives": false,
  "candidates": []
}
```

`hasAlternatives`와 `candidates`는 Result와 같은 최상위 형식을 유지하기 위해 요청에도 기본값으로 포함한다.

## 4. VLAD_Search_ResultData

### 4.1 네이티브 서명

```c
void VLAD_Search_ResultData(
    void* fullImageVladId,
    void* croppedImageVladId,
    void* searchData,
    char* resultJsonUtf8);
```

`resultJsonUtf8`는 C#이 `0`으로 초기화한 8192 byte 버퍼다. 메모리 소유권은 `VLAD_HD_InferenceData_Result`와 같은 규칙을 사용한다.

### 4.2 결과 JSON

```json
{
  "viewName": 1,
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
  "viewName": 1,
  "scoreThreshold": 99.00,
  "topK": 3,
  "hasAlternatives": false,
  "candidates": []
}
```

### 4.3 결과 항목 Size

| 결과 JSON 항목 | JSON 최대 Size | C++ 지정 형태 | 비고 |
| --- | ---: | --- | --- |
| `viewName` | 1 byte | `int32_t` (4 byte) | `1~6` |
| `scoreThreshold` | 32 byte | `double` (8 byte) | JSON Number |
| `topK` | 1 byte | `int32_t` (4 byte) | 현재 `3` 고정 |
| `hasAlternatives` | 5 byte | `bool` (1 byte) | `true`, `false` |
| `candidates` | 최대 3개 | 고정 배열 | Score 내림차순 |
| `candidates[].rank` | 1 byte | `int32_t` (4 byte) | `1~3` |
| `candidates[].partNo` | 64 byte | `char[64]` | 널 종료 포함 |
| `candidates[].score` | 32 byte | `double` (8 byte) | JSON Number |

### 4.4 결과 규칙

- `scoreThreshold` 이상 후보만 반환한다.
- 후보는 Score 내림차순으로 정렬한다.
- 후보는 최대 `topK=3`개다.

## 5. 공통 버퍼 규격

### 5.1 전체 JSON 버퍼

| 구분 | 고정 버퍼 크기 | 단위 | 비고 |
| --- | ---: | --- | --- |
| 검사 요청 JSON | 8192 | UTF-8 byte | 널 종료 포함 |
| 검사 결과 JSON | 8192 | UTF-8 byte | 널 종료 포함 |
| 유사도 요청 JSON | 8192 | UTF-8 byte | 널 종료 포함 |
| 유사도 결과 JSON | 8192 | UTF-8 byte | 널 종료 포함 |

- C#은 호출 전에 전체 버퍼를 `0`으로 초기화한다.
- JSON 직렬화 결과와 널 종료 1 byte를 합한 크기는 8192 byte 이하여야 한다.
- 입력 함수는 크기 인자를 받지 않으므로 DLL은 최대 8192 byte 범위 안에서 널 종료를 확인한다.
- DLL은 전달받은 버퍼 크기를 초과해 읽거나 쓰면 안 된다.
- DLL은 결과 마지막에 반드시 널 종료 문자를 기록한다.
- 실행 중 버퍼를 재할당하지 않는다.

### 5.2 숫자와 배열 형식

| 항목 | JSON 최대 Size | C++에서의 형태 | 비고 |
| --- | ---: | --- | --- |
| `partNo`, `candidates[].partNo` | 64 byte | `char[64]` | 널 종료 포함, 실제 값 최대 63 byte |
| `viewName`, `viewJudge`, `indexNo`, `topK`, `rank` | 11 byte | `int32_t` (4 byte) | 정수 JSON 토큰 최대 크기 |
| Score, 기준값, 허용값, 좌표, 측정값, W/D/H | 32 byte | `double` (8 byte) | 지수 표기 없이 JSON Number 사용 |
| `hasAlternatives` | 5 byte | `bool` (1 byte) | `true` 또는 `false` |
| `measurementPoints` | 최대 5개 | 고정 배열 | `indexNo`는 1부터 연속 번호 |
| `measurements` | 최대 5개 | 고정 배열 | Thickness에서만 값 사용 |
| `candidates` | 최대 3개 | 고정 배열 | Score 내림차순 |

C#이 8192 byte를 `0`으로 초기화해 전달하고, C++은 정해진 필드에 값을 채운다. C#은 JSON을 관리 문자열로 복사하고 파싱한 뒤 네이티브 버퍼를 해제한다.

## 6. 메모리 소유권

1. C#이 고정 크기 입력 JSON 버퍼를 할당하고 값을 채운다.
2. DLL은 함수가 실행되는 동안에만 입력 버퍼를 읽는다.
3. DLL은 입력 포인터를 보관하거나 해제하지 않는다.
4. 함수 반환 후 C#이 입력 버퍼를 해제한다.
5. 결과 JSON 버퍼도 C#이 미리 할당해 DLL에 전달한다.
6. DLL은 결과 버퍼에 값만 기록하고 해당 버퍼를 해제하지 않는다.
7. C#은 결과를 관리 문자열로 복사한 후 결과 버퍼를 해제한다.
8. C# 관리 문자열과 JSON 파싱 객체는 .NET GC가 관리한다.

`VLAD_HD_Inference_Mat`이 반환하는 `detectData`는 JSON 버퍼와 별개다. `detectData`는 SDK 소유 포인터로 취급하며 C#에서 `Marshal.FreeHGlobal`을 호출하지 않는다. DLL이 추론마다 신규 할당하는 구조라면 AI DLL이 전용 해제 API를 제공해야 한다.
