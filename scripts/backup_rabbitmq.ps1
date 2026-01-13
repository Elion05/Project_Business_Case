$rabbitMqUrl = "http://10.2.160.221:15672/api/definitions"
$username = "user"
$password = "user123!"
$scriptDir = $PSScriptRoot
$backupDir = Join-Path $scriptDir "..\backups\rabbitmq"
$timestamp = Get-Date -Format "yyyyMMdd_HHmmss"

# Ensure backup directory exists
if (-not (Test-Path -Path $backupDir)) {
    New-Item -ItemType Directory -Path $backupDir | Out-Null
    Write-Host "Created backup directory: $backupDir"
}

$base64AuthInfo = [Convert]::ToBase64String([Text.Encoding]::ASCII.GetBytes(("${username}:${password}")))
$headers = @{Authorization=("Basic {0}" -f $base64AuthInfo)}

try {
    $response = Invoke-RestMethod -Uri $rabbitMqUrl -Method Get -Headers $headers
    $destinationPath = Join-Path -Path $backupDir -ChildPath "rabbitmq_definitions_$timestamp.json"
    $response | ConvertTo-Json -Depth 10 | Set-Content -Path $destinationPath
    Write-Host "RabbitMQ definitions exported to: $destinationPath"
} catch {
    Write-Error "Failed to export RabbitMQ definitions. Error: $_"
}
