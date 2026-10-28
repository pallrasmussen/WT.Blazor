param(
    [Parameter(Mandatory)]
    [string]$BaseUrl,
    [Parameter(Mandatory)]
    [ValidateSet("Viewer", "Contributor", "Admin")]
    [string]$Role,
    [Parameter(Mandatory)]
    [string]$ExpectedIdentity
)

if (-not $IsWindows) { throw "Run this check on Windows as the dedicated staging identity." }
$previousIdentity = $env:STAGING_BROWSER_EXPECTED_IDENTITY
$previousRunAuthenticatedBrowserTests = $env:RUN_AUTH_BROWSER_TESTS
$previousBrowserBaseUrl = $env:BROWSER_BASE_URL
$previousStagingBrowserRole = $env:STAGING_BROWSER_ROLE
try {
    $env:STAGING_BROWSER_EXPECTED_IDENTITY = $ExpectedIdentity
    $env:RUN_AUTH_BROWSER_TESTS = "1"
    $env:BROWSER_BASE_URL = $BaseUrl
    $env:STAGING_BROWSER_ROLE = $Role
    dotnet test (Join-Path $PSScriptRoot "..\WT.Blazor.Tests\WT.Blazor.Tests.csproj") --no-restore --filter "FullyQualifiedName~AuthenticatedStagingBrowserSmokeTests"
    $exitCode = $LASTEXITCODE
}
finally {
    if ($null -eq $previousIdentity) { Remove-Item Env:STAGING_BROWSER_EXPECTED_IDENTITY -ErrorAction SilentlyContinue } else { $env:STAGING_BROWSER_EXPECTED_IDENTITY = $previousIdentity }
    if ($null -eq $previousRunAuthenticatedBrowserTests) { Remove-Item Env:RUN_AUTH_BROWSER_TESTS -ErrorAction SilentlyContinue } else { $env:RUN_AUTH_BROWSER_TESTS = $previousRunAuthenticatedBrowserTests }
    if ($null -eq $previousBrowserBaseUrl) { Remove-Item Env:BROWSER_BASE_URL -ErrorAction SilentlyContinue } else { $env:BROWSER_BASE_URL = $previousBrowserBaseUrl }
    if ($null -eq $previousStagingBrowserRole) { Remove-Item Env:STAGING_BROWSER_ROLE -ErrorAction SilentlyContinue } else { $env:STAGING_BROWSER_ROLE = $previousStagingBrowserRole }
}

exit $exitCode