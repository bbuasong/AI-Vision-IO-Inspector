param(
    [string]$Configuration = "Release",
    [string]$Runtime = "win-x64",
    [string]$OutputPath = ""
)

$ErrorActionPreference = "Stop"

$ScriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$RepositoryRoot = Split-Path -Parent $ScriptRoot
$SolutionRoot = Join-Path $RepositoryRoot "Tests\AI-Vision IO Inspector"
$AppProjectPath = Join-Path $SolutionRoot "AI.Vision.IOInspector.App\AI.Vision.IOInspector.App.csproj"

if ([string]::IsNullOrWhiteSpace($OutputPath))
{
    $OutputPath = Join-Path $SolutionRoot ("publish\" + $Runtime)
}

dotnet publish $AppProjectPath `
    --configuration $Configuration `
    --runtime $Runtime `
    --self-contained true `
    -p:PublishSingleFile=false `
    -p:PublishReadyToRun=true `
    --output $OutputPath

$DataFolderPath = Join-Path $SolutionRoot "DB"
$NativeFolderPath = Join-Path $SolutionRoot "Native"

if (Test-Path -LiteralPath $DataFolderPath)
{
    Copy-Item -LiteralPath $DataFolderPath -Destination $OutputPath -Recurse -Force
}

if (Test-Path -LiteralPath $NativeFolderPath)
{
    Copy-Item -LiteralPath $NativeFolderPath -Destination $OutputPath -Recurse -Force
}

Write-Host ("Published to: " + $OutputPath)
