# Work Log 자동 추가 스크립트

간단한 PowerShell 스크립트로 work-log 마크다운 파일에 오늘 날짜 헤더 아래에 항목을 추가합니다.

사용 예시:

- 명령줄에서 즉시 추가:

```powershell
powershell -ExecutionPolicy Bypass -File scripts\append_work_log.ps1 -FilePath "Docs/03-development/10-before-development/work-log.md" -Message "오늘 작업 내용 요약"
```

- 스크립트를 호출하면 같은 날짜(예: `## 2026-05-29`) 헤더가 존재하면 그 아래에 항목을 추가하고, 없으면 새 헤더를 만든 뒤 추가합니다.

VS Code에서 빠르게 사용하려면 이 저장소의 `.vscode/tasks.json`에 정의된 `Append Work Log` 작업을 사용하세요.
