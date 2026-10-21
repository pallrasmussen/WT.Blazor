#Requires -RunAsAdministrator
param(
    [string]$TaskName = "WT.Blazor Database Recovery Drill",
    [string]$Time = "03:00",
    [string]$MonitorTaskName = "WT.Blazor Database Recovery Drill Monitor",
    [string]$MonitorTime = "05:00",
    [string]$BackupPath = "C:\inetpub\backups\WT.Blazor",
    [string]$OperationsPath = (Join-Path $PSScriptRoot '../operations/WT.Blazor.Operations.dll'),
    [string]$WorkPath = "C:\inetpub\recovery-drills",
    [int]$MaximumAgeDays = 35,
    [string]$SmtpServer,
    [int]$SmtpPort = 25,
    [string]$From,
    [string[]]$To
)

. (Join-Path $PSScriptRoot "RecoveryDrillTaskDefinitions.ps1")
Install-RecoveryDrillScheduledTasks @PSBoundParameters
