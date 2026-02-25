param(
    [Parameter(Mandatory=$true)]
    [string]$LogPath
)

Write-Host "Checking migration logs at: $LogPath"

$logs = Get-Content $LogPath -ErrorAction SilentlyContinue

if (-not $logs) {
    Write-Host "::error::No logs found at $LogPath"
    exit 1
}

# Check for successful migration completion first
$cultivMigrationSuccess = $logs | Select-String -Pattern "Cultiv Environment Inspect migrations completed successfully" -CaseSensitive:$false
$umbracoInstallSuccess = $logs | Select-String -Pattern "Unattended install completed" -CaseSensitive:$false

# Look for errors AFTER the installation started (exclude initial connection check errors)
$installStartIndex = 0
for ($i = 0; $i -lt $logs.Count; $i++) {
    if ($logs[$i] -match "Starting unattended install") {
        $installStartIndex = $i
        break
    }
}

$postInstallLogs = $logs[$installStartIndex..($logs.Count - 1)]
$errors = $postInstallLogs | Select-String -Pattern "\[ERR\]|\[FTL\]|migration.*fail|PendingModelChanges" -CaseSensitive:$false

if ($errors) {
    Write-Host "::error::Migration errors detected after installation started:"
    $errors | ForEach-Object { Write-Host $_ }
    exit 1
}

if ($cultivMigrationSuccess -and $umbracoInstallSuccess) {
    Write-Host "✓ Database migrations successful"
    Write-Host "✓ Umbraco install completed"
    Write-Host "✓ Cultiv.EnvironmentInspect migrations completed"
} else {
    Write-Host "::warning::Could not verify all migrations completed successfully"
    if (-not $umbracoInstallSuccess) { Write-Host "  - Umbraco install not confirmed" }
    if (-not $cultivMigrationSuccess) { Write-Host "  - Cultiv migrations not confirmed" }
}

exit 0
