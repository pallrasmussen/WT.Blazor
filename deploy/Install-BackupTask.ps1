#Requires -RunAsAdministrator
param(
    [string]$TaskName = "WT.Blazor Database Backup",
    [string]$Time = "02:00"
)

$backupScript = Join-Path $PSScriptRoot "Backup-Database.ps1"
if (-not (Test-Path $backupScript)) {
    throw "Backup script not found: $backupScript"
}

try {
    $scheduledTime = [DateTime]::ParseExact($Time, "HH:mm", $null)
}
catch {
    throw "Time must use 24-hour HH:mm format, for example 02:00."
}

$action = New-ScheduledTaskAction -Execute "PowerShell.exe" -Argument "-NoProfile -ExecutionPolicy Bypass -File `"$backupScript`""
$trigger = New-ScheduledTaskTrigger -Daily -At $scheduledTime
$principal = New-ScheduledTaskPrincipal -UserId "SYSTEM" -LogonType ServiceAccount -RunLevel Highest
$settings = New-ScheduledTaskSettingsSet -StartWhenAvailable

Register-ScheduledTask -TaskName $TaskName -Action $action -Trigger $trigger -Principal $principal -Settings $settings -Force | Out-Null
Write-Host "Daily backup task '$TaskName' is scheduled for $Time."