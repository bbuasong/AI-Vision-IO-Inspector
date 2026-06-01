param(
    [string]$FilePath = "Docs/03-development/work-log.md",
    [string]$Message
)

if (-not (Test-Path $FilePath)) {
    New-Item -Path $FilePath -ItemType File -Force | Out-Null
}

if (-not $Message) {
    $Message = Read-Host "추가할 내용 입력"
}

$date = (Get-Date).ToString('yyyy-MM-dd')
$time = (Get-Date).ToString('HH:mm')
$entry = "- [$time] $Message"

$content = Get-Content -Raw -Path $FilePath -ErrorAction SilentlyContinue

if ($content -match "(?m)^##\s+$date\s*$") {
    # 헤더 바로 아래에 한 줄을 삽입함 (첫 번째 매치에만 적용)
    $pattern = "(?m)^(##\s+" + [regex]::Escape($date) + "\s*$)"
    $replacement = "`$1`r`n`r`n$entry"
    $new = [regex]::Replace($content, $pattern, $replacement, 1)
} else {
    if ([string]::IsNullOrWhiteSpace($content)) {
        $new = "## $date`r`n`r`n$entry`r`n"
    } else {
        $new = $content.TrimEnd() + "`r`n`r`n## $date`r`n`r`n$entry`r`n"
    }
}

Set-Content -Path $FilePath -Value $new -Force -Encoding UTF8

Write-Output "Appended to $FilePath"
