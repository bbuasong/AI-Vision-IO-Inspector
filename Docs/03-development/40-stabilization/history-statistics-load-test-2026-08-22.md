# 이력·통계 부하 시험 (5분에 1번 검사, 1년치)

측정일: 2026-08-22
측정 대상: AI-Vision IO Inspector — 이력 조회, 통계 조회
측정 방법: 실제 DB 스키마에 자료를 채우고 실제 조회 코드(`SqliteInspectionRepository.GetAll`, `StatisticsService.BuildSummary`)를 그대로 호출

## 왜 쟀는가

현장에서 5분에 한 번씩 검사하면 하루 288건, 한 해 105,120건이 쌓입니다.
보존기간을 1년 이상 둘 계획이므로, 그 시점에 이력·통계 화면이 견디는지 미리 확인했습니다.

## 시험 조건

| 항목 | 값 |
| --- | --- |
| 검사 주기 | 5분에 1회 (하루 288건) |
| 검사 1건에 딸리는 행 | 측정부 4 · 이미지 6 · 이벤트 5 = **15행** |
| 부품 수 | 50 |
| 결과 분포 | 합격 다수, 20건마다 불합격, 137건마다 오류 |
| 측정 규모 | 1개월 → 3개월 → 6개월 → 1년 (누적) |

## 결과

| 기간 | 검사 건수 | 딸린 행 | 이력 열기 | 통계(하루치) | 통계(전체) | 메모리 | DB 파일 |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| 1개월 | 8,640 | 129,600 | 0.56초 | 0.56초 | 0.54초 | 35.0MB | 14.3MB |
| 3개월 | 25,920 | 388,800 | 2.3초 | 1.6초 | 1.7초 | 104.7MB | 42.8MB |
| 6개월 | 51,840 | 777,600 | 4.0초 | 3.3초 | 3.4초 | 209.3MB | 86.4MB |
| **1년** | **105,120** | **1,576,800** | **6.8초** | **6.8초** | **7.0초** | **424.5MB** | **176.4MB** |

건수에 거의 정비례해 늘어납니다. 어디서 갑자기 무너지지는 않지만, 줄어들지도 않습니다.

> 이 수치에는 화면에 표를 그리는 시간이 빠져 있습니다.
> 실제 사용자가 느끼는 시간은 이보다 더 깁니다.

## 멈추지는 않습니다

1년치에서도 응답은 옵니다. 다만

- 이력 탭을 열 때마다 **약 7초**를 기다립니다
- 메모리를 **425MB** 더 씁니다 — RTSP 6채널과 VLAD가 이미 쓰고 있는 위에 얹힙니다

## 가장 부조리한 지점

**하루치 통계를 보는 데도 7초가 걸립니다.**

289건을 보려고 105,120건을 전부 메모리로 읽은 뒤 걸러냅니다.
기간 조건이 SQL이 아니라 C# 쪽에서 돌기 때문입니다.

```
StatisticsService.BuildSummary(start, end)
  └ _inspectionRepository.GetAll()      ← 전체를 읽고
      └ 그 다음 C#에서 기간을 거름
```

이력도 같은 구조입니다.

```
MainWindowViewModel.RefreshHistory()
  └ _inspectionRepository.GetAll()      ← 이력 + 측정부 + 이미지 + 이벤트
      └ ApplyHistoryFilters()            ← 메모리에서 걸러냄
```

1년치면 `GetAll()` 한 번에 **168만 행**이 객체로 올라옵니다.

## 실제 한계선

| 규모 | 판정 |
| --- | --- |
| 3개월 (2.6만 건)까지 | 쓸 만합니다 |
| 6개월 (5만 건)부터 | 눈에 띄게 답답합니다 |
| 1년 (10만 건) | 쓸 수는 있으나 매번 7초를 기다려야 합니다 |

보존기간을 1년 이상 둘 계획이므로, 지금 구조로는 후반부가 부담스럽습니다.

## 고치면 얼마나 나아지나

효과 순입니다.

1. **통계를 SQL 집계로** (`COUNT`, `GROUP BY`)
   전체를 읽을 이유가 없습니다. 7초 → 수십 ms 수준으로 떨어집니다.

2. **이력에 기간·건수 조건을 SQL로**
   화면에 필요한 만큼만 읽습니다. Start/End 단추로 하루치를 지정하면 289건만 읽으면 됩니다.

표 그리기는 이미 가상화(`EnableRowVirtualization`)가 켜져 있어 문제되지 않습니다.

## 재현 방법

시험 프로그램은 스크래치패드에 있습니다.

```
scratchpad/loadtest/Program.cs
```

앱의 `SqliteDatabase`로 스키마를 만들고, 실제 리포지토리와 통계 서비스를 그대로 호출합니다.
DB는 시험 폴더 아래 `DB/DataBase.db`에 새로 만들므로 현장 자료에 손대지 않습니다.

```bash
csc -platform:x64 -out:loadtest.exe -r:AI.Vision.IOInspector.Domain.dll -r:AI.Vision.IOInspector.Application.dll -r:AI.Vision.IOInspector.Infrastructure.dll -r:Microsoft.Data.Sqlite.dll Program.cs
```

행 수를 바꾸려면 파일 위쪽 상수를 고칩니다.

```csharp
const int MeasurementsPerInspection = 4;
const int ImagesPerInspection = 6;
const int EventsPerInspection = 5;
const int InspectionsPerDay = 288;   // 5분에 한 번
```

## 남은 결정

지금 손댈지, 현장 안정화 뒤로 미룰지 정해야 합니다.
