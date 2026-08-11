# VLAD 결과 JSON 테스트 모드

작성일: 2026-07-20  
상태: C# 결과 수신 이후 처리 검증용, 기본 비활성

## 목적

AI 담당자가 새 `VLAD_SDK.dll`의 HD 결과 API를 제공하기 전에도, 애플리케이션이 결과 JSON을 받은 뒤 수행하는 아래 흐름을 검증한다.

```text
결과 JSON 수신
  -> VladInferenceResultParser JSON 파싱
  -> true/false, score, measurement1...N 표준 결과 문자열 변환
  -> VladMeasurementMapper의 IndexNo 순서 측정값 매핑
  -> 기준값/허용오차 비교
  -> 검사 이력과 UI 결과 표시
```

유사도 검색도 같은 방식으로 후보 목록 JSON 파싱과 화면 표시를 검증한다.

## 사용 설정

실행 EXE와 같은 위치의 `CFG\VladRuntimeSettings.json`에 아래 값을 설정한 뒤 프로그램을 다시 시작한다.

```json
{
  "UseTestResultJson": true
}
```

기본값은 반드시 `false`다. 실제 카메라/AI 검사, 학습, 현장 배포에서는 `false`로 유지한다.

## 테스트 동작

`UseTestResultJson=true`이면 `VladCamModeRuntime`은 네이티브 등록 대신 테스트 전용의 비어 있지 않은 Full/Crop ID를 만든다. 따라서 GPU, RTSP, VLAD DLL을 호출하지 않는다.

| 호출 위치 | 테스트 동작 |
| --- | --- |
| `VLAD_HD_Inference_Mat(fullImageVladId, croppedImageVladId, ...)` | 실제 Mat 추론 없이 테스트 detectData 핸들을 반환 |
| `TEST_VLAD_HD_InferenceData_Result(...)` | HD 검사 결과 JSON을 반환 |
| `VladInferenceResultParser` | 결과 JSON을 표준 결과 문자열로 변환 |
| `VLAD_Search_Mat(fullImageVladId, croppedImageVladId, ...)` | 테스트 searchData 핸들을 반환 |
| `TEST_VLAD_Search_Data(...)` | 유사품 후보 목록 JSON을 반환 |
| `VladSimilaritySearchResultParser` | 후보 순위, 품번, 품명, Score를 화면 모델로 변환 |

검사와 유사도 검색은 기존처럼 입력 이미지 파일을 OpenCV Mat로 열어 검사 흐름을 유지한다. 따라서 테스트할 기준/캡처 이미지 파일 자체는 존재해야 한다. 단, Mat를 받은 뒤 VLAD DLL 호출은 하지 않는다.

## 검사 결과 JSON Fixture

`TEST_VLAD_HD_InferenceData_Result`는 아래 의미의 JSON을 반환한다.

```json
{
  "schemaVersion": "1.0",
  "resultType": "InspectionResult",
  "inspectionId": "TEST_20260720_001",
  "partNo": "TEST-001",
  "partName": "Test Part",
  "viewName": "Thickness",
  "captureTime": "2026-07-20T10:30:00+09:00",
  "imageJudge": "PASS",
  "measurementJudge": "PASS",
  "overallJudge": "PASS",
  "score": 97.23,
  "scoreThreshold": 95.0,
  "dimensions": { "width": 100.0, "height": 120.0, "depth": 30.0, "unit": "mm" },
  "measurements": [
    { "measurementRegionId": 1, "indexNo": 1, "itemType": "Length", "measuredValue": 150.0, "specValue": 150.0, "toleranceMin": -0.5, "toleranceMax": 0.5, "judge": "PASS", "unit": "mm" },
    { "measurementRegionId": 2, "indexNo": 2, "itemType": "Width", "measuredValue": 60.0, "specValue": 60.0, "toleranceMin": -0.5, "toleranceMax": 0.5, "judge": "PASS", "unit": "mm" },
    { "measurementRegionId": 3, "indexNo": 3, "itemType": "Height", "measuredValue": 290.0, "specValue": 290.0, "toleranceMin": -0.5, "toleranceMax": 0.5, "judge": "PASS", "unit": "mm" },
    { "measurementRegionId": 4, "indexNo": 4, "itemType": "Thickness", "measuredValue": 10.0, "specValue": 10.0, "toleranceMin": -0.5, "toleranceMax": 0.5, "judge": "PASS", "unit": "mm" }
  ],
  "failureReasons": [],
  "message": "Test inspection completed successfully."
}
```

현재 파서는 이를 아래 표준 결과 문자열로 변환한다.

```text
true,97.23,150.00,60.00,290.00,10.00
```

측정값은 `indexNo` 오름차순으로 정렬되어 `Part.MeasurementRegions`의 `IndexNo` 순서에 매핑된다.

## 유사도 검색 JSON Fixture

`TEST_VLAD_Search_Data`는 아래 의미의 JSON을 반환한다.

```json
{
  "viewName": "Top",
  "hasAlternatives": true,
  "candidates": [
    { "rank": 1, "partNo": "TEST-001", "partName": "Test Similar Part 1", "score": 99.52 },
    { "rank": 2, "partNo": "TEST-002", "partName": "Test Similar Part 2", "score": 98.91 }
  ]
}
```

## UTF-8 버퍼 규칙

테스트 함수도 실제 HD DLL 계약과 동일하게 `requiredResultJsonBytes`를 UTF-8 byte 수로 계산하며 널 종료 문자 1 byte를 포함한다. 현재 fixture 검증값은 아래와 같다.

| 함수 | 반환값 | requiredResultJsonBytes | 파싱 결과 |
| --- | ---: | ---: | --- |
| `TEST_VLAD_HD_InferenceData_Result` | `1` | `1115` | PASS, Image/Measurement/Overall Judge, Score 97.23, 측정값 4개, failureReasons 0개 |
| `TEST_VLAD_Search_Data` | `1` | `200` | 후보 2개 |

`resultJsonCapacity`가 필요한 UTF-8 byte 수보다 작으면 `0`을 반환한다. 테스트 함수는 버퍼 범위를 넘겨 쓰지 않는다.

## 종료 조건

새 HD DLL을 적용하기 전 결과 파싱, 측정값 비교, 이력 저장, 유사품 후보 UI를 검증하는 용도로만 사용한다. 실제 DLL 검증 시에는 `UseTestResultJson=false`로 되돌린 뒤 실제 `VLAD_HD_InferenceData_Result`와 `VLAD_Search_Data` 응답으로 별도 검증해야 한다.
