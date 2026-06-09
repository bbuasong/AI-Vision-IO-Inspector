# 데이터 명세 초안

`data-model.md`의 논리 모델을 구현 시 확인할 수 있도록 필드 단위로 정리한다.

## SQLite DB 구성

로컬 상용 무료 DB는 SQLite를 사용한다. 개발 기준 DB 파일은 관리 중인 솔루션 폴더의 `Tests/AI-Vision IO Inspector/DB/DataBase.db`이다. 앱 실행 시 `DataBase.db`가 없으면 빈 스키마만 생성하며, 특정 CSV 파일을 찾아 자동 적재하지 않는다.

| 구분 | 테이블 | 역할 |
| --- | --- | --- |
| Part List | `PartList_Categories` | 분류코드/분류설명 |
| Part List | `PartList_Parts` | 품번, 품명, 분류코드, 분류설명, 구분 |
| Part List | `PartList_MeasurementSets` | 품번별 측정부 세트. 예: 측정부, 측정부2 |
| Part List | `PartList_MeasurementItems` | 세트별 길이/너비/높이/두께 기준값, 허용값, 단위, 사용 여부 |
| Part List | `PartList_ReferenceImages` | 기준 이미지 위치, 실제 파일경로, 표시용 관리경로 |
| History | `History_Inspections` | 검사 이력 헤더와 검사 당시 품번/분류 스냅샷 |
| History | `History_Measurements` | 검사 당시 측정값, 기준값, 허용값, 판정 |
| History | `History_CapturedImages` | 검사 당시 촬영 이미지 경로 |
| History | `History_Events` | 검사 이벤트 로그 |

`export_Test.csv`는 테스트용 1회성 초기 데이터 원본이다. 이미 `DataBase.db`에 적재했으며, 향후 프로그램 실행이나 배포는 이 파일에 의존하지 않는다.

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

### 분류코드 정합성 정책

`PartList_Categories`는 분류코드/분류설명의 기준 목록으로 사용한다. 부품 저장 시 분류코드가 신규이면 신규 분류로 등록하고, 이미 등록된 분류코드이면 기존 분류설명과 입력 분류설명이 일치할 때만 저장한다. 서로 다르면 DB 저장을 차단하고 차단 사유를 팝업으로 표시한다.

다중품목 CSV 저장에서도 같은 정책을 적용한다. CSV 내부에서 같은 분류코드가 서로 다른 분류설명을 갖거나, 기존 DB의 분류설명과 CSV의 분류설명이 다르면 저장하지 않는다.

## 기준 이미지

| 필드 | 설명 | 필수 | 비고 |
| --- | --- | --- | --- |
| PartNo | 품번 | Y | 부품 기준정보 연결 |
| ViewType | 촬영 방향 | Y | Top, Front, Back, Left, Right, Thickness |
| FilePath | 파일 경로 | Y | 이미지 DB 폴더의 실제 파일 경로를 직접 참조 |
| CapturedAt | 촬영일시 | N |  |

### 기준 이미지 파일 관리 규칙

| 항목 | 규칙 |
| --- | --- |
| 등록 가능 방향 | Top, Front, Back, Left, Right, Thickness 6개 |
| 유니크 기준 | 한 부품 안에서 ViewType별 1개만 현재 이미지로 관리 |
| 최대 등록 수 | 부품 1개당 현재 이미지 최대 6개 |
| 저장 폴더 | 관리 중인 솔루션 폴더 기준 `DB/Image/{분류코드}/{품번}` |
| 현재 이미지 파일명 | `{품번}_{ViewType}.{확장자}` 예: `04026346_Top.png` |
| 같은 ViewType 재등록 | 기존 현재 이미지를 `{품번}_{ViewType}_OldVer_{yyyyMMdd_HHmmssfff}.{확장자}`로 백업 후 새 파일로 교체 |
| 조회 방식 | 저장된 `FilePath`로 직접 접근하며 폴더 전체 검색을 전제로 하지 않음 |
| 화면 표시 관리경로 | 실제 절대경로 대신 `REFERENCE:\\{분류코드}\{품번}` 형식으로 축약 표시 |
| 부품 삭제 | 부품 기준정보만 삭제하며 기준 이미지 파일과 OldVer 백업 파일은 프로그램이 자동 삭제하지 않음 |
| 검사 이력 | 부품 삭제와 무관하게 검사 이력은 보존 기간/디스크 여유공간 정책으로만 삭제 |

기준 이미지 파일 삭제는 다음 경우에만 허용한다.

- 단일품목 등록 화면에서 작업자가 기준 이미지 삭제 버튼을 누른 경우
- 현재6개저장 또는 검사 UI 기준 이미지 저장으로 같은 위치 이미지를 갱신하는 경우. 이때 기존 현재 이미지는 OldVer 백업으로 보존한다.
- 작업자가 폴더 관리로 직접 삭제하는 경우

그 외 부품 삭제, 다중품목 CSV DB 교체, DB 재저장 동작은 기준 이미지 실제 파일을 삭제하지 않는다.

## 측정부

