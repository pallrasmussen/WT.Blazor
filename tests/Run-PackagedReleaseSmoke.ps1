param(
    [Parameter(Mandatory)][string]$ReleasePath,
    [int]$TimeoutSeconds = 60
)

$ErrorActionPreference = 'Stop'
$ReleasePath = (Resolve-Path -LiteralPath $ReleasePath).Path
$appPath = Join-Path $ReleasePath 'app'
$application = Join-Path $appPath 'WT.Blazor.dll'
if (-not (Test-Path -LiteralPath $application)) {
    throw "Packaged application was not found: $application"
}

$workPath = Join-Path ([IO.Path]::GetTempPath()) "wt-packaged-smoke-$([guid]::NewGuid().ToString('N'))"
New-Item -ItemType Directory -Path $workPath | Out-Null
$listener = [Net.Sockets.TcpListener]::new([Net.IPAddress]::Loopback, 0)
$listener.Start()
$port = $listener.LocalEndpoint.Port
$listener.Stop()
$url = "http://127.0.0.1:$port"
$variables = @{
    ASPNETCORE_ENVIRONMENT = 'Development'
    Hosting__RedirectToHttps = 'false'
    ConnectionStrings__DefaultConnection = "Data Source=$(Join-Path $workPath 'smoke.db');Pooling=False"
    AllowedHosts = 'localhost;127.0.0.1'
}
$previous = @{}
$appProcess = $null
try {
    foreach ($name in $variables.Keys) {
        $previous[$name] = [Environment]::GetEnvironmentVariable($name)
        [Environment]::SetEnvironmentVariable($name, $variables[$name])
    }
    $appProcess = Start-Process dotnet -ArgumentList "`"$application`" --urls $url" -WorkingDirectory $appPath -WindowStyle Hidden -PassThru -RedirectStandardOutput (Join-Path $workPath 'server.log') -RedirectStandardError (Join-Path $workPath 'server-error.log')
    $deadline = [DateTime]::UtcNow.AddSeconds($TimeoutSeconds)
    do {
        if ($appProcess.HasExited) {
            throw "Packaged application exited unexpectedly. See $workPath for logs."
        }
        try {
            if ((Invoke-WebRequest "$url/health" -TimeoutSec 2 -UseBasicParsing).StatusCode -eq 200) {
                break
            }
        }
        catch {
        }
        Start-Sleep -Milliseconds 250
    } while ([DateTime]::UtcNow -lt $deadline)

    if ((Invoke-WebRequest "$url/health" -TimeoutSec 2 -UseBasicParsing).StatusCode -ne 200) {
        throw "Packaged application did not become healthy within $TimeoutSeconds seconds. See $workPath for logs."
    }
    if ((Invoke-WebRequest "$url/" -TimeoutSec 10 -UseBasicParsing).StatusCode -ne 200) {
        throw "Packaged application did not serve the public root. See $workPath for logs."
    }
    Write-Host "PASS Packaged application served health and public-root checks."
}
finally {
    if ($appProcess -and -not $appProcess.HasExited) {
        Stop-Process -Id $appProcess.Id -Force
        $appProcess.WaitForExit()
    }
    foreach ($name in $previous.Keys) {
        [Environment]::SetEnvironmentVariable($name, $previous[$name])
    }
    if (Test-Path -LiteralPath $workPath) {
        for ($attempt = 1; $attempt -le 5; $attempt++) {
            try {
                Remove-Item -LiteralPath $workPath -Recurse -Force -ErrorAction Stop
                break
            }
            catch {
                if ($attempt -eq 5) {
                    throw
                }
                Start-Sleep -Milliseconds 250
            }
        }
    }
}
