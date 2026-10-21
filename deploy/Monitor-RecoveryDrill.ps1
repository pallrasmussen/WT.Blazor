param(
    [string]$TaskName = "WT.Blazor Database Recovery Drill",
    [int]$MaximumAgeDays = 35,
    [string]$SmtpServer,
    [int]$SmtpPort = 25,
    [string]$From,
    [string[]]$To
)

. (Join-Path $PSScriptRoot "OperationsAlert.ps1")

if ($MaximumAgeDays -lt 1) {
    throw "MaximumAgeDays must be at least 1."
}

$task = Get-ScheduledTask -TaskName $TaskName -ErrorAction Stop
$info = Get-ScheduledTaskInfo -TaskName $TaskName -ErrorAction Stop
$failure = $null
if ($info.LastRunTime -eq [DateTime]::MinValue) {
    $failure = "Recovery drill task '$TaskName' has never run."
}
elseif (([DateTime]::Now - $info.LastRunTime).TotalDays -gt $MaximumAgeDays) {
    $failure = "Recovery drill task '$TaskName' last ran $($info.LastRunTime) and is older than $MaximumAgeDays days."
}
elseif ($info.LastTaskResult -ne 0) {
    $failure = "Recovery drill task '$TaskName' failed with exit code $($info.LastTaskResult) at $($info.LastRunTime)."
}

if ($failure) {
    Send-OperationsAlert -Subject "WT.Blazor database recovery drill failed or is overdue" -Body $failure -SmtpServer $SmtpServer -SmtpPort $SmtpPort -From $From -To $To
    throw $failure
}

Write-Host "PASS Recovery drill '$TaskName' completed at $($info.LastRunTime)."
