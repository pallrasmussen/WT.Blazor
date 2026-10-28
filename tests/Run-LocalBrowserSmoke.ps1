param([string]$WorkPath = (Join-Path $env:TEMP "wt-browser-$([guid]::NewGuid().ToString('N'))"))
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$WorkPath = [IO.Path]::GetFullPath($WorkPath)
if (Test-Path -LiteralPath $WorkPath) { throw 'WorkPath must be a new directory for a disposable database.' }
New-Item -ItemType Directory -Path $WorkPath | Out-Null
$publish = Join-Path $WorkPath 'app'
dotnet publish (Join-Path $root 'WT.Blazor.csproj') --no-restore -o $publish
if ($LASTEXITCODE -ne 0) { throw 'Publishing the browser fixture failed.' }
$listener = [Net.Sockets.TcpListener]::new([Net.IPAddress]::Loopback, 0)
$listener.Start()
$port = $listener.LocalEndpoint.Port
$listener.Stop()
$url = "http://127.0.0.1:$port"
$variables = @{
    ASPNETCORE_ENVIRONMENT = 'Development'
    Hosting__RedirectToHttps = 'false'
    SeedData__EnableDemoData = 'true'
    ConnectionStrings__DefaultConnection = "Data Source=$(Join-Path $WorkPath 'browser.db');Pooling=False"
    AllowedHosts = 'localhost;127.0.0.1'
    RUN_BROWSER_TESTS = '1'
    RUN_MUTATING_BROWSER_TESTS = '1'
    BROWSER_BASE_URL = $url
    BROWSER_A11Y_RESULTS_PATH = (Join-Path $WorkPath 'results')
}
$previous = @{}
$appProcess = $null
try {
    foreach ($name in $variables.Keys) {
        $previous[$name] = [Environment]::GetEnvironmentVariable($name)
        [Environment]::SetEnvironmentVariable($name, $variables[$name])
    }
    $dll = Join-Path $publish 'WT.Blazor.dll'
    $appProcess = Start-Process dotnet -ArgumentList "`"$dll`" --urls $url" -WorkingDirectory $publish -WindowStyle Hidden -PassThru -RedirectStandardOutput (Join-Path $WorkPath 'server.log') -RedirectStandardError (Join-Path $WorkPath 'server-error.log')
    $ready = $false
    $deadline = [DateTime]::UtcNow.AddSeconds(60)
    do {
        if ($appProcess.HasExited) { throw "Browser fixture exited. See $WorkPath for server logs." }
        try { $ready = (Invoke-WebRequest "$url/health" -TimeoutSec 2).StatusCode -eq 200 } catch { }
        if (-not $ready) { Start-Sleep -Milliseconds 250 }
    } while (-not $ready -and [DateTime]::UtcNow -lt $deadline)
    if (-not $ready) { throw "Browser fixture did not become healthy. See $WorkPath." }
    dotnet test (Join-Path $root 'WT.Blazor.Tests/WT.Blazor.Tests.csproj') --no-restore --filter 'FullyQualifiedName~PublicBrowserSmokeTests|FullyQualifiedName~LocalWorkflowBrowserTests' --logger trx --results-directory (Join-Path $WorkPath 'results')
    if ($LASTEXITCODE -ne 0) { throw 'Public browser smoke tests failed.' }
}
finally {
    if ($appProcess -and -not $appProcess.HasExited) { Stop-Process -Id $appProcess.Id -Force }
    foreach ($name in $previous.Keys) { [Environment]::SetEnvironmentVariable($name, $previous[$name]) }
    Write-Host "Browser test logs and disposable data: $WorkPath"
}
