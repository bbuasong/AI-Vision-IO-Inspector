# VLAD StringBuilder Detect_Str 결과 계약

작성일: 2026-07-16
최종 수정: 2026-07-16

## 목적

`VLAD_Custom_InferenceData_V1`이 채우는 `StringBuilder Detect_Str`는 일반 검사에서 다음 정보를 전달하는 결과 문자열이다.

- 이미지 정합 결과: `true` 또는 `false`
- AI Score: `0.00 ~ 100.00`
- 측정부별 실제 측정값: mm

이 문자열은 최종 OK/NG 판정, 측정값 비교, 이력 저장, 검사 화면 메시지에 사용된다. `Custom_Info_Struct` TLV는 검출 좌표 등 부가 정보용이며, 현재 측정값 기준 비교의 기본 입력은 `Detect_Str`이다.

유사 제품 후보의 순위, 품번, 품명, 유사도 Score는 `Detect_Str`에 포함하지 않는다. 해당 정보는 `VLAD_Search_Data`가 반환하는 별도 JSON 결과로 전달한다.

## 결과 채널 분리 원칙

현재 프로젝트의 AI 결과는 용도별로 아래 두 채널을 사용한다.

| 용도 | DLL 함수 | 반환 버퍼 | 형식 | 포함 정보 |
| --- | --- | --- | --- | --- |
| 일반 검사 | `VLAD_Custom_InferenceData_V1` | `StringBuilder Detect_Str` | 고정 순서 CSV | 이미지 정합, 검사 Score, 측정부별 실제값 |
| 등록 기준이미지 유사도 검색 | `VLAD_Search_Data` | `StringBuilder resultJson` | JSON | 방향, 후보 순위, 품번, 품명, 유사도 Score |

후보 목록을 `Detect_Str` CSV에 넣지 않는 이유는 다음과 같다.

1. 후보 수가 가변적이므로 측정부 값의 고정 순서와 충돌한다.
2. 품명에는 공백, 쉼표 등 CSV 구분자를 포함할 수 있다.
3. `rank`, `partNo`, `partName`, `score`는 하나의 후보 단위로 묶여야 하므로 위치 기반 토큰보다 JSON 객체가 안전하다.

따라서 일반 검사 결과를 JSON으로 즉시 전환하지 않는다. 현재 `Detect_Str` 파서와 측정부 비교는 CSV 계약을 사용하고 있으며, 유사도 검색에 필요한 가변 후보 정보는 이미 별도 JSON 채널로 처리할 수 있다.

## 호출 위치와 흐름

```text
검사 시작
  -> VladVisionInferenceEngine.InspectCapturedImages
  -> VLAD_Inference_Mat
  -> VladInferenceResultParser.Parse
  -> FillDrawResult
  -> FillCustomDrawResult
  -> VLAD_Custom_InferenceData_V1
  -> StringBuilder detectTextBuilder
  -> VladMeasurementMapper.TryParseStandardAiResult
  -> 기준값/허용오차 비교 및 최종 OK/NG
```

`VladInferenceResultParser.Parse`는 호출자 버퍼로 `new StringBuilder(8192)`를 생성한다. DLL은 이 버퍼에 결과 문자열을 기록해야 하며, 종료 문자를 포함하여 할당 범위를 넘겨 쓰면 안 된다.

현재 실제 호출 모양은 다음과 같다.

```csharp
VLAD_Custom_InferenceData_V1(
    vladId,
    detectData,
    rawData,
    classCount,
    detectText,
    customParameter,
    tlvInfo,
    tlvSize);
```

`detectText`만이 이 문서의 문자열 계약 대상이다. `classCount`, `tlvInfo`, `tlvSize`의 구조 계약은 별도 확인 대상이다.

## RTSP 콜백 경계

`VLAD_Ops_RTSP_Frame_Proc`는 RTSP 프레임을 수신해 최신 프레임 캐시에 복사하는 역할만 수행한다. 이 콜백에서는 `VLAD_Inference_Mat` 또는 `VLAD_Custom_InferenceData_V1`을 호출하지 않는다.

```text
RTSP callback
  -> 최신 프레임 메모리 캐시
  -> 검사 시작 시 캐시 프레임을 파일로 저장
  -> VLAD_Inference_Mat
  -> VLAD_Custom_InferenceData_V1
```

