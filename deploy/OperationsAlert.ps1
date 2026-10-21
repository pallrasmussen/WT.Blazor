function Send-OperationsAlert {
    param(
        [Parameter(Mandatory)][string]$Subject,
        [Parameter(Mandatory)][string]$Body,
        [string]$SmtpServer,
        [int]$SmtpPort = 25,
        [string]$From,
        [string[]]$To
    )

    if (-not ($SmtpServer -and $From -and $To -and $To.Count -gt 0)) {
        return
    }

    $mail = [System.Net.Mail.MailMessage]::new($From, $To[0])
    foreach ($recipient in $To | Select-Object -Skip 1) {
        $mail.To.Add($recipient)
    }
    $mail.Subject = $Subject
    $mail.Body = $Body
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
