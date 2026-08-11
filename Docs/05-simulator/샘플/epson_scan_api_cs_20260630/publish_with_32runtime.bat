@echo off
setlocal
REM ============================================================================
REM  Self-contained x86 deployment build (runtime included).
REM  Paths are RELATIVE to this .bat (%~dp0), so the project can move freely.
REM
REM   %~dp0            = this file's folder (...\epson_scan_api_cs\)
REM   PROJ            = the .csproj next to this bat
REM   OUT             = bin\Publish\EpsonScanApi-win-x86  (under bin, next to Debug/Release)
REM  Needs .NET 8 SDK. Run build_sidecar.bat (venv32) first for RapidOCR.
REM ============================================================================

set "PROJ=%~dp0EpsonScanApi.csproj"
set "SIDECAR=%~dp0dist\rapid_sidecar.exe"
set "OUT=%~dp0bin\Publish\EpsonScanApi-win-x86"

if not exist "%SIDECAR%" (
  echo [WARN] %SIDECAR% not found.
  echo        Run build_sidecar.bat in venv32 first, or the deploy will have no RapidOCR.
  echo.
)

echo Cleaning output folder: %OUT%
if exist "%OUT%" rmdir /s /q "%OUT%"

echo.
echo Publishing self-contained x86 ...
dotnet publish "%PROJ%" -c Release -r win-x86 --self-contained true -o "%OUT%"
if errorlevel 1 (
  echo.
  echo [ERROR] publish failed. See messages above.
  pause
  exit /b 1
)

echo.
echo ============================================================================
echo  Done.  Deployment folder:  %OUT%
echo   - The many DLLs there are the included .NET runtime (normal).
echo   - EpsonScanApi.exe + rapid_sidecar.exe are inside.
echo   - Zip the whole folder and give it to the customer (no .NET/Python needed).
echo ============================================================================
pause
endlocal
