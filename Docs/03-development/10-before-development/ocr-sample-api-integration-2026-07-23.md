# Epson OCR Sample API Integration

Date: 2026-07-23

## Purpose

Use the full `EpsonScanApi.exe` sample pipeline instead of the WPF application's former single OCR path. The sample runs scanner acquisition, label orientation and crop processing, Epson OCR, RapidOCR assistance, and part-number reconciliation.

## Runtime Flow

1. The user selects OCR from inspection search or part registration.
2. `EpsonEsC320wOcrService` starts `Native\EpsonOCR\EpsonScanApi.exe` without a console window and checks `/jobs` without starting the RapidOCR model.
3. The API returns only the configured `EPSON ES-C320W` scanner from `/scanners`.
4. WPF calls `/scan-to-pdf?ocrOnly=true` with the selected ADF, PNG, DPI, and color-mode options.
5. The sample API performs Epson OCR and RapidOCR-assisted part-number recognition.
6. WPF maps `part_no`, OCR text, and quality data to the existing OCR UI model, then copies the scan image to `OUTPUT_PATH` or `OCR_PATH`.
7. Temporary API image, crop, OCR source, and server job data are deleted after copying.

## No-paper handling

- An empty ADF returns HTTP `409` with the API message `ADF empty`; the WPF client reads this response through `HttpClient` and converts it to a failed result without throwing an application exception.
- The WPF service converts this response to: `스캐너 ADF에 용지가 없습니다. 용지를 올린 뒤 OCR 스캔을 다시 시도하세요.`
- The failed attempt is recorded in the transient OCR history. The API process remains running, so the operator can load paper and scan again without restarting the application.
- Verified on 2026-07-23 with the deployed `net472\Native\EpsonOCR\EpsonScanApi.exe`: HTTP `409`, no-paper message returned, `/jobs` remained responsive.

## Deployment

- `Native\EpsonOCR\EpsonScanApi.exe`: x86 OCR API host.
- `Native\EpsonOCR\rapid_sidecar.exe`: RapidOCR helper used by the sample, approximately 3.1 GB.
- `Native\EpsonOCR\appsettings.json`: API temporary scan folder is the relative `scans` folder.
- The App project's MSBuild copy target places the full folder in `net472\Native\EpsonOCR`.

Do not remove `Native\EpsonOCR` when deploying only the `net472` folder. A normal GitHub repository cannot accept the 3.1 GB helper executable; keep source code and deployment artifacts separate, or use an approved large-file distribution policy.

## Verification Required

- Compare at least 20 physical ES-C320W scans between the sample and the integrated application.
- Confirm ports `8000` and `8011` are available on each deployment PC.
- Confirm repeated OCR and application shutdown clean up only the RapidOCR sidecar started by this application.

## 2026-07-23 Temporary File Retention Update

### Storage Rules

- `Native\\EpsonOCR\\scans` is an API-only temporary working folder. It may contain a raw scan, card crop, orientation-corrected image, part-number crop, and an optional PDF only while a job is active.
- Inspection OCR copies the raw image and JSON to `OUTPUT_PATH\\Inspection_Data\\YYYY\\MM\\DD\\HH\\OCR_Scan`. This is inspection history and follows the existing inspection retention policy.
- Registration OCR copies the raw image and JSON to `OCR_PATH\\YYYY\\MM\\DD\\HH\\Registration`. These are temporary registration files and are deleted after successful DB save, New Part, or application shutdown.

### Cleanup Flow

1. WPF resolves API relative paths such as `scans\\{jobId}_raw.png` against `Native\\EpsonOCR`, copies the raw scan to its final history or registration location, then deletes the API raw/card/orientation/part-crop files.
2. `DELETE /jobs/{jobId}` now removes every temporary file with the job ID prefix and then removes the job record from `jobs.json`.
3. On API startup, WPF calls `DELETE /jobs?olderThanMinutes=10` once. The API removes jobs older than ten minutes and orphan temporary files in `Native\\EpsonOCR\\scans`. 작업이 하나도 남지 않으면 `jobs.json`도 삭제합니다.
4. A file that is still in use is skipped and retried on the next cleanup cycle. Final inspection history and the DB are not affected.

### Verification

- Before the fix, the deployed `Native\\EpsonOCR\\scans` folder had 30 files using about 235 MB, which confirmed that the former implementation did not fully clean temporary OCR artifacts.
- The updated x86 API was tested in an isolated deployment directory. A file older than 10 minutes was deleted by `DELETE /jobs?olderThanMinutes=10`, while a recent file was retained. Result: `deleted_files=1`, old file absent, recent file present.
- `EpsonScanApi` Release x86 and the integrated WPF App were compiled successfully with zero errors. The currently running application still uses the old `Native\\EpsonOCR\\EpsonScanApi.dll`; stop the application and API before deploying the new Native folder through the normal build output.

## 2026-07-23 Latest OCR Result Display Update

