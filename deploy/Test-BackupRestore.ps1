param(
    [string]$BackupPath = "C:\inetpub\backups\WT.Blazor",
    [string]$AppPath = "C:\inetpub\wwwroot\WT.Blazor",
    [string]$OperationsPath = (Join-Path $PSScriptRoot '../operations/WT.Blazor.Operations.dll'),
    [string]$WorkPath = (Join-Path $env:TEMP 'WT.Blazor-recovery-drills'),
    [string]$ReportPath
)
$ErrorActionPreference = 'Stop'
$backup = Get-ChildItem -LiteralPath $BackupPath -Filter 'wt-blazor-*.db' |
    Sort-Object LastWriteTime -Descending | Select-Object -First 1
if (-not $backup) { throw "No database backup found in $BackupPath." }
if (-not (Test-Path -LiteralPath $OperationsPath)) { throw "Operations tool not found: $OperationsPath. Use the operations/ directory from the release package." }
$keys = Join-Path $BackupPath "$($backup.BaseName).keys"
$verificationJson = dotnet $OperationsPath verify-backup $backup.FullName $keys $WorkPath
if ($LASTEXITCODE -ne 0) { throw 'Backup recovery verification failed. Production was not modified.' }
$verification = ($verificationJson -join [Environment]::NewLine) | ConvertFrom-Json
if (-not $verification.KeyContinuityVerified -or -not $verification.ApplicationDataReadable) { throw 'Recovery report did not confirm readable application data and key continuity.' }
if ($ReportPath) {
    $reportDirectory = Split-Path -Parent ([IO.Path]::GetFullPath($ReportPath))
    New-Item -ItemType Directory -Path $reportDirectory -Force | Out-Null
    $verification | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $ReportPath -Encoding utf8
}
Write-Host 'PASS: database integrity, foreign keys, application-readable estimates/history/settings, migration history, file hashes, and Data Protection key continuity verified in an isolated copy.'
Write-Host 'Finish acceptance on isolated IIS staging with dedicated Windows identities; this offline drill does not validate AD group membership or IIS authentication.'
