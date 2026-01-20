#!/usr/bin/env bash
set -euo pipefail

# ===== Config =====
DB_PATH="${DB_PATH:-data/bestelapp_local.db}"   # adjust when SQLite is added
BACKUP_ROOT="${BACKUP_ROOT:-backups}"

# ===== Logic =====
TS="$(date +%Y%m%d_%H%M%S)"
OUT_DIR="$BACKUP_ROOT/$TS/sqlite"
mkdir -p "$OUT_DIR"

if [[ ! -f "$DB_PATH" ]]; then
  echo "[WARN] SQLite db not found at '$DB_PATH' (skip)" >&2
  exit 0
fi

DEST="$OUT_DIR/$(basename "$DB_PATH")"

# Prefer SQLite's online backup to avoid corrupted copies
if command -v sqlite3 >/dev/null 2>&1; then
  sqlite3 "$DB_PATH" ".backup '$DEST'"
else
  cp -f "$DB_PATH" "$DEST"
fi

echo "[OK] SQLite backup saved to: $DEST"
