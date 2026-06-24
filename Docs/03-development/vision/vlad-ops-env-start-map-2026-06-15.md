# VLAD_Ops_Ai_Env_Start 대응표

## 2026-06-15 기준 판단

원본 VLAD_Ops에서 `VLAD_Ops_Ai_Env_Start`는 FILE, MOVIE, MAP, CAM, MONITOR 화면/모드에서 반복 호출된다. 현재 AI-Vision IO Inspector는 제품 검사 앱이므로 실제 실행 경로는 CAM/HD 중심이지만, AI 담당자가 기존 VLAD_Ops 함수명을 따라갈 수 있도록 `VLAD_Ops_Ai.VLAD_Ops_Ai_Env_Start`를 공식 진입점으로 유지한다.

## 원본 호출 패턴

| 원본 모드 | 대표 파일 | user | root_name | site_name | msg/maj | model_path | 현재 대응 |
| --- | --- | --- | --- | --- | --- | --- | --- |
| FILE | `VLAD_Ops_File.cs`, `VLAD_Ops_Kind_File.cs` | `USER_CUS_STD` 또는 `USER_STD` | `FILE` | 설정 user 또는 null | V2/V2, V1/V2, V1/V1 | FILE 설정 model | Env Start 인자 구조 수용. 현재 제품 앱 UI에서는 FILE 모드 미사용 |
| MOVIE | `VLAD_Ops_Movie.cs`, `VLAD_Ops_Kind_Movie.cs` | `USER_CUS_STD` 또는 `USER_STD` | `MOVIE` | 설정 user 또는 null | V2/V2, V1/V2, V1/V1 | MOVIE 설정 model | Env Start 인자 구조 수용. 현재 제품 앱 UI에서는 MOVIE 모드 미사용 |
| MAP | `VLAD_Ops_Map.cs`, `VLAD_Ops_Kind_Map.cs` | `USER_CUS_STD` 또는 `USER_STD` | `MAP` | 설정 user 또는 null | V1/V1 | MAP 설정 model 또는 카메라별 model | Env Start 인자 구조 수용. 현재 제품 앱 UI에서는 MAP 모드 미사용 |
| CAM | `VLAD_Ops_Kind_Cam.cs`, `VLAD_Ops_imvCam.cs`, `VLAD_Ops_RTSP.cs` | `USER_CUS_STD` | `CAM` | 설정 user | V1/V1 | CAM 설정 model | 현재 검사 흐름의 주 대상. `VladSdkSession`에서 CAM/HD로 사용 |
| MONITOR | `VLAD_Ops_Kind_Monitor.cs` | `USER_STD` | `MONITOR` | 설정 user | V1/V1 | null | 현재 코드에서 modelPath가 null이고 rootName이 MONITOR이면 `VLAD_Registration`만 수행 |

## 현재 코드 대응

| 현재 파일 | 역할 |
| --- | --- |
| `LegacyVlad/VLAD_Ops_Ai.cs` | 원본 `VLAD_Ops_Ai_Env_Start` 분기를 최대한 유지하는 공식 구현 |
| `LegacyVlad/VladSdkSession.cs` | 원본 전역 `Vlad_id` 역할을 WPF 앱 전체 공유 세션으로 보관 |
| `Engines/VladVisionInferenceEngine.cs` | 검사 시작 시 공유 세션을 통해 CAM/HD VLAD 등록 후 추론 수행 |
| `LegacyVlad/VladInferenceResultParser.cs` | 원본 Draw 함수 분기(`USER_CUS_STD` + V1은 `VLAD_Custom_InferenceData_V1`)를 따른다 |

## 오늘 반영한 안정화

- 원본 VLAD_Ops에는 `BuildInferenceReadinessFailureMessage`에 해당하는 C# 선차단 함수가 없다.
- 현재 코드도 원본 흐름에 맞춰 모델 경로/구조 문제를 C#에서 먼저 throw하지 않고, `VLAD_Custom_Registration` 또는 `VLAD_Ops_Inference_Registration` 결과를 우선한다.
- checkpoint-only 모델 폴더는 VLAD_SDK가 바로 추론 모델로 로드할 수 없는 구조이지만, 현재 앱은 이를 Debug 진단으로만 남긴다.
- 원본 VLAD_Ops처럼 `detectData` 메모리를 C#에서 직접 파싱하지 않고 SDK Draw 함수 결과를 기본으로 사용한다.
- raw detectData 직접 파싱은 `AI_VISION_VLAD_PARSE_RAW_DETECT_DATA=1`일 때만 켠다.
- 2026-06-19에 미사용 `VLAD_Ops_Ai_Compat`, `VladFunctionAdapter`, `VladRuntimeContext` 계층은 제거했고, 공식 진입점은 `VLAD_Ops_Ai`로 단일화했다.

## 남은 판단

FILE/MOVIE/MAP 모드는 원본 테스트 도구의 화면 모드이며 현재 고객용 검사 앱의 직접 요구사항은 아니다. 다만 AI 담당자가 해당 모드 코드를 이식해야 한다면, 현재 공식 Env Start 함수가 인자 구조를 수용하므로 UI/서비스 호출 경로만 추가하면 된다.
