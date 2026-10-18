param(
    [string]$BackupPath = "C:\inetpub\backups\WT.Blazor",
    [int]$MaximumAgeHours = 30,
    [string]$SmtpServer,
    [int]$SmtpPort = 25,
    [string]$From,
    [string[]]$To
)

if ($MaximumAgeHours -lt 1) {
    throw "MaximumAgeHours must be at least 1."
}

$backup = Get-ChildItem -Path $BackupPath -Filter "wt-blazor-*.db" -File -ErrorAction SilentlyContinue |
    Sort-Object LastWriteTimeUtc -Descending |
    Select-Object -First 1

$failure = $null
if (-not $backup) {
    $failure = "No database backups were found in $BackupPath."
}
elseif ($backup.Length -eq 0) {
    $failure = "The newest database backup is empty: $($backup.FullName)."
}
else {
    $ageHours = ([DateTime]::UtcNow - $backup.LastWriteTimeUtc).TotalHours
    $keyBackup = Join-Path $BackupPath "$($backup.BaseName).keys"
    if (-not (Test-Path $keyBackup -PathType Container)) {
        $failure = "The newest database backup has no matching Data Protection key-ring archive: $($backup.BaseName).keys."
    }
    elseif ($ageHours -lt -0.1) {
        $failure = "The newest database backup has a future timestamp: $($backup.FullName)."
    }
    elseif ($ageHours -gt $MaximumAgeHours) {
        $failure = "The newest database backup is $([Math]::Floor($ageHours)) hours old (limit: $MaximumAgeHours hours): $($backup.FullName)."
    }
}

if ($failure) {
    if ($SmtpServer -and $From -and $To -and $To.Count -gt 0) {
        $mail = [System.Net.Mail.MailMessage]::new($From, $To[0])
        foreach ($recipient in $To | Select-Object -Skip 1) {
            $mail.To.Add($recipient)
        }
        $mail.Subject = "WT.Blazor database backup is stale or missing"
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

Write-Host "PASS Latest database backup $($backup.Name) is $([Math]::Floor($ageHours)) hours old."
