$scriptDir = $PSScriptRoot
$sourcePath = Join-Path $scriptDir "..\BestelApp_Web\BestelApp.db"
$backupDir = Join-Path $scriptDir "..\backups\sqlite"
$timestamp = Get-Date -Format "yyyyMMdd_HHmmss"

# Ensure backup directory exists
if (-not (Test-Path -Path $backupDir)) {
    New-Item -ItemType Directory -Path $backupDir | Out-Null
    Write-Host "Created backup directory: $backupDir"
}

if (Test-Path -Path $sourcePath) {
    $destinationPath = Join-Path -Path $backupDir -ChildPath "BestelApp_$timestamp.db"
    Copy-Item -Path $sourcePath -Destination $destinationPath
    Write-Host "Backup created at: $destinationPath"
} else {
    Write-Error "Source database not found at: $sourcePath"
}
