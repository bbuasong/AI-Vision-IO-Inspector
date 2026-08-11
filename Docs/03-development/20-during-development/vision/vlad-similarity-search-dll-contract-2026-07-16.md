# VLAD 학습 DB 이미지 유사도 검색 DLL 계약

작성일: 2026-07-16
최종 수정: 2026-07-20

## 목적

> 2026-07-20부터 목표 HD DLL의 UTF-8 ABI, boolean `hasAlternatives`, 결과 버퍼 규칙은 `vlad-hd-json-interface-contract-2026-07-20.md`를 우선한다. 이 문서는 현재 C# 호출부와 기존 테스트 대체 경로를 기록한다.

부품등록 화면의 `유사도 체크`는 이미 저장된 등록 기준이미지를 학습 이미지 DB와 비교하는 기능이다.

- 카메라 촬영, RTSP 재접속, 검사 이력 저장을 수행하지 않는다.
- 등록 기준이미지 파일을 OpenCV `Mat`으로 로드하여 방향별로 검색한다.
- 일반 검사에 필요한 부품 정보, 분류 정보, 측정부 좌표, 기준값, 허용오차는 전달하지 않는다.
- 촬영 방향(`viewName`)이 다른 이미지를 서로 비교하지 않도록 `viewName`은 반드시 전달한다.
- 기준 Score(`scoreThreshold`)는 후보 표시 및 학습 DB 존재 여부 판단의 기준으로 전달한다.

## 호출 흐름

```text
유사도 체크 버튼
  -> 등록 기준이미지 파일 확인
  -> 방향별 PNG를 OpenCV Mat으로 로드
  -> viewName, scoreThreshold JSON 생성
  -> VLAD_Search_Mat 호출
  -> VLAD_Search_Data 호출
  -> 후보 목록 JSON 파싱
  -> 위치 / 학습 DB / 순위 / 품번 / 품명 / 유사도 표시
```

## AI DLL Export

AI 담당자는 x64 `VLAD_SDK.dll`에 아래 export를 추가한다.

```csharp
IntPtr VLAD_Search_Mat(
    IntPtr vladId,
    IntPtr rawData,
    float threshold,
    int drawMode,
    string searchContextJson);

int VLAD_Search_Data(
    IntPtr vladId,
    IntPtr searchData,
    StringBuilder resultJson,
    int resultJsonCapacity);
```

문자열은 기존 VLAD SDK와 같은 Windows ANSI `char*` 계약이다. C# P/Invoke는 `CharSet.Ansi`를 사용한다.

- `resultJsonCapacity`는 ANSI 문자 버퍼 크기이며 현재 프로그램은 `65536`을 전달한다.
- DLL은 종료 문자를 포함하여 이 크기를 넘겨 쓰면 안 된다.
- `VLAD_Search_Data`는 성공 시 `1`, 실패 시 `0`을 반환한다.
- `rawData`, `resultCount`, TLV, custom parameter는 Search API에서 사용하지 않는다.

## Search 입력 JSON

```json
{
  "viewName": "Thickness",
  "scoreThreshold": 99.0
}
```

`viewName` 값은 `Top`, `Front`, `Back`, `Left`, `Right`, `Thickness` 중 하나다.

## Search 결과 JSON

```json
{
  "viewName": "Top",
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

- 후보가 없으면 `candidates`는 빈 배열로 반환한다.
- 후보별 `score`는 `0.00~100.00` 범위다.
- 프로그램은 `score >= scoreThreshold`일 때 `학습 DB 존재`로 표시한다.
- `partNo`, `partName`은 검색 결과 후보 식별용 출력값이며, Search 입력으로 전달하지 않는다.

## 내부 테스트 대체 결과

현재 C# 파싱 및 UI 확인을 위해 `VLAD_Ops_Ai`는 아래 두 네이티브 호출을 임시 주석 처리하고 내부 테스트 함수를 사용한다.

```csharp
// return VladNativeMethods.VLAD_Custom_InferenceData_V1(...);
return TEST_VLAD_Custom_InferenceData_V1(...);

// int nativeResult = VladNativeMethods.VLAD_Search_Data(...);
int nativeResult = TEST_VLAD_Search_Data(...);
```

- `TEST_VLAD_Custom_InferenceData_V1` detectText: `true,98.50,150.00,60.00,290.00,10.00`
- `TEST_VLAD_Search_Data` resultJson: 순위 1~2, 품번, 품명, 유사도를 포함한 후보 목록 JSON
- 이 대체 경로는 C#의 결과 문자열 파싱, 측정값 비교, 후보 목록 UI를 확인하기 위한 용도다.
- 새 VLAD DLL 검증을 시작할 때는 두 테스트 호출을 다시 주석 처리하고, 보존된 네이티브 호출의 주석을 해제해야 한다.

## 구현 상태

- C# 호출부, 입력 JSON 생성, 결과 JSON 파싱, 후보 UI 표시는 구현되어 있다.
- 현재 배포된 `VLAD_SDK.dll`에는 이 export가 없으므로, 실제 호출 검증은 AI 담당자가 위 계약을 구현한 x64 DLL을 제공한 뒤 진행한다.
