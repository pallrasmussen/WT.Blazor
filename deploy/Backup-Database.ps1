#Requires -RunAsAdministrator
param(
    [string]$AppPoolName = "WT.BlazorPool",
    [string]$DatabasePath = "C:\inetpub\wwwroot\WT.Blazor\App_Data\wt-blazor.db",
    [string]$KeyRingPath = (Join-Path (Split-Path -Parent $DatabasePath) "keys"),
    [string]$BackupPath = "C:\inetpub\backups\WT.Blazor",
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
    Copy-Item -Path $DatabasePath -Destination $destination -ErrorAction Stop
    if (Test-Path $KeyRingPath) {
        $keyDestination = Join-Path $BackupPath "wt-blazor-$timestamp.keys"
        Copy-Item -Path $KeyRingPath -Destination $keyDestination -Recurse -ErrorAction Stop
        Write-Host "Data Protection key ring backed up: $keyDestination"
    }
    else {
        Write-Warning "Data Protection key ring not found at $KeyRingPath; database backup was created without it."
    }
}
finally {
    Start-AppPool
}

Get-ChildItem -Path $BackupPath -Filter "wt-blazor-*.db" |
    Sort-Object LastWriteTime -Descending |
    Select-Object -Skip $Keep |
    ForEach-Object {
        Remove-Item -LiteralPath $_.FullName -Force
        $keyBackup = Join-Path $BackupPath "$($_.BaseName).keys"
        if (Test-Path $keyBackup) {
            Remove-Item -LiteralPath $keyBackup -Recurse -Force
        }
    }

Write-Output $destination