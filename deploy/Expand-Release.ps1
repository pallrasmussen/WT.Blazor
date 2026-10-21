param(
    [Parameter(Mandatory)][string]$PackagePath,
    [Parameter(Mandatory)][string]$DestinationPath
)
$ErrorActionPreference = 'Stop'
$PackagePath = (Resolve-Path -LiteralPath $PackagePath).Path
$expected = ((Get-Content -LiteralPath "$PackagePath.sha256" -Raw).Trim() -split '\s+')[0]
if ((Get-FileHash -LiteralPath $PackagePath -Algorithm SHA256).Hash -ne $expected) { throw 'Release archive checksum mismatch.' }
$DestinationPath = [IO.Path]::GetFullPath($DestinationPath)
if (Test-Path -LiteralPath $DestinationPath) { throw 'Extract into a new directory.' }
# Validate zip entries before extraction so archive paths cannot escape the destination.
$archive = [IO.Compression.ZipFile]::OpenRead($PackagePath)
try {
    foreach ($entry in $archive.Entries) {
        $target = [IO.Path]::GetFullPath((Join-Path $DestinationPath $entry.FullName))
        if (-not $target.StartsWith($DestinationPath.TrimEnd('\', '/') + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) { throw 'Unsafe archive path.' }
    }
} finally { $archive.Dispose() }
Expand-Archive -LiteralPath $PackagePath -DestinationPath $DestinationPath
$manifest = Get-Content -LiteralPath (Join-Path $DestinationPath 'release-manifest.json') -Raw | ConvertFrom-Json
foreach ($entry in $manifest.files) {
    $path = [IO.Path]::GetFullPath((Join-Path $DestinationPath $entry.path))
    if (-not $path.StartsWith($DestinationPath.TrimEnd('\', '/') + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) { throw 'Unsafe manifest path.' }
    if ((Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash -ne $entry.sha256) { throw "Release file checksum mismatch: $($entry.path)" }
}
Write-Host "Verified release $($manifest.version), commit $($manifest.commitSha). Deploy app/ unchanged to staging and production using deploy/ scripts."
