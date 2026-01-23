#!/usr/bin/env bash
set -euo pipefail

# ==========================================
# VM Backup Setup Script
# ==========================================
# Eenmalig uitvoeren op de Ubuntu VM als user 'itproj'
# Configureert backup directories, scripts en cron jobs
# ==========================================

# ===== Configuration =====
BACKUP_ROOT="/home/itproj/project_backups"
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
TARGET_DIR="/home/itproj"

# ===== Helper Functions =====
log() {
    echo "[SETUP] $1"
}

error() {
    echo "[ERROR] $1" >&2
    exit 1
}

# ===== Pre-flight Checks =====
log "Starting VM backup setup..."

# Check if running as itproj user
CURRENT_USER=$(whoami)
if [[ "$CURRENT_USER" != "itproj" ]]; then
    log "WARNING: This script is designed to run as user 'itproj', but you are: $CURRENT_USER"
    log "Continuing anyway, but paths may need adjustment..."
fi

# ===== Create Backup Directories =====
log "Creating backup directories..."
mkdir -p "$BACKUP_ROOT/sqlite"
mkdir -p "$BACKUP_ROOT/rabbitmq"
log "✓ Directories created: $BACKUP_ROOT/{sqlite,rabbitmq}"

# ===== Deploy Backup Scripts =====
log "Deploying backup scripts to $TARGET_DIR..."

# Copy SQLite backup script
if [[ -f "$SCRIPT_DIR/backup_sqlite.sh" ]]; then
    cp "$SCRIPT_DIR/backup_sqlite.sh" "$TARGET_DIR/backup_sqlite.sh"
    chmod +x "$TARGET_DIR/backup_sqlite.sh"
    log "✓ Deployed: $TARGET_DIR/backup_sqlite.sh"
else
    error "Source script not found: $SCRIPT_DIR/backup_sqlite.sh"
fi

# Copy RabbitMQ backup script
if [[ -f "$SCRIPT_DIR/backup_rabbitmq.sh" ]]; then
    cp "$SCRIPT_DIR/backup_rabbitmq.sh" "$TARGET_DIR/backup_rabbitmq.sh"
    chmod +x "$TARGET_DIR/backup_rabbitmq.sh"
    log "✓ Deployed: $TARGET_DIR/backup_rabbitmq.sh"
else
    error "Source script not found: $SCRIPT_DIR/backup_rabbitmq.sh"
fi

# ===== Check Required Tools =====
log "Checking required tools..."

MISSING_TOOLS=()

if ! command -v curl >/dev/null 2>&1; then
    MISSING_TOOLS+=("curl")
fi

if ! command -v sqlite3 >/dev/null 2>&1; then
    MISSING_TOOLS+=("sqlite3")
fi

if [[ ${#MISSING_TOOLS[@]} -gt 0 ]]; then
    log "⚠ WARNING: Missing required tools: ${MISSING_TOOLS[*]}"
    log ""
    log "Please install them manually with:"
    log "  sudo apt update"
    log "  sudo apt install -y ${MISSING_TOOLS[*]}"
    log ""
    log "Note: Backups may fail without these tools!"
else
    log "✓ All required tools are installed (curl, sqlite3)"
fi

# ===== Setup Cron Jobs =====
log "Setting up cron jobs (idempotent)..."

CRON_SQLITE="0 * * * * $TARGET_DIR/backup_sqlite.sh >> $BACKUP_ROOT/sqlite/cron.log 2>&1"
CRON_RABBITMQ="0 2 * * * $TARGET_DIR/backup_rabbitmq.sh >> $BACKUP_ROOT/rabbitmq/cron.log 2>&1"

# Get current crontab (ignore error if empty)
CURRENT_CRON=$(crontab -l 2>/dev/null || true)

# Function to add cron entry if not exists
add_cron_if_missing() {
    local CRON_ENTRY="$1"
    local DESCRIPTION="$2"
    
    if echo "$CURRENT_CRON" | grep -qF "$CRON_ENTRY"; then
        log "✓ Cron job already exists: $DESCRIPTION"
    else
        # Add to crontab
        (echo "$CURRENT_CRON"; echo "$CRON_ENTRY") | crontab -
        CURRENT_CRON=$(crontab -l 2>/dev/null || true)
        log "✓ Added cron job: $DESCRIPTION"
    fi
}

add_cron_if_missing "$CRON_SQLITE" "SQLite backup (hourly at :00)"
add_cron_if_missing "$CRON_RABBITMQ" "RabbitMQ backup (daily at 02:00)"

# ===== Summary =====
log ""
log "=========================================="
log "VM Backup Setup Completed Successfully!"
log "=========================================="
log ""
log "Backup locations:"
log "  SQLite:   $BACKUP_ROOT/sqlite/"
log "  RabbitMQ: $BACKUP_ROOT/rabbitmq/"
log ""
log "Deployed scripts:"
log "  $TARGET_DIR/backup_sqlite.sh"
log "  $TARGET_DIR/backup_rabbitmq.sh"
log ""
log "Cron schedule:"
log "  SQLite:   Every hour at minute 0"
log "  RabbitMQ: Every day at 02:00"
log ""
log "Cron logs:"
log "  SQLite:   $BACKUP_ROOT/sqlite/cron.log"
log "  RabbitMQ: $BACKUP_ROOT/rabbitmq/cron.log"
log ""
log "To verify cron jobs:"
log "  crontab -l | grep backup"
log ""
log "To test backups manually:"
log "  $TARGET_DIR/backup_sqlite.sh"
log "  $TARGET_DIR/backup_rabbitmq.sh"
log ""
log "Retention policy: Maximum 5 newest backups per type"
log ""
