Param(
    [string]$BackupRoot = "backups"
)

# Uses defaults inside the scripts, or override with params when calling them.

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path

& (Join-Path $scriptDir "backup_rabbitmq.ps1") -BackupRoot $BackupRoot
& (Join-Path $scriptDir "backup_sqlite.ps1") -BackupRoot $BackupRoot

# Optional: zip latest folder
$latest = Get-ChildItem -Directory $BackupRoot | Sort-Object Name -Descending | Select-Object -First 1
if ($latest) {
    $zipPath = "$($latest.FullName).zip"
    if (Test-Path $zipPath) { Remove-Item $zipPath -Force }
    Compress-Archive -Path $latest.FullName -DestinationPath $zipPath
    Write-Host "[OK] Compressed: $zipPath"
}
