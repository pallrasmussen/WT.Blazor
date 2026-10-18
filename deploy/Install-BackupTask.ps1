#Requires -RunAsAdministrator
param(
    [string]$TaskName = "WT.Blazor Database Backup",
    [string]$Time = "02:00",
    [string]$MonitorTaskName = "WT.Blazor Database Backup Monitor",
    [string]$MonitorTime = "08:00",
    [string]$BackupPath = "C:\inetpub\backups\WT.Blazor",
    [int]$MaximumBackupAgeHours = 30,
    [string]$SmtpServer,
    [int]$SmtpPort = 25,
    [string]$From,
    [string[]]$To
)

$backupScript = Join-Path $PSScriptRoot "Backup-Database.ps1"
if (-not (Test-Path $backupScript)) {
    throw "Backup script not found: $backupScript"
}

try {
    $scheduledTime = [DateTime]::ParseExact($Time, "HH:mm", $null)
    $monitorScheduledTime = [DateTime]::ParseExact($MonitorTime, "HH:mm", $null)
}
catch {
    throw "Time and MonitorTime must use 24-hour HH:mm format, for example 02:00."
}
if ($MaximumBackupAgeHours -lt 1) {
    throw "MaximumBackupAgeHours must be at least 1."
}

$action = New-ScheduledTaskAction -Execute "PowerShell.exe" -Argument "-NoProfile -ExecutionPolicy Bypass -File `"$backupScript`""
$trigger = New-ScheduledTaskTrigger -Daily -At $scheduledTime
$principal = New-ScheduledTaskPrincipal -UserId "SYSTEM" -LogonType ServiceAccount -RunLevel Highest
$settings = New-ScheduledTaskSettingsSet -StartWhenAvailable

Register-ScheduledTask -TaskName $TaskName -Action $action -Trigger $trigger -Principal $principal -Settings $settings -Force | Out-Null
Write-Host "Daily backup task '$TaskName' is scheduled for $Time."

$monitorScript = Join-Path $PSScriptRoot "Monitor-Backup.ps1"
if (-not (Test-Path $monitorScript)) {
    throw "Backup monitor script not found: $monitorScript"
}
$monitorArguments = "-NoProfile -ExecutionPolicy Bypass -File `"$monitorScript`" -BackupPath `"$BackupPath`" -MaximumAgeHours $MaximumBackupAgeHours"
if ($SmtpServer -and $From -and $To -and $To.Count -gt 0) {
    $monitorArguments += " -SmtpServer `"$SmtpServer`" -SmtpPort $SmtpPort -From `"$From`" -To"
    $monitorArguments += " " + (($To | ForEach-Object { "`"$_`"" }) -join " ")
}
$monitorAction = New-ScheduledTaskAction -Execute "PowerShell.exe" -Argument $monitorArguments
$monitorTrigger = New-ScheduledTaskTrigger -Daily -At $monitorScheduledTime
Register-ScheduledTask -TaskName $MonitorTaskName -Action $monitorAction -Trigger $monitorTrigger -Principal $principal -Settings $settings -Force | Out-Null
Write-Host "Daily backup freshness monitor '$MonitorTaskName' is scheduled for $MonitorTime."