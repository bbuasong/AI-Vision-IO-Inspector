# VLAD HD API 고정 JSON 버퍼 변경 사양

- 최초 작성일: 2026-08-03
- 최종 수정일: 2026-08-07
- 문서 버전: 1.3
- 참고:

## 1. VLAD_HD_Inference_Mat

### 1.1 네이티브 서명

```c
void* VLAD_HD_Inference_Mat(
    void* fullImageVladId,
    void* rawData,
    const char* requestJsonUtf8);
```

변경 사항:

- 기존 `croppedImageVladId`, `drawMode` 인자를 제거한다. `fullImageVladId` 하나만 사용한다.
- 업무 판정 기준은 JSON의 `scoreThreshold` 하나만 사용한다.
- `requestJsonUtf8`는 C#이 `0`으로 초기화해 할당한 8192 byte 버퍼이며, **요청과 결과가 이 버퍼
  하나를 공유한다.** C#이 요청 필드를 채워 넘기면, DLL은 같은 버퍼 안의 결과 필드(`viewJudge`/
  `score`/`dimensions`/`measurements`)만 갱신해 되돌려준다. 함수의 리턴값(`void*`)은 사용하지 않는다.
- C#은 입력 JSON이 8192 byte를 넘지 않는지 호출 전에 확인한다.
- 실제 검사 이미지는 `rawData`의 `cv::Mat*`로 전달하므로 이미지 경로는 JSON에 넣지 않는다.

### 1.2 일반 View 요청

일반 View에서도 Thickness와 같은 최상위 구성을 사용한다. 측정부가 없으므로 `measurementPoints`는
빈 배열이다. `viewJudge`/`score`/`dimensions`는 DLL이 채워 넣을 자리이므로 호출 전에는 `0`(또는
`0.00`)으로 채운다.

```json
{
  "partNo": "01100-51430",
  "viewName": 1,
  "viewJudge": 0,
  "score": 0.00,
  "scoreThreshold": 95.00,
  "dimensions": {
    "width": 0.00,
    "depth": 0.00,
    "height": 0.00
  },
  "measurementPoints": [],
  "measurements": []
}
```

### 1.3 Thickness 요청

```json
{
  "partNo": "01100-51430",
  "viewName": 6,
  "viewJudge": 0,
  "score": 0.00,
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
  ],
  "measurements": []
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
| `viewJudge`, `score`, `measurements` | 0/빈 배열로 전달 | 0/빈 배열로 전달 | 결과 자리 — DLL이 채운다 (1.8절 참고) |

`dimensions`를 요청 단계부터 `[]`가 아닌 객체로 유지하는 이유는, 요청과 결과가 같은 버퍼를
공유하는 이번 구조에서 호출 전후로 같은 키의 JSON 타입이 달라지면 C++ 파싱이 복잡해지기
때문이다. 요청 단계에서도 객체를 유지하고 `width`, `depth`, `height`를 `0.00`으로 전달한다.

### 1.6 측정부 IndexNo 규칙

1. DB에 저장된 측정부는 화면 표시 순서대로 1부터 연속 번호를 사용한다.
2. 중간 측정부를 삭제하면 뒤 측정부의 IndexNo를 앞으로 당긴다.
3. 측정부가 1개이면 `indexNo=1`이다.
4. C#은 검사 요청을 만들 때 현재 DB의 최종 정렬 순서로 IndexNo를 다시 정규화한다.
5. AI DLL은 요청의 IndexNo를 결과 `measurements[].indexNo`에 그대로 반환한다.

### 1.7 일반 View 결과

`VLAD_HD_Inference_Mat` 호출이 끝나면 C#은 **1.2절에서 넘긴 것과 같은 버퍼**를 다시 읽는다.
DLL은 `viewJudge`, `score`, `dimensions`, `measurements`만 갱신하고 나머지 필드는 그대로 둔다.

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
  "measurementPoints": [],
  "measurements": []
}
```

### 1.8 Thickness 결과

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
  ],
  "measurements": [
    {
      "indexNo": 1,
      "measuredValue": 150.10
    }
  ]
}
```

### 1.9 ViewJudge

결과의 `viewJudge`는 다음 정수 코드를 사용한다.

| `viewJudge` | 판정 |
| ---: | --- |
| `0` | PASS |
| `1` | FAIL |

### 1.10 결과 항목 Size

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

### 1.11 결과 규칙

- `viewJudge`는 현재 View의 AI 판정이며 `0=PASS`, `1=FAIL`이다.
- AI DLL은 업무상 `ERROR` 판정을 반환하지 않는다.
- 이상한 이미지 또는 측정값도 AI가 계산한 Score와 측정값으로 반환한다.
- 일반 5개 View의 `measurements`는 빈 배열이다.
- Thickness는 요청받은 측정부 수만큼 `indexNo`, `measuredValue`만 반환한다.
- W/D/H는 `dimensions.width`, `dimensions.depth`, `dimensions.height`로 반환한다.
- 단위는 프로그램 전체에서 `mm` 고정이므로 JSON에서 제외한다.

## 2. VLAD_Search_Mat

### 2.1 네이티브 서명

```c
void* VLAD_Search_Mat(
    void* fullImageVladId,
    void* rawData,
    const char* requestJsonUtf8);
