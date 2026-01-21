#!/usr/bin/env bash
set -euo pipefail

# ==========================================
# RabbitMQ Backup Script voor Ubuntu VM
# ==========================================
# Exporteert RabbitMQ definitions via Management API
# Implementeert retention policy: max 5 nieuwste backups bewaren
# Logs worden geschreven naar cron.log
# ==========================================

# ===== Configuration =====
RMQ_USER="${RMQ_USER:-user}"
RMQ_PASS="${RMQ_PASS:-user123!}"
RMQ_HOST="${RMQ_HOST:-127.0.0.1}"
RMQ_PORT="${RMQ_PORT:-15672}"
BACKUP_DIR="/home/itproj/project_backups/rabbitmq"
TIMESTAMP=$(date +%Y%m%d_%H%M%S)
BACKUP_FILE="${BACKUP_DIR}/definitions_${TIMESTAMP}.json"
MAX_BACKUPS=5

# ===== Helper Functions =====
log() {
    echo "[$(date '+%Y-%m-%d %H:%M:%S')] $1"
}

error() {
    log "ERROR: $1" >&2
    exit 1
}

# ===== Sanity Checks =====
log "Starting RabbitMQ definitions backup..."

# Check if curl is available
if ! command -v curl >/dev/null 2>&1; then
    error "curl is not installed. Please install it: sudo apt install curl"
fi

# Ensure backup directory exists
mkdir -p "$BACKUP_DIR"

# ===== Perform Backup =====
log "Exporting definitions from http://${RMQ_HOST}:${RMQ_PORT}/api/definitions"

# Export definitions via Management API
# Using -sS: silent but show errors
# Using quotes around credentials to handle special characters
API_URL="http://${RMQ_HOST}:${RMQ_PORT}/api/definitions"

if ! curl -sS -u "${RMQ_USER}:${RMQ_PASS}" "$API_URL" -o "$BACKUP_FILE"; then
    error "curl command failed (exit code: $?). Check if RabbitMQ Management API is accessible."
fi

log "Export completed: $BACKUP_FILE"

# ===== Validate Backup =====
# Check if file exists
if [[ ! -f "$BACKUP_FILE" ]]; then
    error "Backup file was not created: $BACKUP_FILE"
fi

# Check if file is not empty
if [[ ! -s "$BACKUP_FILE" ]]; then
    error "Backup file is empty. RabbitMQ API may be unreachable or returned empty response."
fi

# Check if response contains error (simple check for "error" keyword)
if grep -qi "error" "$BACKUP_FILE"; then
    log "WARNING: Backup file may contain error response:"
    head -n 5 "$BACKUP_FILE" >&2
    error "RabbitMQ API returned an error response. Check credentials and API availability."
fi

# Basic JSON validation (check if it starts with '{' or '[')
FIRST_CHAR=$(head -c 1 "$BACKUP_FILE")
if [[ "$FIRST_CHAR" != "{" && "$FIRST_CHAR" != "[" ]]; then
    error "Backup file does not appear to be valid JSON"
fi

BACKUP_SIZE=$(du -h "$BACKUP_FILE" | cut -f1)
log "Backup validation successful: $BACKUP_FILE (size: $BACKUP_SIZE)"

# ===== Retention Policy =====
log "Applying retention policy (keep newest $MAX_BACKUPS backups)..."

# Find all backup files (exclude cron.log)
# Sort by modification time (newest first), skip the first MAX_BACKUPS, delete the rest
DELETED_COUNT=0
while IFS= read -r old_backup; do
    if [[ -f "$old_backup" ]]; then
        log "Deleting old backup: $(basename "$old_backup")"
        rm -f "$old_backup"
        ((DELETED_COUNT++))
    fi
done < <(find "$BACKUP_DIR" -name "definitions_*.json" -type f -printf '%T@ %p\n' | \
         sort -rn | \
         tail -n +$((MAX_BACKUPS + 1)) | \
         cut -d' ' -f2-)

if [[ $DELETED_COUNT -gt 0 ]]; then
    log "Retention: kept newest $MAX_BACKUPS backups, deleted $DELETED_COUNT old backup(s)"
else
    log "Retention: all backups within limit (total: $(find "$BACKUP_DIR" -name "definitions_*.json" | wc -l))"
fi

log "RabbitMQ backup completed successfully"
