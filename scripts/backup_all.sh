#!/usr/bin/env bash
set -euo pipefail

# One timestamped folder per run (RabbitMQ + SQLite)
# You can override BACKUP_ROOT, RMQ_* and DB_PATH via env vars.

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

"$SCRIPT_DIR/backup_rabbitmq.sh"
"$SCRIPT_DIR/backup_sqlite.sh"

# Optional: compress the newest backup folder
BACKUP_ROOT="${BACKUP_ROOT:-backups}"
LATEST="$(ls -1dt "$BACKUP_ROOT"/* 2>/dev/null | head -n 1 || true)"
if [[ -n "${LATEST}" && -d "${LATEST}" ]]; then
  tar -czf "${LATEST}.tar.gz" -C "$(dirname "$LATEST")" "$(basename "$LATEST")"
  echo "[OK] Compressed: ${LATEST}.tar.gz"
fi
