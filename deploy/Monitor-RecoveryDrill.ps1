param(
    [string]$TaskName = "WT.Blazor Database Recovery Drill",
    [int]$MaximumAgeDays = 35,
    [string]$SmtpServer,
    [int]$SmtpPort = 25,
    [string]$From,
    [string[]]$To
)

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
    if ($SmtpServer -and $From -and $To -and $To.Count -gt 0) {
        $mail = [System.Net.Mail.MailMessage]::new($From, $To[0])
        foreach ($recipient in $To | Select-Object -Skip 1) {
            $mail.To.Add($recipient)
        }
        $mail.Subject = "WT.Blazor database recovery drill failed or is overdue"
        $mail.Body = $failure
        $client = [System.Net.Mail.SmtpClient]::new($SmtpServer, $SmtpPort)
        $client.UseDefaultCredentials = $true
        try {
            $client.Send($mail)
        }
        finally {
            $client.Dispose()
            $mail.Dispose()
        }
    }
    throw $failure
}

Write-Host "PASS Recovery drill '$TaskName' completed at $($info.LastRunTime)."
