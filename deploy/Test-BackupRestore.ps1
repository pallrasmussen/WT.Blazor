param(
    [string]$BackupPath = "C:\inetpub\backups\WT.Blazor",
    [string]$AppPath = "C:\inetpub\wwwroot\WT.Blazor"
)

$backup = Get-ChildItem -Path $BackupPath -Filter "wt-blazor-*.db" |
    Sort-Object LastWriteTime -Descending |
    Select-Object -First 1

if ($null -eq $backup) {
    throw "No database backup was found in $BackupPath."
}

$sqliteAssembly = Join-Path $AppPath "Microsoft.Data.Sqlite.dll"
if (-not (Test-Path $sqliteAssembly)) {
    throw "Microsoft.Data.Sqlite.dll was not found at $sqliteAssembly. Publish the app before running this check."
}

$restorePath = Join-Path $env:TEMP "wt-blazor-restore-test-$([guid]::NewGuid().ToString('N')).db"
Copy-Item -Path $backup.FullName -Destination $restorePath -ErrorAction Stop

try {
    [System.Reflection.Assembly]::LoadFrom($sqliteAssembly) | Out-Null
    $connection = [Microsoft.Data.Sqlite.SqliteConnection]::new("Data Source=$restorePath;Mode=ReadOnly")
    $connection.Open()
    try {
        $command = $connection.CreateCommand()
        $command.CommandText = "PRAGMA integrity_check;"
        if ($command.ExecuteScalar() -ne "ok") {
            throw "SQLite integrity check failed for $($backup.FullName)."
        }

        $command.CommandText = "SELECT COUNT(*) FROM WaitingTimeHistoryEntries;"
        $entries = $command.ExecuteScalar()
        Write-Host "PASS $($backup.Name): SQLite integrity check succeeded ($entries audit records)."
    }
    finally {
        $connection.Dispose()
    }
}
finally {
    Remove-Item -Path $restorePath -Force -ErrorAction SilentlyContinue
}