```

- 기존 `croppedImageVladId`, `drawMode` 인자를 제거한다.
- 유사도 기준은 JSON의 `scoreThreshold`만 사용한다.
- `requestJsonUtf8`는 `VLAD_HD_Inference_Mat`과 같은 방식으로 **요청과 결과가 버퍼 하나를
  공유한다.** 리턴값(`void*`)은 사용하지 않는다.

### 2.2 요청 JSON

```json
{
  "viewName": 1,
  "scoreThreshold": 99.00,
  "topK": 3,
  "hasAlternatives": false,
  "candidates": []
}
```

`hasAlternatives`와 `candidates`는 결과와 같은 최상위 형식을 유지하기 위해 요청에도 기본값으로
포함하며, DLL이 호출 후 이 두 필드만 갱신한다.

### 2.3 결과 JSON

`VLAD_Search_Mat` 호출이 끝나면 C#은 2.2절에서 넘긴 것과 같은 버퍼를 다시 읽는다.

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

### 2.4 결과 항목 Size

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

### 2.5 결과 규칙

- `scoreThreshold` 이상 후보만 반환한다.
- 후보는 Score 내림차순으로 정렬한다.
- 후보는 최대 `topK=3`개다.

## 3. 공통 버퍼 규격

### 3.1 전체 JSON 버퍼

요청과 결과가 버퍼 하나를 공유하므로, 기능별로 버퍼가 하나씩만 필요하다.

| 구분 | 고정 버퍼 크기 | 단위 | 비고 |
| --- | ---: | --- | --- |
| 검사 JSON 버퍼 (`VLAD_HD_Inference_Mat` 요청·결과 공용) | 8192 | UTF-8 byte | 널 종료 포함 |
| 유사도 JSON 버퍼 (`VLAD_Search_Mat` 요청·결과 공용) | 8192 | UTF-8 byte | 널 종료 포함 |

- C#은 호출 전에 전체 버퍼를 `0`으로 초기화한다.
- JSON 직렬화 결과와 널 종료 1 byte를 합한 크기는 8192 byte 이하여야 한다.
- 입력 함수는 크기 인자를 받지 않으므로 DLL은 최대 8192 byte 범위 안에서 널 종료를 확인한다.
- DLL은 전달받은 버퍼 크기를 초과해 읽거나 쓰면 안 된다.
- DLL은 결과 갱신 후에도 반드시 널 종료 문자를 유지한다.
- 실행 중 버퍼를 재할당하지 않는다.

### 3.2 숫자와 배열 형식

| 항목 | JSON 최대 Size | C++에서의 형태 | 비고 |
| --- | ---: | --- | --- |
| `partNo`, `candidates[].partNo` | 64 byte | `char[64]` | 널 종료 포함, 실제 값 최대 63 byte |
| `viewName`, `viewJudge`, `indexNo`, `topK`, `rank` | 11 byte | `int32_t` (4 byte) | 정수 JSON 토큰 최대 크기 |
| Score, 기준값, 허용값, 좌표, 측정값, W/D/H | 32 byte | `double` (8 byte) | 지수 표기 없이 JSON Number 사용 |
| `hasAlternatives` | 5 byte | `bool` (1 byte) | `true` 또는 `false` |
| `measurementPoints`, `measurements` | 최대 5개 | 고정 배열 | `indexNo`는 1부터 연속 번호 |
| `candidates` | 최대 3개 | 고정 배열 | Score 내림차순 |

C#이 8192 byte를 `0`으로 초기화해 전달하고, C++은 정해진 결과 필드에만 값을 채운다. C#은 같은
버퍼에서 JSON을 관리 문자열로 복사하고 파싱한 뒤 자신이 할당했던 버퍼를 해제한다.

## 4. 메모리 소유권

1. C#이 고정 크기 버퍼를 할당하고 요청 필드 값을 채운다.
2. DLL은 함수가 실행되는 동안에만 이 버퍼를 읽고 쓴다.
3. DLL은 버퍼 포인터를 보관하거나 해제하지 않는다.
4. 함수 반환 후 C#이 같은 버퍼에서 결과를 관리 문자열로 복사한다.
5. C#은 버퍼를 처음 할당한 주체이므로, 사용이 끝나면 C#이 직접 해제한다(`Marshal.FreeHGlobal`).
   DLL이 별도로 할당한 메모리를 해제하는 것이 아니므로 할당자 불일치로 인한 힙 손상 위험이 없다.
6. C# 관리 문자열과 JSON 파싱 객체는 .NET GC가 관리한다.

`VLAD_HD_Inference_Mat`/`VLAD_Search_Mat`의 리턴값(`void*`)은 사용하지 않는다. v1.2에 있던
`detectData`/`searchData` 핸들을 별도 조회 함수에 넘겨 결과를 받는 2단계 구조는 더 이상
사용하지 않는다.
