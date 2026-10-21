param(
    [string]$BackupPath = "C:\inetpub\backups\WT.Blazor",
    [string]$AppPath = "C:\inetpub\wwwroot\WT.Blazor",
    [string]$OperationsPath = (Join-Path $PSScriptRoot '../operations/WT.Blazor.Operations.dll'),
    [string]$WorkPath = (Join-Path $env:TEMP 'WT.Blazor-recovery-drills')
)
$ErrorActionPreference = 'Stop'
$backup = Get-ChildItem -LiteralPath $BackupPath -Filter 'wt-blazor-*.db' |
    Sort-Object LastWriteTime -Descending | Select-Object -First 1
if (-not $backup) { throw "No database backup found in $BackupPath." }
if (-not (Test-Path -LiteralPath $OperationsPath)) { throw "Operations tool not found: $OperationsPath. Use the operations/ directory from the release package." }
$keys = Join-Path $BackupPath "$($backup.BaseName).keys"
dotnet $OperationsPath verify-backup $backup.FullName $keys $WorkPath
if ($LASTEXITCODE -ne 0) { throw 'Backup recovery verification failed. Production was not modified.' }
Write-Host 'PASS: database integrity, foreign keys, records, settings, migration history, file hashes, and Data Protection key continuity verified in an isolated copy.'
Write-Host 'Finish acceptance on isolated IIS staging with dedicated Windows identities; this offline drill does not validate AD group membership or IIS authentication.'
