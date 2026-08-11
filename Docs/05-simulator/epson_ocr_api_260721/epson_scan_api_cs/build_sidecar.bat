@echo off
REM Build rapid_sidecar.py into a single rapid_sidecar.exe (PyInstaller).
REM Run once on the dev PC from a Python that has rapidocr (e.g. venv32 activated).
REM Result: dist\rapid_sidecar.exe -> copy next to EpsonScanApi.exe.
REM Also copy rapid_sidecar.config.json next to that exe (sidecar reads it for tuning).
REM No Python needed on the customer PC; ship the exe + config json.

REM 1) install required packages
python -m pip install pyinstaller rapidocr-onnxruntime

REM 2) remove the obsolete 'pathlib' backport (blocks PyInstaller). Harmless if not installed.
python -m pip uninstall -y pathlib

REM 3) build
python -m PyInstaller --onefile --noconfirm --clean --name rapid_sidecar --collect-all rapidocr_onnxruntime --collect-all onnxruntime --collect-all cv2 rapid_sidecar.py

REM 4) stop here if PyInstaller failed (avoid a misleading "Done")
if errorlevel 1 (
  echo.
  echo [BUILD FAILED] Check the error above.
  echo   - if "pathlib ... incompatible" appears again: run  conda remove pathlib  then retry
  echo   - dist\rapid_sidecar.exe may be an OLD build, not your new one.
  pause
  exit /b 1
)

REM 5) copy the config file into dist (must sit next to the exe to be read)
if exist rapid_sidecar.config.json copy /y rapid_sidecar.config.json dist\ >nul

echo.
echo [OK] Done. Output: dist\rapid_sidecar.exe
echo   1) copy dist\rapid_sidecar.exe   next to EpsonScanApi.exe
echo   2) copy rapid_sidecar.config.json next to EpsonScanApi.exe (tuning values)
pause
