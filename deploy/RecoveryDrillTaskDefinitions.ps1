function Install-RecoveryDrillScheduledTasks {
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
        [string[]]$To,
        [scriptblock]$RegisterTask = {
            param($parameters)
            Register-ScheduledTask @parameters
        }
    )

    $drillScript = Join-Path $PSScriptRoot "Test-BackupRestore.ps1"
    $monitorScript = Join-Path $PSScriptRoot "Monitor-RecoveryDrill.ps1"
    foreach ($script in @($drillScript, $monitorScript)) {
        if (-not (Test-Path -LiteralPath $script)) {
            throw "Required script not found: $script"
        }
    }
    if (-not (Test-Path -LiteralPath $OperationsPath)) {
        throw "Operations tool not found: $OperationsPath"
    }
    try {
        $scheduledTime = [DateTime]::ParseExact($Time, "HH:mm", $null)
        $monitorScheduledTime = [DateTime]::ParseExact($MonitorTime, "HH:mm", $null)
    }
    catch {
        throw "Time and MonitorTime must use 24-hour HH:mm format, for example 03:00."
    }
    if ($MaximumAgeDays -lt 1) {
        throw "MaximumAgeDays must be at least 1."
    }

    $principal = New-ScheduledTaskPrincipal -UserId "SYSTEM" -LogonType ServiceAccount -RunLevel Highest
    $settings = New-ScheduledTaskSettingsSet -StartWhenAvailable
    $drillArguments = "-NoProfile -ExecutionPolicy Bypass -File `"$drillScript`" -BackupPath `"$BackupPath`" -OperationsPath `"$OperationsPath`" -WorkPath `"$WorkPath`""
    $drillAction = New-ScheduledTaskAction -Execute "PowerShell.exe" -Argument $drillArguments
    $drillTrigger = New-ScheduledTaskTrigger -Weekly -WeeksInterval 4 -DaysOfWeek Sunday -At $scheduledTime
    & $RegisterTask @{
        TaskName = $TaskName
        Action = $drillAction
        Trigger = $drillTrigger
        Principal = $principal
        Settings = $settings
        Force = $true
    } | Out-Null
    Write-Host "Recovery drill task '$TaskName' is scheduled every four weeks at $Time on Sunday."

    $monitorArguments = "-NoProfile -ExecutionPolicy Bypass -File `"$monitorScript`" -TaskName `"$TaskName`" -MaximumAgeDays $MaximumAgeDays"
    if ($SmtpServer -and $From -and $To -and $To.Count -gt 0) {
        $monitorArguments += " -SmtpServer `"$SmtpServer`" -SmtpPort $SmtpPort -From `"$From`" -To"
        $monitorArguments += " " + (($To | ForEach-Object { "`"$_`"" }) -join " ")
    }
    $monitorAction = New-ScheduledTaskAction -Execute "PowerShell.exe" -Argument $monitorArguments
    $monitorTrigger = New-ScheduledTaskTrigger -Weekly -WeeksInterval 4 -DaysOfWeek Sunday -At $monitorScheduledTime
    & $RegisterTask @{
        TaskName = $MonitorTaskName
        Action = $monitorAction
        Trigger = $monitorTrigger
        Principal = $principal
        Settings = $settings
        Force = $true
    } | Out-Null
    Write-Host "Recovery drill monitor '$MonitorTaskName' is scheduled every four weeks at $MonitorTime on Sunday."
}