- The Options OCR panel binds its scan path to `OcrLatestImagePath` and its OCR source text to `OcrLatestRawText`.
- `EpsonScanApiClient` accepts `ocr.text`, `ocr.engine_raw_text`, or `ocr.raw_text` and uses the first non-empty value. This supports both the current sample response and compatible older API responses.
- If the API provides only `ocr_src_path`, it is used as the scan path fallback. If an image copy or temporary cleanup step fails after OCR completed, the available API scan path and OCR source text are retained in the failed result rather than being replaced with empty strings.
- The latest result is intentionally cleared for registration OCR after successful DB save, New Part, or application exit. This is required because the registration image and JSON are deleted at those points. Inspection OCR remains visible until the application is closed.

## 2026-07-23 OCR Setting Persistence Update

- The OCR panel does not include a separate Save Settings button.
- Resolution and color mode selections are already used by the next scan immediately, so retaining a manual save action would imply a false apply step.
- When either user-selectable option changes, the WPF application writes the new values to the executable-local CFG\\OcrScannerSettings.json.
- The configuration-load guard prevents the initial restore at application startup from rewriting the file.
- The fixed ADF, PNG, and kor+eng settings remain code-controlled and are not exposed as user settings.

## 2026-07-23 OCR Raw Text Display Verification

- The live API job 82e00401237b returned a non-empty ocr.text value with 114 characters. The value was also parsed successfully by the .NET Framework JavaScriptSerializer-compatible dictionary structure.
- The read-only scan-path and OCR-raw-text TextBox bindings are explicitly OneWay. The display controls cannot write an empty initial Text value back to the ViewModel.
- OcrLatestRawText uses the API result first. If it is absent after a successful scan, the application reads the saved response JSON and restores ocr.text; if neither source has text, the UI shows an explicit API-response-missing message instead of a blank area.
- Verification build: .NET Framework 4.7.2 / x64 Debug completed with zero warnings and zero errors in an isolated output directory.

## 2026-07-23 Sample OCR Comparison

### Confirmed Common Components

- The integrated application does not use a separate OCR implementation. It starts `Native\\EpsonOCR\\EpsonScanApi.exe` and requests `POST /scan-to-pdf?ocrOnly=true`.
- The deployed API executable and the sample `EpsonScanApi-win-x86\\EpsonScanApi.exe` have the same SHA-256 value: `58E0F86822252040BE73E793EFC67F9FA08533B82E4B9350A8EA198D87283F83`.
- The WPF request explicitly sends the configured DPI, gray/color mode, `source=feeder`, `fmt=png`, card DPI, and `engine=auto`. `ocrOnly=true` only skips searchable-PDF creation; it does not skip Epson OCR, RapidOCR assistance, or the card orientation/crop pipeline.

### Relevant Differences

- The executable-local `CFG\\OcrScannerSettings.json` currently selects `600 DPI` and `gray` mode. The sample API configuration has `DefaultDpi=300`; the API default is used only when the caller does not send a DPI value.
- Since the integrated application always sends its selected DPI, the active WPF setting controls actual scan resolution. A 600 DPI scan is not automatically more accurate: it produces a different card crop and OCR source image than a 300 DPI scan, and must be compared using the same label and color mode.
- The current API configuration had a narrower part-number extraction rule than the sample. It was aligned with the sample to accept Unicode hyphen variants and full-width/typographic brackets. This improves extraction after OCR, but does not alter the raw OCR engine result.

### Required Accuracy Verification

1. Use one representative label and scan it with the sample and the integrated application under the same `300 DPI / gray / ADF / PNG` condition.
2. Compare the raw scan image, card image, and OCR source image for orientation, crop area, dimensions, and brightness before comparing part-number text.
3. If those three images are equivalent, the WPF application cannot be the source of an OCR-engine accuracy difference because it calls the same x86 API executable and engine selection. In that case, compare the complete `Native\\EpsonOCR` helper runtime with the sample, especially `rapid_sidecar.exe`, its model files, and the API process startup log.
4. If the OCR source images differ, resolve the resolution/color/crop condition first. The first controlled baseline is `300 DPI / gray`, then test `400 DPI`, then `600 DPI`; retain the setting that gives the best recognition for actual production labels.

### Build Verification

- The updated project built successfully as .NET Framework 4.7.2 / x64 Debug with zero warnings and zero errors.
- Isolated verification output: `C:\\Temp\\AI-Vision-IO-Inspector-OcrCompatibilityBuild`.
- The currently running application must be stopped and rebuilt normally before `net472\\Native\\EpsonOCR\\appsettings.json` receives the updated extraction rule.

## 2026-07-23 jobs.json Immediate Cleanup Update

### Final OCR Record and API Job Separation

- `jobs.json` is only the x86 EpsonScanApi in-progress job registry. It is not a final OCR record and must not be retained between scans.
- After a successful scan, WPF copies the image and API response to the selected final folder as `scan_yyyyMMdd_HHmmss_fff.png` and `scan_yyyyMMdd_HHmmss_fff.ocr.json`.
- Registration OCR uses `OCR_PATH\\YYYY\\MM\\DD\\HH\\Registration`; inspection OCR uses `OUTPUT_PATH\\Inspection_Data\\YYYY\\MM\\DD\\HH\\OCR_Scan`.
- Registration cleanup deletes the final PNG and matching `.ocr.json` together when the user saves the part, starts a new part, or exits the application. Inspection retention deletes both files when its time-based history folder is deleted.

