# RuntimeData

기준일: 2026-08-11

이 폴더의 실제 내용물은 **Git으로 관리하지 않습니다.** clone 직후에는 비어 있으며, 별도로 배치해야 앱이 실행됩니다.

## 제외 이유

AI 모델과 네이티브 런타임입니다. 모델 파일은 갱신 시 통째로 새 blob이 되어 저장소가 급격히 커집니다. 2026-08-11에 `Native\`와 함께 배포 패키지로 분리하기로 결정했습니다(`open-items.md` O-010, O-011).

## 배치해야 할 내용

```text
RuntimeData\
├── Models\
│   └── VLAD\
│       └── Ex_Weight\        VLAD 추론 모델
└── Native\
    ├── FFmpeg\
    ├── LibVLC\win-x64\       RTSP 라이브 화면 렌더링
    └── OpenCvSharp\x64\
```

## 모델 경로 규칙

`CFG\Config.json`의 `MODEL` 값이 이 폴더를 가리킵니다.

```json
"MODEL": "RuntimeData/Models/VLAD/Ex_Weight"
```

경로 해석 순서는 다음과 같습니다.

1. 환경변수 `AI_VISION_VLAD_MODEL_PATH`가 있으면 `Config.json`의 `MODEL`보다 우선합니다.
2. 상대경로이면 `AppContext.BaseDirectory`(EXE 폴더) 기준 절대경로로 변환합니다.
3. `VLAD_Ops_Ai_Env_Start()`가 trailing slash를 붙여 `VLAD_Custom_Registration`에 전달합니다.

**소스 폴더가 아니라 실행 파일 폴더 기준**이라는 점에 주의합니다. 배포 폴더가 `C:\LinkGenesis\AI-Vision IO Inspector\Run_2608010b`이면 그 아래 `RuntimeData\Models\VLAD\Ex_Weight\`가 전달됩니다.

## 미확정 사항

`Ex_Weight`에 checkpoint 계열 파일은 있으나, VLAD_SDK가 직접 읽는 최종 추론 export 구조인지는 아직 확인되지 않았습니다(`open-items.md` O-001).
