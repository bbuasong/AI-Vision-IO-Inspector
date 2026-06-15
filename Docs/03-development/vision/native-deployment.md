# Native DLL 호환성 및 배포 기준

## 2026-06-09 검증 결과

`dotnet build` 기준으로 `.NET 9.0`, C# WPF/MVVM 솔루션에서 VLAD/VLC/OpenCV/MVSDK 관리 DLL 참조는 컴파일 가능합니다. 최종 빌드 결과는 경고 0개, 오류 0개입니다.

| DLL | 구분 | 확인 결과 |
| --- | --- | --- |
| `MVSDK_Net.dll` | 관리 DLL, AMD64 | 참조 및 컴파일 가능. `IMVApi`는 internal이므로 직접 호출하지 않고 기존 샘플처럼 `MyCamera` 공개 API를 사용해야 합니다. |
| `OpenCvSharp.dll` | 관리 DLL, MSIL | 참조 및 컴파일 가능. 단, 기존 VLAD 배포본은 .NET Framework 계열 종속성이 있어 RTSP 런타임 주 경로로 쓰지 않습니다. |
| `OpenCvSharp.Blob.dll` | 관리 DLL, MSIL | 참조 및 컴파일 가능. |
| `OpenCvSharp.Extensions.dll` | 관리 DLL, MSIL | 참조 및 컴파일 가능. |
| `OpenCvSharp.UserInterface.dll` | 관리 DLL, MSIL | 참조 및 컴파일 가능. |
| `VLAD_SDK.dll` | native x64 | P/Invoke 대상입니다. 같은 실행 폴더 또는 DLL 검색 경로에 종속 DLL이 있어야 합니다. |
| `opencv_world453.dll` | native x64 | VLAD/OpenCV 계열 종속 DLL입니다. |
| `libvlc.dll`, `libvlccore.dll`, `plugins` | native x64 | RTSP 미리보기/캡처에 필요합니다. `plugins` 폴더 전체가 필요합니다. |

## 중요한 제한

- `MVSDKmd.dll`은 현재 프로젝트의 `Native` 폴더에서 확인되지 않았습니다.
- `MVSDK_Net.dll`은 빌드 참조가 가능하지만 실제 IMV Direct SDK 호출 시 내부적으로 `MVSDKmd.dll`과 제조사 종속 DLL을 찾습니다.
- 따라서 카메라 Direct SDK 제어를 실제로 켜기 전에는 제조사 SDK 런타임 세트를 `Native\IMV\x64` 또는 `Native\VLAD`에 추가해야 합니다.
- 네이티브 DLL이 x64 기준이므로 `AI.Vision.IOInspector.App`과 `AI.Vision.IOInspector.Vision`은 `PlatformTarget=x64`로 고정합니다.

## 권장 배포 구조

```text
AI.Vision.IOInspector\
  AI.Vision.IOInspector.App.exe
  DB\
    DataBase.db
  Native\
    VLAD\
      VLAD_SDK.dll
      VLAD_Ctrl.dll
      MVSDK_Net.dll
      OpenCvSharp*.dll
      opencv_world453.dll
      libvlc.dll
      libvlccore.dll
      plugins\...
      CFG\
        Config.json
    IMV\
      x64\
        MVSDKmd.dll
        제조사 IMV 종속 DLL...
  RuntimeData\
```

## GitHub 관리 기준

- 기존 `Docs\00-inbox\documents\VLAD Source` 원본 소스는 GitHub에 올리지 않습니다.
- 다른 개발자가 빌드와 RTSP/VLAD 연동 검증을 할 수 있도록 필요한 런타임 DLL은 `Native\VLAD`에 선별 포함합니다.
- 대용량 DLL(`tensorflow.dll` 등)과 VLC `plugins` 전체는 저장소 용량과 GitHub 제한을 고려해 별도 배포 패키지 또는 Git LFS 적용 여부를 결정해야 합니다.