### Immediate Cleanup Flow

1. EpsonScanApi includes its job `id` in both successful and failed `scan-to-pdf` responses.
2. WPF calls `DELETE /jobs/{id}` for success, ADF-empty, OCR-error, image-copy-error, and other scan failures.
3. The API removes the job's raw/card/orientation/crop files and removes the job from the in-memory registry.
4. When the registry becomes empty, the API deletes `Native\\EpsonOCR\\jobs.json` instead of writing an empty `[]` file.
5. Therefore a later scan starts with a new job only; an old `jobs.json` cannot be reused as a result source.

## 2026-07-23 Failed OCR Evidence Retention Update

### Retention Rule

- `jobs.json` remains an API-only transient file and is deleted after every completed or failed job cleanup.
- When OCR fails, the API raw image, card image, orientation image when present, and the error response JSON are copied to `net472\\Native\\EpsonOCR\\scans\\Failed` before the API job is deleted.
- The primary saved files use `scan_yyyyMMdd_HHmmss_fff.png` and `scan_yyyyMMdd_HHmmss_fff.ocr.json`. Additional card and OCR-source images use the same prefix with `_card` and `_ocr-source` suffixes.
- If the scanner fails before creating an image, only the `.ocr.json` error response is retained.
- A failure archive is not a permanent inspection history. The next OCR that completes with a valid part number, acceptable quality, and no rescan request deletes the entire `Failed` folder.

### Purpose

- The work registry must not remain because stale jobs can affect the next scan.
- The raw image and response JSON must remain long enough to diagnose OCR recognition, scan orientation, card crop, and scanner/API errors.
- Final inspection and registration storage rules are unchanged. This failure archive exists only below the executable-local Native folder.

### Verification

- EpsonScanApi was published as `net8.0-windows / win-x86` successfully. Existing warnings are unrelated to this change: `SixLabors.ImageSharp 3.1.7` advisory, one nullable warning, and one pre-existing async-without-await warning.
- AI-Vision IO Inspector built successfully as `.NET Framework 4.7.2 / x64 Debug`, with zero warnings and zero errors.
- The updated `EpsonScanApi.dll` and dependency manifest were copied to `AI.Vision.IOInspector.App\\bin\\x64\\Debug\\net472\\Native\\EpsonOCR` and the DLL SHA-256 values were verified equal.
- The earlier API process from the project-root `Native\\EpsonOCR` folder remains running and was not forcibly stopped. It must be closed before replacing that source runtime folder; otherwise a later normal build can copy the previous API DLL again.

## 2026-07-23 OCR Runtime Path and Result Verification

### Runtime Folder Rule

- `EpsonEsC320wOcrService.BuildWorkerPath()` must resolve `Native\\EpsonOCR\\EpsonScanApi.exe` from the executable folder (`AppContext.BaseDirectory`), not by searching upward for the development solution folder.
- The previous development-root lookup caused `EpsonScanApi.exe` to write raw scan files to `<project-root>\\Native\\EpsonOCR\\scans`, even when the WPF application itself was started from `net472`.
- The worker path now uses only `<net472>\\Native\\EpsonOCR\\EpsonScanApi.exe`. Its `ProcessStartInfo.WorkingDirectory` is the same native folder, so the relative `ScanOutputDir=scans` resolves to `<net472>\\Native\\EpsonOCR\\scans`.
- If port 8000 already belongs to an `EpsonScanApi.exe` from a different folder, the WPF client now stops before scanning and reports both the active and required EXE paths. It does not terminate a sample API automatically.

### Result and Part Number Rule

- The API working folder contains `jobs.json` and temporary image files. It is not the final inspection history location.
- For a successful job, the authoritative product number is `jobs.json -> ocr -> part_no`.
- `EpsonScanApiClient` maps exactly that value to `EpsonScanApiResult.PartNo`. If `part_no` is empty, `NeedsConfirmation=true`; Search DB must not select a product automatically.
- API error jobs have no `ocr.part_no`; WPF must show the error and must not create final OCR image/JSON history files.

### 600 DPI Failure Observed

- The 2026-07-23 15:34 test created a 47 MB raw PNG, then `engine=auto` failed with `Epson 엔진 실패: 이미지 로드 실패(kRecLoadImgFW)`.
- Auto mode attempted a Tesseract fallback, but the deployed runtime had no `eng/kor.traineddata`; the resulting job error was `사용 가능한 언어 데이터가 없습니다. tessdata에 eng/kor.traineddata를 넣으세요.`
- The sample default is 300 DPI. For the current x86 Epson OCR runtime, use `300 DPI / gray / ADF / PNG` as the first stable baseline. Test 400 DPI and 600 DPI only after the Epson engine can load the corresponding raw image reliably.
