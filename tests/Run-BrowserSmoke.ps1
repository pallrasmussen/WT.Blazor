param(
	[Parameter(Mandatory)]
	[string]$BaseUrl
)

$previousRunBrowserTests = $env:RUN_BROWSER_TESTS
$previousBrowserBaseUrl = $env:BROWSER_BASE_URL
try {
	$env:RUN_BROWSER_TESTS = "1"
	$env:BROWSER_BASE_URL = $BaseUrl
	dotnet test (Join-Path $PSScriptRoot "..\WT.Blazor.Tests\WT.Blazor.Tests.csproj") --no-restore --filter "FullyQualifiedName~PublicBrowserSmokeTests"
	$exitCode = $LASTEXITCODE
}
finally {
	if ($null -eq $previousRunBrowserTests) { Remove-Item Env:RUN_BROWSER_TESTS -ErrorAction SilentlyContinue } else { $env:RUN_BROWSER_TESTS = $previousRunBrowserTests }
	if ($null -eq $previousBrowserBaseUrl) { Remove-Item Env:BROWSER_BASE_URL -ErrorAction SilentlyContinue } else { $env:BROWSER_BASE_URL = $previousBrowserBaseUrl }
}

exit $exitCode