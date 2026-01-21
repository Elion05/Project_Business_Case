#!/usr/bin/env bash
set -euo pipefail

# ==========================================
# SQLite Backup Script voor Ubuntu VM
# ==========================================
# Maakt een backup van de SQLite database met timestamp
# Implementeert retention policy: max 5 nieuwste backups bewaren
# Logs worden geschreven naar cron.log
# ==========================================

# ===== Configuration =====
SQLITE_DB_PATH="${SQLITE_DB_PATH:-/home/data/idempotency.db}"
BACKUP_DIR="/home/itproj/project_backups/sqlite"
TIMESTAMP=$(date +%Y%m%d_%H%M%S)
BACKUP_FILE="${BACKUP_DIR}/idempotency_${TIMESTAMP}.db"
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
log "Starting SQLite backup..."

# Check if source database exists
if [[ ! -f "$SQLITE_DB_PATH" ]]; then
    error "Source database not found at: $SQLITE_DB_PATH"
fi

# Check if source database is not empty
if [[ ! -s "$SQLITE_DB_PATH" ]]; then
    error "Source database is empty (0 bytes): $SQLITE_DB_PATH"
fi

# Ensure backup directory exists
mkdir -p "$BACKUP_DIR"

# ===== Perform Backup =====
log "Creating backup: $BACKUP_FILE"

# Use sqlite3 .backup command for safe online backup (avoids corruption)
if command -v sqlite3 >/dev/null 2>&1; then
    if ! sqlite3 "$SQLITE_DB_PATH" ".backup '$BACKUP_FILE'"; then
        error "sqlite3 .backup command failed"
    fi
    log "Backup created using sqlite3 .backup"
else
    # Fallback to cp if sqlite3 not available
    log "WARNING: sqlite3 not found, using cp (less safe)"
    if ! cp -f "$SQLITE_DB_PATH" "$BACKUP_FILE"; then
        error "cp command failed"
    fi
fi

# Verify backup was created and is not empty
if [[ ! -f "$BACKUP_FILE" ]]; then
    error "Backup file was not created: $BACKUP_FILE"
fi

if [[ ! -s "$BACKUP_FILE" ]]; then
    error "Backup file is empty: $BACKUP_FILE"
fi

BACKUP_SIZE=$(du -h "$BACKUP_FILE" | cut -f1)
log "Backup successful: $BACKUP_FILE (size: $BACKUP_SIZE)"

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
done < <(find "$BACKUP_DIR" -name "idempotency_*.db" -type f -printf '%T@ %p\n' | \
         sort -rn | \
         tail -n +$((MAX_BACKUPS + 1)) | \
         cut -d' ' -f2-)

if [[ $DELETED_COUNT -gt 0 ]]; then
    log "Retention: kept newest $MAX_BACKUPS backups, deleted $DELETED_COUNT old backup(s)"
else
    log "Retention: all backups within limit (total: $(find "$BACKUP_DIR" -name "idempotency_*.db" | wc -l))"
fi

log "SQLite backup completed successfully"
