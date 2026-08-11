@echo off
REM Build rapid_sidecar.py into a single rapid_sidecar.exe (PyInstaller).
REM Run once on the dev PC from a Python that has rapidocr (e.g. venv32 activated).
REM Result: dist\rapid_sidecar.exe  ->  copy next to EpsonScanApi.exe (output/publish folder).
REM No Python is needed on the customer PC; just ship that one exe.

python -m pip install pyinstaller rapidocr-onnxruntime

python -m PyInstaller --onefile --noconfirm --clean --name rapid_sidecar --collect-all rapidocr_onnxruntime --collect-all onnxruntime rapid_sidecar.py

echo.
echo Done. Output: dist\rapid_sidecar.exe
echo Copy dist\rapid_sidecar.exe next to EpsonScanApi.exe.
echo If it errors on cv2/numpy at runtime, add  --collect-all cv2  to the PyInstaller line above.
pause
