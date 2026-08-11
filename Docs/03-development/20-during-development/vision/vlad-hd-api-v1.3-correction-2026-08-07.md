# VLAD HD API v1.3 정정 (AI 벤더 회신)

- 작성일: 2026-08-07
- 근거 메일: `Docs\00-inbox\mail\RE_ _첨부_ 파라미터 정의.msg` (발신 이근호, 2026-08-07 13:43, 수신 openmind@linkgenesis.co.kr)
- 대상 이전 문서: `VLAD_HD_Inference_Mat수정-2026-08-05.md` (v1.2, 이 문서가 그 내용을 메일로 보낸 원본 제안임)

## 1. 정정 배경

2026-08-05에 제안한 v1.2 스펙(`VLAD_HD_Inference_Mat`을 두 VladId + `drawMode` + 요청 JSON 5인자로,
결과는 `VLAD_HD_InferenceData_Result`로 별도 조회)에 대해 AI 벤더(이근호)가 2026-08-07에 회신하며
구조를 정정했다. 회신 메일에는 새로 빌드한 `VLAD_SDK.dll`(258,560 byte)과 `HD_Dll.dll`(42,496 byte)이
실제로 첨부되어 있었다.

## 2. 구조 정정 — 결과 조회를 별도 함수로 분리하지 않는다

기존에는 다음과 같이 이해하고 있었다.

- `VLAD_HD_Inference_Mat` 호출 → detectData(또는 핸들) 반환
- `VLAD_HD_InferenceData_Result` 별도 호출 → 결과 JSON 조회

(기존 `VLAD_Inference_Mat` → `VLAD_InferenceData_V1_Draw`/`VLAD_Custom_InferenceData_V1`처럼
핸들을 반환받아 별도 함수로 결과를 다시 읽는 2단계 패턴과 같은 모양으로 설계했었다.)

AI 벤더 확인 결과 실제 구현은 다르다.

- **`VLAD_HD_Inference_Mat` 한 번의 호출로 요청 전달과 결과 채움이 모두 끝난다.**
- 별도 결과 조회 함수가 필요 없다. 그래서 결과 수신 단계를 명확히 하려고 제안했던
  `VLAD_HD_InferenceData_Result`는 AI 벤더 구현과 맞지 않아 불필요하다.
- `VLAD_Search_Mat` / `VLAD_Search_ResultData`도 동일한 이유로 **`VLAD_Search_Mat` 하나만 사용**하기로
  확정했다. `VLAD_Search_ResultData`는 사용하지 않는다.

> **리턴값(`void*`) 사용 안 함 — 결과는 요청 버퍼를 그대로 재사용해 받는다 (2026-08-07 확정).**
> 요청/결과가 **같은 8192byte 버퍼를 공유하는 in-place 업데이트** 구조다.
> 1. C#이 8192byte 버퍼를 `0`으로 초기화해 할당한다.
> 2. 그 버퍼에 요청 JSON(`partNo`/`viewName`/`scoreThreshold`/`measurementPoints`, `viewJudge`/`score`/
>    `dimensions`는 자리만 채운 placeholder)을 기록한다.
> 3. 이 버퍼 포인터를 `requestJsonUtf8`로 넘겨 `VLAD_HD_Inference_Mat`을 호출한다. **리턴값(`void*`)은
>    사용하지 않는다.**
> 4. AI DLL은 **같은 버퍼**를 열어 `viewJudge`/`score`/`dimensions`/`measurements`만 갱신해서 되돌려준다.
>    `partNo`/`viewName`/`scoreThreshold`/`measurementPoints`는 DLL이 그대로 두거나 무시한다.
> 5. C#이 같은 버퍼를 다시 읽어 결과를 파싱하고, 사용이 끝나면 C#이 직접 해제한다.
>
> 이 구조라면 별도 결과 버퍼, `VLAD_HD_InferenceData_Result` 호출, `detectData` 리턴값 해석이 전부
> 필요 없다. 메모리도 C#이 할당부터 해제까지 전 구간을 소유하므로 DLL 쪽 할당자와 섞일 위험이 없다.
> `VLAD_Search_Mat`도 동일한 in-place 버퍼 방식을 따르는 것으로 간주한다(요청 시 명시적으로 재확인된
> 것은 `VLAD_HD_Inference_Mat` 쪽이며, Search 쪽은 동일 패턴을 따른다는 전제).
>
> 이전 초안(2026-08-05)의 1.1절 "`requestJsonUtf8`는 널 종료된 UTF-8 JSON이며 DLL은 읽기만 한다"는
> 문구는 이 in-place 갱신 구조와 맞지 않아 더 이상 유효하지 않다.

## 3. 정정된 네이티브 시그니처

두 함수 모두 기존 5인자(전체/Crop 두 VladId + `drawMode` + 요청 JSON)에서
**3인자(단일 VladId + `rawData` + 요청 JSON)로 축소**된다. `croppedImageVladId`와 `drawMode`는 모두 제거한다.

```c
void* VLAD_HD_Inference_Mat(
    void* fullImageVladId,
    void* rawData,
    const char* requestJsonUtf8);

void* VLAD_Search_Mat(
    void* fullImageVladId,
    void* rawData,
    const char* requestJsonUtf8);
```

