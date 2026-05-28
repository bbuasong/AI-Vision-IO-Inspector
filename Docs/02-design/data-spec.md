# 데이터 명세 초안

`data-model.md`의 논리 모델을 구현 시 확인할 수 있도록 필드 단위로 정리한다.

## 부품 기준정보

| 필드 | 설명 | 필수 | 비고 |
| --- | --- | --- | --- |
| PartNo | 품번 | Y | 중복 불가 |
| PartName | 품명 | Y |  |
| CategoryCode | 분류코드 | Y | 코드 체계 확인 필요 |
| CategoryDescription | 분류설명 | N |  |
| PartType | 구분 | N | 고객 정의 필요 |
| CreatedAt | 등록일시 | Y | 시스템 생성 |
| UpdatedAt | 수정일시 | Y | 시스템 생성 |

## 기준 이미지

| 필드 | 설명 | 필수 | 비고 |
| --- | --- | --- | --- |
| PartNo | 품번 | Y | 부품 기준정보 연결 |
| ViewType | 촬영 방향 | Y | Top, Front, Back, Left, Right, Thickness |
| FilePath | 파일 경로 | Y | 상대/절대 정책 확인 |
| CapturedAt | 촬영일시 | N |  |

## 측정부

| 필드 | 설명 | 필수 | 비고 |
| --- | --- | --- | --- |
| PartNo | 품번 | Y |  |
| MeasurementName | 측정부명 | Y | 예: 측정부 1 |
| ViewType | 기준 이미지 방향 | Y | 측정 위치가 속한 뷰 |
| Coordinates | 측정 좌표 | N | 좌표 포맷 확정 필요 |
| NominalValue | 기준값 | Y |  |
| ToleranceMin | 하한 허용오차 | N | 판정 기준 확인 필요 |
| ToleranceMax | 상한 허용오차 | N | 판정 기준 확인 필요 |
| Unit | 단위 | N | mm 후보 |

## 검사 이력

| 필드 | 설명 | 필수 | 비고 |
| --- | --- | --- | --- |
| InspectionId | 검사 ID | Y | 시스템 생성 |
| PartNo | 품번 | Y |  |
| InputCode | 입력 라벨/바코드 | N | 입력 방식 확정 필요 |
| Result | OK/NG/Error | Y | 오류와 NG 구분 |
| InspectedAt | 검사일시 | Y |  |
| ElapsedMs | 검사 소요시간 | N | 통계 사용 |

## 검사 측정값

| 필드 | 설명 | 필수 | 비고 |
| --- | --- | --- | --- |
| InspectionId | 검사 ID | Y |  |
| MeasurementRegionId | 측정부 ID | Y |  |
| MeasuredValue | 측정값 | Y |  |
| Result | OK/NG | Y | 측정부별 판정 |
