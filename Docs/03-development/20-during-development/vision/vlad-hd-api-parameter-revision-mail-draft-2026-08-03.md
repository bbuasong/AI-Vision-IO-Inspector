# VLAD HD API 파라미터 정리 회신 메일 초안

작성일: 2026-08-03  
수신 대상: AI/VLAD SDK 담당자  
상태: 발송 전 검토 필요

## 제목

`[검토 요청] VLAD HD 검사/유사도 API 파라미터 정리 제안`

## 본문

안녕하세요.  
FA사업2팀 송영준입니다.

7월 27일 및 8월 3일에 회신해 주신 VLAD API 파라미터 재확인 의견을 검토했습니다.

말씀해 주신 불필요한 항목 전달, Thickness 중심 구조, 가변 JSON 메모리 관리 문제를 반영해 아래와 같이 정리하고자 합니다. 동의 여부와 추가로 필요한 항목을 확인 부탁드립니다.

### 1. 검사 View 구분

- Top/Front/Back/Left/Right는 이미지 검사 정보만 전달합니다.
- Thickness는 이미지 검사 정보와 측정부 정보를 함께 전달합니다.
- 일반 5개 View에는 `measurementPoints`를 전달하지 않습니다.

### 2. 공통 입력 정보

다음 항목만 공통으로 전달하는 안입니다.

- `schemaVersion`
- `inspectionId`
- `partNo`
- `partName`
- `viewName`
- `scoreThreshold`

품명은 Request와 Result 모두 `partName`으로 통일하고 기존 `productName`은 사용하지 않으려 합니다.

Thickness에는 아래 측정부 정보만 추가합니다.

- `measurementRegionId`
- `indexNo`
- `itemType`
- `lineColor`
- `nominalValue`
- `toleranceMin`, `toleranceMax`
- `x1`, `y1`, `x2`, `y2`
- `unit`

`categoryNo`, `categoryName`, `partType`, `captureTime`, `capturedImagePath`, 중복 `tolerance`는 DLL에서 사용하지 않는다면 제외하려 합니다. `lineColor`는 좌표 이미지에서 측정부를 구분하는 데 필요한지 확인 후 유지 여부를 확정하려 합니다.

### 3. 판정 주체

- `VLAD_HD_Inference_Mat`에는 설정된 `scoreThreshold`를 전달합니다.
- 이미지 PASS/FAIL은 AI에서 판단합니다.
- Thickness 측정부 PASS/FAIL도 AI에서 판단합니다.
- C# 프로그램은 Score 및 기준값/허용오차를 다시 계산해 판정을 변경하지 않고 AI 결과를 화면과 이력에 표시합니다.
- 프로그램은 6개 View의 AI 판정값을 검사 한 건으로 묶는 집계만 수행합니다.

결과의 `overallJudge`는 제품 전체 판정으로 오해할 수 있어 현재 View의 최종 판정인 `viewJudge`로 변경하는 안을 제안드립니다. 제품 전체 판정이 별도로 필요하면 `inspectionJudge`로 구분하겠습니다.

### 4. 유사도 검색

`VLAD_Search_Mat`에는 다음 항목을 전달하려 합니다.

- `viewName`
- `scoreThreshold`
- `topK` (`3`)

`VLAD_Search_Data`는 AI에서 기준 이상으로 판단한 후보만 Score 내림차순으로 최대 3개 반환하고, 프로그램에서는 후보 Score를 다시 비교하지 않는 방식입니다.

후보의 품명도 `partName`으로 통일합니다.

### 5. JSON 메모리 관리

메모리 소유권은 다음 방식으로 제안드립니다.

- 입력 JSON은 C#에서 널 종료 UTF-8 버퍼로 할당합니다.
- DLL은 함수 실행 중에만 입력 포인터를 사용하고 반환 이후 보관하거나 해제하지 않습니다.
- 결과 JSON은 C#이 버퍼와 byte 용량을 전달합니다.
- 버퍼가 부족하면 DLL이 `requiredResultJsonBytes`를 채우고 버퍼 부족 코드를 반환합니다.
- C#은 필요한 크기로 한 번 재할당해 재호출합니다.

이 방식이면 SDK에서 결과 문자열 메모리를 별도로 Alloc하여 반환하지 않아도 되므로 Free 시점과 Memory Leak 위험을 줄일 수 있다고 판단했습니다.

### 6. W/D/H 결과와 최초 지연 확인

결과 이미지의 이미지 영역을 침범하지 않는 하단 정보 영역에 W/D/H를 표시해야 하므로, `dimensions`는 기본 결과에서 제외하지 않고 다음 값을 포함하려 합니다.

- `width`
- `depth`
- `height`
- `unit` (`mm`)

또한 최초 약 10초 동안 화면이 표시되지 않는 현상은 JSON 처리 비용과 별도로 확인할 필요가 있습니다. 가능하다면 다음 구간별 시작/종료 시각 또는 경과 시간을 로그로 확인 부탁드립니다.

- `VLAD_Custom_Registration` 호출부터 반환까지
- 모델 로드 구간
- CUDA/cuDNN 초기화 구간
- RTSP 등록부터 첫 프레임 Callback까지
- 첫 `VLAD_HD_Inference_Mat` 호출부터 반환까지

C#에서는 `VLAD_Custom_Registration` 전체 호출 시간, 첫 추론 전체 호출 시간, JSON 생성 및 결과 파싱 시간을 측정하겠습니다. 모델 로드와 CUDA/cuDNN 초기화가 DLL 내부에서 수행된다면 두 구간은 SDK 내부 로그가 있어야 분리할 수 있으므로 확인 부탁드립니다. 화면 표시 지연이 첫 추론 전에 발생한다면 첫 추론은 직접 원인이 아닐 수 있어, 단계별 시간을 기준으로 병목을 판단하려 합니다.

### 7. 확정사항 및 확인 요청사항

1. 위 공통 필드 중 AI에서 사용하지 않는 항목이 있는지 확인 부탁드립니다.
2. 일반 5개 View에는 `measurementPoints`를 전달하지 않는 것이 맞는지 확인 부탁드립니다.
3. Thickness 측정부 필드 중 추가 또는 제거할 항목이 있는지 확인 부탁드립니다.
4. `scoreThreshold`와 기준값/허용오차를 이용한 PASS/FAIL을 AI에서 모두 판단할 수 있는지 확인 부탁드립니다.
5. `overallJudge` 대신 현재 View 최종 판정인 `viewJudge`를 반환하는 방식이 가능한지 확인 부탁드립니다. 제품 전체 판정이 별도로 필요하면 `inspectionJudge`로 구분하는 안도 함께 확인 부탁드립니다.
6. 결과 이미지 하단에 W/D/H를 표시하기 위해 `dimensions.width`, `dimensions.depth`, `dimensions.height`, `dimensions.unit`을 기본 결과로 반환할 수 있는지 확인 부탁드립니다.
7. 유사도 검색 결과는 기준 이상 후보만 Score 내림차순으로 최대 3개 반환하도록 하겠습니다.
8. 입력/출력 JSON의 호출자 소유 메모리 방식과 버퍼 부족 처리 방식 적용 가능 여부를 확인 부탁드립니다.
9. 최초 약 10초 지연 원인을 확인할 수 있도록 Registration, 모델 로드, CUDA/cuDNN 초기화, RTSP 등록 후 첫 프레임 Callback, 첫 추론 시간을 구분한 로그 제공 가능 여부를 확인 부탁드립니다.

동의해 주시는 내용을 기준으로 C# 프로그램의 JSON 생성, 결과 파싱 및 내부 재판정 부분을 수정하겠습니다.

감사합니다.