따라서 `Detect_Str`의 유일한 생산 경로는 검사 버튼으로 시작하는 추론 흐름이다. 매 RTSP 프레임에서 AI 추론을 수행하지 않으므로, 화면 갱신과 검사 추론이 서로 중복 실행되지 않는다.

## 유사도 검색 결과 JSON

`유사도 체크`는 등록 기준이미지 파일을 대상으로 `VLAD_Search_Mat`과 `VLAD_Search_Data`를 호출한다. 이 기능은 검사 이력이나 `Detect_Str`을 사용하지 않는다.

```text
유사도 체크
  -> 방향별 등록 기준이미지 Mat 로드
  -> VLAD_Search_Mat
  -> VLAD_Search_Data
  -> StringBuilder resultJson
  -> 후보 JSON 파싱
  -> 순위 / 품번 / 품명 / Score UI 표시
```

AI DLL이 `resultJson`에 반환해야 할 형식은 아래와 같다.

```json
{
  "schemaVersion": "1.0",
  "resultType": "SearchResult",
  "searchId": "20260716_103000_001",
  "sourcePartNo": "NEW_ITEM",
  "viewName": "Top",
  "scoreThreshold": 99.0,
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
  ]
}
```

| 필드 | 필수 여부 | 설명 |
| --- | --- | --- |
| `viewName` | 필수 | `Top`, `Front`, `Back`, `Left`, `Right`, `Thickness` 중 검색한 기준이미지 위치 |
| `candidates` | 필수 | 후보가 없으면 빈 배열 `[]` |
| `rank` | 필수 | 동일 방향 후보 안에서 1부터 시작하는 오름차순 순위 |
| `partNo` | 필수 | 학습 DB에 저장된 후보 품번 |
| `partName` | 필수 | 학습 DB에 저장된 후보 품명 |
| `score` | 필수 | 0.00~100.00 범위의 유사도 Score |
| `schemaVersion`, `resultType`, `searchId`, `sourcePartNo`, `scoreThreshold` | 권장 | DLL 결과 추적 및 향후 이력 확장용 메타데이터 |

현재 C# 파서는 `viewName`, `candidates`, 후보의 `rank`, `partNo`, `partName`, `score`를 실제 UI 모델로 변환한다. 권장 메타데이터는 수신해도 무시되므로, AI DLL은 지금부터 함께 반환할 수 있다. 필요 시 검색 이력 저장 기능을 만들 때 해당 메타데이터를 모델과 DB에 추가한다.

후보는 `rank` 오름차순으로 반환해야 하며, 후보가 기준 `scoreThreshold`보다 낮더라도 반환할 수 있다. 프로그램은 각 후보 Score와 기준값을 비교해 학습 DB 존재 여부를 표시한다.

## 기본 문자열 형식

```text
isMatched,score,measurement1,measurement2,...,measurementN
```

예시: 측정부가 4개인 경우

```text
true,98.50,150.00,60.00,290.00,10.00
```

| 순서 | 필드 | 예시 | 규칙 |
| --- | --- | --- | --- |
| 1 | `isMatched` | `true` | `true`, `ok`, `pass`, `1`은 일치. `false`, `ng`, `fail`, `0`은 불일치. 대소문자는 구분하지 않는다. |
| 2 | `score` | `98.50` | Score는 0~100 기준이다. 소수점은 반드시 `.`을 사용한다. |
| 3 이후 | `measurementN` | `150.00` | DB 측정부 `IndexNo` 오름차순과 같은 순서의 실제 측정값이다. 단위는 mm 고정이다. |

## 측정부 매핑 규칙

프로그램은 DB의 `Part.MeasurementRegions`를 `IndexNo` 오름차순으로 정렬한 뒤, 문자열의 세 번째 토큰부터 순서대로 연결한다.

```text
Detect_Str: true,98.50,150.00,60.00,290.00,10.00

IndexNo 1 -> 150.00 mm
IndexNo 2 ->  60.00 mm
IndexNo 3 -> 290.00 mm
IndexNo 4 ->  10.00 mm
```

