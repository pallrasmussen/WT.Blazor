param(
    [Parameter(Mandatory)][ValidatePattern('^\d+\.\d+\.\d+(-[0-9A-Za-z.-]+)?$')][string]$Version,
    [Parameter(Mandatory)][string]$OutputPath
)
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$dirty = git -C $root status --porcelain --untracked-files=normal
if ($LASTEXITCODE -ne 0 -or $dirty) { throw 'Create releases from a clean committed checkout.' }
$commit = git -C $root rev-parse HEAD
if ($LASTEXITCODE -ne 0) { throw 'Cannot identify the release commit.' }
$OutputPath = [IO.Path]::GetFullPath($OutputPath)
if (Test-Path -LiteralPath $OutputPath) { throw 'OutputPath must be a new directory; release artifacts are never overwritten.' }
New-Item -ItemType Directory -Path $OutputPath | Out-Null
$bundle = Join-Path $OutputPath 'bundle'
$app = Join-Path $bundle 'app'
$operations = Join-Path $bundle 'operations'
dotnet publish (Join-Path $root 'WT.Blazor.csproj') -c Release --no-restore -p:Version=$Version -o $app
if ($LASTEXITCODE -ne 0) { throw 'Application publish failed.' }
dotnet publish (Join-Path $root 'tools/WT.Blazor.Operations/WT.Blazor.Operations.csproj') -c Release --no-restore -p:Version=$Version -o $operations
if ($LASTEXITCODE -ne 0) { throw 'Operations tool publish failed.' }
$manifest = Get-Content (Join-Path $app 'deployment-manifest.json') -Raw | ConvertFrom-Json
if ($manifest.commitSha -ne $commit -or $manifest.buildVersion -ne $Version) { throw 'Deployment manifest does not identify this release.' }
Copy-Item -LiteralPath (Join-Path $root 'deploy') -Destination (Join-Path $bundle 'deploy') -Recurse
Copy-Item -LiteralPath (Join-Path $root 'docs') -Destination (Join-Path $bundle 'docs') -Recurse
$files = @(Get-ChildItem -LiteralPath $bundle -File -Recurse | ForEach-Object {
    [ordered]@{ path = [IO.Path]::GetRelativePath($bundle, $_.FullName).Replace('\', '/'); sha256 = (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash }
})
[ordered]@{ version=$Version; commitSha=$commit; files=$files } | ConvertTo-Json -Depth 5 | Set-Content (Join-Path $bundle 'release-manifest.json') -Encoding utf8
$zip = Join-Path $OutputPath "WT.Blazor-$Version.zip"
Compress-Archive -Path "$bundle/*" -DestinationPath $zip
"$((Get-FileHash -LiteralPath $zip -Algorithm SHA256).Hash)  $([IO.Path]::GetFileName($zip))" | Set-Content "$zip.sha256" -Encoding ascii
Write-Host "Release package: $zip"