`DrawMode`를 제거한 사유는 메일에 "DrawMode 필요 없음"으로만 명시되어 있고,
`croppedImageVladId` 제거는 시그니처 diff에서만 확인되며 별도 사유 설명은 없다.

## 4. 폐기되는 함수

다음 두 함수는 AI 벤더가 "필요 없음. 혹시 필요한 이유가 있으면 전달 요망"으로 회신했다.
현재는 만들지 않기로 확정되었으며, 필요하면 별도로 사유를 정리해 재요청해야 한다.

- `VLAD_HD_InferenceData_Result`
- `VLAD_Search_ResultData`

## 5. 첨부 DLL 확인 결과

메일에 첨부된 `VLAD_SDK.dll`/`HD_Dll.dll`을 열어 직접 확인했다.

- 리포지토리의 `Native\VLAD\VLAD_SDK.dll`(258,048 byte)과 SHA256이 다르다. **아직 리포지토리에
  반영되지 않은 새 빌드다.**
- `HD_Dll.dll`은 파일 크기(42,496 byte)는 기존과 같지만 SHA256이 달라 내부적으로 재빌드된 것으로
  보인다.
- 새 `VLAD_SDK.dll`의 export 심볼을 직접 추출해 메일 내용을 대조 확인했다.
  - `VLAD_HD_Inference_Mat`, `VLAD_HD_Registration`, `VLAD_HD_ImageMerge`, `VLAD_Search_Mat` — export 존재.
  - `VLAD_HD_InferenceData_Result`, `VLAD_Search_ResultData` — **export 목록에 없음**. 메일 내용과 일치한다.
  - extern "C" 내보내기라 심볼 이름만으로는 인자 개수·타입까지 검증할 수는 없다. 3인자 시그니처
    여부는 메일 텍스트 근거이며, DLL 바이너리로 인자 개수까지 교차검증한 것은 아니다.

## 6. 현재 코드베이스와의 차이

`AI.Vision.IOInspector.Vision\LegacyVlad\VLAD_Ops_Ai.cs`/`VladNativeMethods.cs`,
`AI.Vision.IOInspector.Vision\Engines\VladVisionInferenceEngine.cs`는 여전히 v1.2 설계
(두 VladId + `drawMode` + `VLAD_HD_InferenceData_Result`/`VLAD_Search_ResultData` 2단계 조회) 그대로다.

- 새 DLL을 그대로 붙이면 `VLAD_HD_Inference_Mat`/`VLAD_Search_Mat` 호출의 인자 개수가 실제 네이티브
  함수(3개)와 맞지 않아 스택이 어긋나며 `AccessViolationException` 위험이 있다.
- `VLAD_HD_InferenceData_Result`/`VLAD_Search_ResultData` 호출은 export가 없어
  `EntryPointNotFoundException`이 발생하는데, 이 경로는 기존 코드가 이미 catch하여 안전하게
  fallback하도록 되어 있다.

## 7. 확정된 최종 호출 구조 (2026-08-07)

1. C#이 8192byte UTF-8 버퍼를 `0`으로 초기화해 할당하고, 요청 JSON을 기록한다.
2. `VLAD_HD_Inference_Mat(fullImageVladId, rawData, requestJsonUtf8=그 버퍼)`를 호출한다(3인자,
   `croppedImageVladId`·`drawMode` 없음). 리턴값(`void*`)은 사용하지 않는다.
3. 같은 버퍼에서 `viewJudge`/`score`/`dimensions`/`measurements`를 다시 읽는다.
4. 사용이 끝나면 C#이 그 버퍼를 직접 해제한다 (`Marshal.FreeHGlobal` 등, DLL 쪽 할당자와 무관).
5. `VLAD_Search_Mat`도 동일한 3인자·in-place 버퍼 패턴을 따른다.
6. 기존 구버전(non-HD) 호환 경로(`VLAD_Inference_Mat` 단일 ID + `VLAD_InferenceData_V1_Draw`/
   `VLAD_Custom_InferenceData_V1`/`VLAD_InferenceData_V2_Draw` 기반 결과 파싱)는 **구버전 프로그램을
   이관하는 과정에서 남은 코드이며 더 이상 필요 없다** — 정리 대상.

## 8. 남은 작업

1. ~~반환값이 무엇을 가리키는지~~ — 확정됨(사용 안 함, 7절 참고).
2. P/Invoke 시그니처를 3인자로 재설계 (`VladNativeMethods.cs`, `VLAD_Ops_Ai.cs`).
3. `VLAD_HD_Inference_Mat`/`VLAD_Search_Mat` 호출부를 in-place 버퍼 방식으로 재설계하고,
   `VLAD_HD_InferenceData_Result`/`VLAD_Search_ResultData` 호출과 구버전 호환 경로를 제거한다.
4. 새 `VLAD_SDK.dll`/`HD_Dll.dll`을 `Native\VLAD\`에 반영할지 결정 (현재 DLL과 SHA256 다름).

2026-08-07 세션에서는 분석만 진행하고 위 4개 항목은 착수하지 않기로 했다.
