# Native DLL 호환성 및 배포 기준

## 2026-05-30 검증 요약

목표는 개발 도구가 설치되지 않은 장비 PC에서 앱을 실행하는 것입니다. 현실적인 배포 단위는 `exe` 하나가 아니라 `exe가 들어 있는 실행 폴더 하나`입니다. VLAD/VLC/IMV 계열은 네이티브 DLL과 플러그인 폴더를 실제 파일로 필요로 하므로 단일 파일 EXE 배포는 권장하지 않습니다.

## 확인된 DLL 성격

| 항목 | 판정 | 배포 영향 |
| --- | --- | --- |
| `VLAD_SDK.dll` | x64 네이티브 DLL | 앱도 `win-x64` 기준으로 배포해야 합니다. |
| `jsoncpp.dll`, `libvlc.dll`, `libvlccore.dll` | x64 네이티브 DLL | `VLAD_SDK.dll`과 같은 폴더에 배치합니다. |
| `VLAD_SDK\plugins` | VLC 플러그인 DLL 묶음 | `VLC_PLUGIN_PATH`가 플러그인 폴더를 가리켜야 합니다. |
| `MVSDK_Net.dll` | .NET Framework 4.0 래퍼, x64/x86 별도 존재 | .NET 9 WPF에서 직접 참조는 위험합니다. 우선 어댑터 경계 뒤에서 격리합니다. |
| `CLIDelegate.dll` | .NET Framework 4.0 + 네이티브 의존 | `MVSDKmd.dll`, VC++ 2013 런타임이 필요합니다. |
| `VLAD_Ctrl.dll` | .NET Framework 4.7.2, x86 | 현재 .NET 9 x64 앱에 직접 참조하지 않습니다. |

## 확인된 주요 의존성

`dumpbin /dependents` 기준으로 `VLAD_SDK.dll`은 다음 DLL을 추가로 요구합니다.

- `onnxruntime.dll`
- `opencv_world453.dll`
- `tensorflow.dll`
- `DongleLicenseChecker.dll`
- `MSVCP140.dll`
- `VCRUNTIME140.dll`
- `VCRUNTIME140_1.dll`

`MVSDKmd.dll`은 다음 DLL을 추가로 요구합니다.

- `GCBase_MD_VC120_v3_0.dll`
- `GenApi_MD_VC120_v3_0.dll`
- `CLProtocol_MD_VC120_v3_0.dll`
- `MVlog4cppmd.dll`
- `ImageConvert.dll`
- `ImageSave.dll`
- `MSVCP120.dll`
- `MSVCR120.dll`

현재 `VLAD Source` 안에서 `GCBase_MD_VC120_v3_0.dll`, `GenApi_MD_VC120_v3_0.dll`, `CLProtocol_MD_VC120_v3_0.dll`, `MVlog4cppmd.dll`, `ImageConvert.dll`, `ImageSave.dll`, `MSVCP120.dll`, `MSVCR120.dll`, `VCRUNTIME140.dll`, `VCRUNTIME140_1.dll`은 확인되지 않았습니다. 실제 장비 배포 전 제조사 SDK 설치본 또는 재배포 패키지에서 확보해야 합니다.

## 권장 배포 구조

```text
AI.Vision.IOInspector\
  AI.Vision.IOInspector.App.exe
  *.dll
  DB\
    DataBase.db
  Native\
    VLAD\
      VLAD_SDK.dll
      jsoncpp.dll
      libvlc.dll
      libvlccore.dll
      plugins\...
    IMV\
      x64\
        MVSDK_Net.dll
        CLIDelegate.dll
        ThridLibray.dll
        MVSDKmd.dll
    AI\
      x64\
        onnxruntime.dll
        opencv_world453.dll
        tensorflow.dll
  RuntimeData\
```

앱 시작 시 `NativeDependencyLoader`가 `Native\VLAD`, `Native\VLAD\plugins`, `Native\IMV\x64`, `Native\AI\x64`를 프로세스 DLL 검색 경로에 등록합니다. 폴더가 없어도 시뮬레이션 모드는 실행됩니다.

## 배포 방식

권장 publish 방식은 다음과 같습니다.

```powershell
dotnet publish .\AI.Vision.IOInspector.App\AI.Vision.IOInspector.App.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=false -p:PublishReadyToRun=true
```

`--self-contained true`는 .NET 런타임 설치 없이 실행하기 위한 설정입니다. 하지만 VC++ 런타임, 카메라 제조사 드라이버, 동글 라이선스 런타임은 별도 네이티브 요구사항입니다. 개발 도구는 필요하지 않지만, 장비 SDK/드라이버가 필요한지는 실제 장비 PC에서 확인해야 합니다.

## 주의 사항

- `tensorflow.dll`은 380MB 이상으로 GitHub 일반 파일 제한을 넘을 수 있습니다. Git에는 넣지 말고 릴리스 산출물 또는 설치 패키지로 관리합니다.
- `MVSDK_Net.dll`은 .NET Framework 4.0 래퍼이므로 .NET 9 앱에 직접 참조하기 전에 호환 테스트가 필요합니다.
- x86 DLL과 x64 DLL을 같은 프로세스에서 섞을 수 없습니다. 현재 앱은 `win-x64` 기준으로 고정하는 것이 맞습니다.
- 단일 파일 EXE 옵션은 VLC 플러그인/카메라 SDK DLL 로딩 문제를 만들 가능성이 높으므로 현재 단계에서는 사용하지 않습니다.