- 반환 측정값 수와 DB 측정부 수가 다르면 작은 수까지만 매핑한다.
- 측정값을 반환하지 않는 경우에는 `true,98.50`처럼 두 토큰만 반환할 수 있다.
- 중간 측정값을 비우거나 문자열을 추가하면 이후 값의 순서가 어긋난다. 예를 들어 `true,98.50,150.00,,290.00`은 사용하면 안 된다.
- 현재 숫자 파서는 각 토큰에서 첫 번째 숫자를 읽는다. `150mm`도 150으로 읽힐 수 있지만, DLL은 단위 문자 없이 순수 숫자만 반환해야 한다.
- 음수값은 허용한다. 예: `true,98.50,-0.25,10.00`.

## 다중 카메라 처리 규칙

이미지 6장을 검사하면 엔진은 각 이미지 결과를 다음처럼 한 줄씩 합친다.

```text
[Top] true,98.50,150.00,60.00
[Front] true,98.20,150.00,60.00
[Thickness] true,98.70,150.00,60.00
```

`[Top]` 같은 위치 접두사는 엔진이 결과를 모을 때 추가하며, 원본 DLL이 넣지 않아도 된다. 파서는 접두사가 있으면 제거한 뒤 값을 읽는다.

중요한 현재 동작은 다음과 같다.

- 여러 줄 중 **측정값이 포함된 첫 번째 정상 행**을 표준 결과로 사용한다.
- 따라서 카메라마다 서로 다른 일부 측정값만 반환하면 현재 코드에서는 합쳐지지 않는다.
- 현 구조에서 AI는 첫 번째 측정값 포함 결과에 필요한 전체 `measurement1..N`을 반환해야 한다.
- 카메라별 측정값을 따로 반환해야 하는 요구가 생기면, `viewName`, `IndexNo`, 측정값을 포함한 JSON 또는 별도 구조체 계약으로 확장해야 한다.

## Score와 최종 판정

1. `isMatched`가 불일치면 즉시 NG다.
2. `score`가 존재하면 `INSPECTION_PASS_SCORE_THRESHOLD`와 비교한다.
3. 각 측정값을 DB의 기준값과 허용오차로 비교한다.
4. 이미지 정합, Score, 모든 측정부 비교가 모두 통과해야 최종 OK다.

내부 코드에서는 `98.50`을 `0.9850`으로 정규화해 보관하지만, 화면과 판정에서는 다시 100점 기준 `98.50`으로 비교한다.

## 현재 내부 테스트 데이터

현재 C# 파싱과 UI를 확인하기 위해 실제 네이티브 호출은 주석으로 보존하고 `TEST_VLAD_Custom_InferenceData_V1`을 사용 중이다.

```text
true,98.50,150.00,60.00,290.00,10.00
```

이 테스트는 문자열 파싱, 측정값 매핑, 기준값 비교, 최종 판정까지 확인한다. 다만 테스트 함수는 TLV 구조체를 채우지 않으므로 `Custom_Info_Struct` 검출 좌표와 클래스 정보는 검증하지 않는다.

실제 DLL 검증 시에는 아래 전환이 필요하다.

```csharp
// return TEST_VLAD_Custom_InferenceData_V1(...);
return VladNativeMethods.VLAD_Custom_InferenceData_V1(...);
```

## AI 담당자 확인 항목

1. `Detect_Str`가 위 CSV 토큰 순서와 100점 기준 Score를 반환하는지 확인한다.
2. 측정부 순서가 DB의 `IndexNo` 오름차순과 동일한지 확인한다.
3. 다중 카메라에서 전체 측정값을 어느 한 결과 행에 제공할 수 있는지 확인한다.
4. `StringBuilder(8192)` 버퍼 범위를 넘지 않도록 DLL의 문자열 기록 길이를 제한한다.
5. TLV가 필요한 경우 `Custom_Info_Struct`의 실제 메모리 레이아웃과 개수 계약을 별도로 확정한다.
6. `VLAD_Search_Data`는 후보 목록을 `resultJson` JSON으로 반환하고, 후보마다 `rank`, `partNo`, `partName`, `score`를 모두 채운다.
7. 유사도 후보 정보는 `Detect_Str`에 덧붙이지 않는다. 일반 검사 CSV와 검색 JSON의 결과 채널을 분리한다.
