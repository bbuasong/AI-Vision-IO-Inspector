# Native

기준일: 2026-08-11

이 폴더의 실제 내용물은 **Git으로 관리하지 않습니다.** clone 직후에는 비어 있으며, 별도로 배치해야 앱이 실행됩니다.

## 제외 이유

서드파티 실행 DLL 약 288MB(401개)입니다. 갱신될 때마다 저장소에 수백 MB가 누적되고, `opencv_world453.dll` 하나가 59MB입니다. 2026-08-11에 배포 패키지로 분리하기로 결정했습니다(`open-items.md` O-010, O-011).

## 배치해야 할 내용

```text
Native\
├── VLAD\        VLAD_SDK.dll, OpenCvSharp, MVSDK_Net, LibVLC plugins 등
└── EpsonOCR\    Epson ES-C320W OCR 런타임
```

## 확인 방법

배치 후 다음이 있으면 정상입니다.

- `Native\VLAD\VLAD_SDK.dll`
- `Native\VLAD\opencv_world453.dll`
- `Native\VLAD\OpenCvSharpExtern.dll`

`NativeDependencyLoader`가 앱 시작 시 이 경로를 `AssemblyResolve`, `PATH`, `SetDllDirectory`에 등록합니다. 파일이 없으면 시작 또는 검사 시작 단계에서 실패합니다.

## 함께 필요한 것

- `..\RuntimeData\` — AI 모델과 LibVLC/FFmpeg/OpenCvSharp 런타임
- CUDA 11.0 런타임(`cudart64_110.dll`), cuDNN 8(`cudnn64_8.dll`), `cublas64_11.dll`, VC++ Runtime — PC 설치 또는 별도 배치 필요(`open-items.md` O-002)
