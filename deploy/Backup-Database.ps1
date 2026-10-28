#Requires -RunAsAdministrator
param(
    [string]$AppPoolName = "WT.BlazorPool",
    [string]$DatabasePath = "C:\inetpub\wwwroot\WT.Blazor\App_Data\wt-blazor.db",
    [string]$KeyRingPath = (Join-Path (Split-Path -Parent $DatabasePath) "keys"),
    [string]$BackupPath = "C:\inetpub\backups\WT.Blazor",
    [string]$OperationsPath = (Join-Path $PSScriptRoot '../operations/WT.Blazor.Operations.dll'),
    [int]$Keep = 30
)

Import-Module WebAdministration

if ($Keep -lt 1) {
    throw "Keep must be at least 1."
}
if (-not (Test-Path $DatabasePath)) {
    throw "Database not found: $DatabasePath"
}

function Wait-AppPoolState {
    param(
        [string]$ExpectedState,
        [int]$TimeoutSeconds = 30
    )

    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    do {
        if ((Get-WebAppPoolState -Name $AppPoolName).Value -eq $ExpectedState) {
            return
        }
        Start-Sleep -Milliseconds 250
    } while ((Get-Date) -lt $deadline)

    throw "App pool $AppPoolName did not reach $ExpectedState within $TimeoutSeconds seconds."
}

function Start-AppPool {
    $deadline = (Get-Date).AddSeconds(30)
    do {
        try {
            Start-WebAppPool -Name $AppPoolName -ErrorAction Stop
            Wait-AppPoolState -ExpectedState "Started"
            return
        }
        catch {
            if ((Get-Date) -ge $deadline) { throw }
            Start-Sleep -Milliseconds 500
        }
    } while ($true)
}

New-Item -ItemType Directory -Path $BackupPath -Force | Out-Null
$currentState = (Get-WebAppPoolState -Name $AppPoolName).Value
if ($currentState -ne "Stopped") {
    if ($currentState -ne "Stopping") {
        Stop-WebAppPool -Name $AppPoolName
    }
    Wait-AppPoolState -ExpectedState "Stopped"
}

try {
    $timestamp = Get-Date -Format "yyyyMMdd-HHmmss"
    $destination = Join-Path $BackupPath "wt-blazor-$timestamp.db"
    $keyDestination = Join-Path $BackupPath "wt-blazor-$timestamp.keys"
    if (Test-Path -LiteralPath $OperationsPath) {
        dotnet $OperationsPath snapshot-database $DatabasePath $destination
        if ($LASTEXITCODE -ne 0) { throw 'Creating a consistent SQLite backup failed.' }
    }
    else {
        # Legacy file copying cannot safely omit committed WAL data.
        if (Test-Path -LiteralPath "$DatabasePath-wal") { throw 'A SQLite WAL exists. Use the release operations tool to create a consistent backup.' }
        Copy-Item -Path $DatabasePath -Destination $destination -ErrorAction Stop
    }
    if (Test-Path $KeyRingPath) {
        if ((Test-Path -LiteralPath $OperationsPath) -and (Get-ChildItem -LiteralPath $KeyRingPath -Filter '*.xml')) {
            $probeJson = dotnet $OperationsPath backup-probe $KeyRingPath
            if ($LASTEXITCODE -ne 0) { throw 'Creating the backup-time Data Protection recovery probe failed.' }
            ($probeJson | ConvertFrom-Json).protectedValue | Set-Content -LiteralPath "$destination.probe" -Encoding ascii
        }
        else {
            Write-Warning 'No operations tool or usable key ring was found. This legacy backup has no recovery probe; use the release bundle for full key-continuity verification.'
        }
        Copy-Item -Path $KeyRingPath -Destination $keyDestination -Recurse -ErrorAction Stop
        Write-Host "Data Protection key ring backed up: $keyDestination"
    }
    else {
        New-Item -ItemType Directory -Path $keyDestination -Force | Out-Null
        Write-Host "No Data Protection keys existed at backup time; an empty key-ring archive was created."
    }
}
catch {
    if ($destination -and (Test-Path -LiteralPath "$destination.probe")) {
        Remove-Item -LiteralPath "$destination.probe" -Force -ErrorAction SilentlyContinue
    }
    if ($destination -and (Test-Path $destination)) {
        Remove-Item -LiteralPath $destination -Force -ErrorAction SilentlyContinue
    }
    if ($keyDestination -and (Test-Path $keyDestination)) {
        Remove-Item -LiteralPath $keyDestination -Recurse -Force -ErrorAction SilentlyContinue
    }
    throw
}
finally {
    Start-AppPool
}

Get-ChildItem -Path $BackupPath -Filter "wt-blazor-*.db" |
    Sort-Object LastWriteTime -Descending |
    Select-Object -Skip $Keep |
    ForEach-Object {
        $probeBackup = "$($_.FullName).probe"
        if (Test-Path -LiteralPath $probeBackup) { Remove-Item -LiteralPath $probeBackup -Force }
        Remove-Item -LiteralPath $_.FullName -Force
        $keyBackup = Join-Path $BackupPath "$($_.BaseName).keys"
        if (Test-Path $keyBackup) {
            Remove-Item -LiteralPath $keyBackup -Recurse -Force
        }
    }

Write-Output $destination