| 필드 | 설명 | 필수 | 비고 |
| --- | --- | --- | --- |
| PartNo | 품번 | Y |  |
| MeasurementName | 측정부명 | Y | 예: 측정부, 측정부2 |
| ViewType | 기준 이미지 방향 | Y | 측정 위치가 속한 뷰 |
| Coordinates | 측정 좌표 | N | 좌표 포맷 확정 필요 |
| NominalValue | 기준값 | Y |  |
| ToleranceMin | 하한 허용오차 | N | 판정 기준 확인 필요 |
| ToleranceMax | 상한 허용오차 | N | 판정 기준 확인 필요 |
| Unit | 단위 | N | mm 후보 |

## 부품 다중 등록 CSV

다중품목 등록은 CSV 파일의 1행을 헤더로 사용하고, 2행부터 부품 기준정보로 처리한다.
CSV 내보내기는 현재 DB에 등록된 전체 부품 기준정보를 한 파일로 저장한다.
기준 이미지는 파일 경로와 촬영 위치를 별도 UI에서 관리하므로 다중 등록 CSV에는 포함하지 않는다.

| 컬럼 | 설명 | 필수 | 비고 |
| --- | --- | --- | --- |
| 품번 | PartNo | Y | 기존 품번이면 수정 저장 |
| 품명 | PartName | Y |  |
| 분류코드 | CategoryCode | N |  |
| 분류설명 | CategoryDescription | N |  |
| 구분 | PartType | N |  |
| 측정부_길이 | 단일 측정부 길이 기준값 | N | 빈 값 또는 `-`는 미사용 |
| 측정부_길이_허용 | 단일 측정부 길이 허용값 | N | 저장 시 `±허용`으로 사용 |
| 측정부_길이_단위 | 단일 측정부 길이 단위 | N | 기본 mm |
| 측정부_너비 / 측정부_너비_허용 / 측정부_너비_단위 | 단일 측정부 너비 기준값/허용/단위 | N | 빈 값 또는 `-`는 미사용 |
| 측정부_높이 / 측정부_높이_허용 / 측정부_높이_단위 | 단일 측정부 높이 기준값/허용/단위 | N | 빈 값 또는 `-`는 미사용 |
| 측정부_두께 / 측정부_두께_허용 / 측정부_두께_단위 | 단일 측정부 두께 기준값/허용/단위 | N | 빈 값 또는 `-`는 미사용 |
| 측정부N_길이/너비/높이/두께 + `_허용`/`_단위` | 복수 측정부 세트 | N | 첫 번째는 `측정부`, 두 번째부터 `측정부2`, `측정부3` 순서로 확장 |

## 검사 이력

| 필드 | 설명 | 필수 | 비고 |
| --- | --- | --- | --- |
| InspectionId | 검사 ID | Y | 시스템 생성 |
| PartNo | 품번 | Y |  |
| InputCode | 입력 라벨/바코드 | N | 입력 방식 확정 필요 |
| Result | OK/NG/Error | Y | 오류와 NG 구분 |
| InspectedAt | 검사일시 | Y |  |
| ElapsedMs | 검사 소요시간 | N | 통계 사용 |
| CategoryCode | 분류코드 | Y | 검사 당시 기준정보 스냅샷 |
| CategoryDescription | 분류설명 | N | 검사 당시 기준정보 스냅샷 |
| PartType | 구분 | N | 검사 당시 기준정보 스냅샷 |
| NgResult | NG 결과 | N | NG 시 불일치 측정부 요약 |

## 검사 측정값

| 필드 | 설명 | 필수 | 비고 |
| --- | --- | --- | --- |
| InspectionId | 검사 ID | Y |  |
| MeasurementRegionId | 측정부 ID | Y |  |
| MeasuredValue | 측정값 | Y |  |
| Result | OK/NG | Y | 측정부별 판정 |

## 검사 이력 저장 위치와 삭제 정책

개발용 구현은 SQLite `Tests/AI-Vision IO Inspector/DB/DataBase.db`의 `History_*` 테이블을 사용한다.

| 항목 | 현재 구현 |
| --- | --- |
| 저장 위치 | `Tests/AI-Vision IO Inspector/DB/DataBase.db`의 `History_Inspections`, `History_Measurements`, `History_CapturedImages`, `History_Events` |
| 검사 로그 | 이벤트 로그는 `History_Events`에 저장 |
| 저장 단위 | 검사 1건당 `History_Inspections` 1행과 하위 측정/이미지/이벤트 행 |
| 화면 조회 | 앱 시작 시 SQLite 이력을 로드하고, 시간/품번/품명/분류코드/분류설명/구분/NG결과 키워드로 현재 화면 목록을 필터링 |
| CSV 저장 | 이력 화면의 `CSV 저장` 버튼으로 검색 조건에 따라 현재 표시된 이력만 내보냄 |
| CSV 측정 컬럼 | 현재 표시된 이력의 측정부를 모아 첫 번째 세트는 `측정부_길이_측정값`, 두 번째부터 `측정부2_길이_측정값` 형태의 동적 컬럼으로 저장 |
| 기간 삭제 | 기본 365일 초과 날짜 폴더 삭제 |
| 저장공간 삭제 | 실행 드라이브 여유 공간이 기본 2GB 미만이면 오래된 날짜 폴더부터 삭제 |

중앙 서버 DB 요구가 생기면 동일한 `IInspectionRepository` 경계를 유지한 채 PostgreSQL 등 서버형 DB 저장소로 교체한다.
