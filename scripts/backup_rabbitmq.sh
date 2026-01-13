#!/usr/bin/env bash
set -euo pipefail

# ===== Config =====
RMQ_HOST="${RMQ_HOST:-10.2.160.221}"
RMQ_PORT="${RMQ_PORT:-15672}"        # RabbitMQ Management port
RMQ_USER="${RMQ_USER:-user}"
RMQ_PASS="${RMQ_PASS:-user123!}"
BACKUP_ROOT="${BACKUP_ROOT:-backups}"

# ===== Logic =====
TS="$(date +%Y%m%d_%H%M%S)"
OUT_DIR="$BACKUP_ROOT/$TS/rabbitmq"
mkdir -p "$OUT_DIR"

OUT_FILE="$OUT_DIR/definitions.json"

# Export all definitions (exchanges/queues/bindings/policies, incl. DLQ/retry if configured)
# NOTE: keep quotes around -u because of special chars like '!'
curl -sS -u "${RMQ_USER}:${RMQ_PASS}" \
  "http://${RMQ_HOST}:${RMQ_PORT}/api/definitions" \
  -o "$OUT_FILE"

# Basic sanity check
if [[ ! -s "$OUT_FILE" ]]; then
  echo "[ERROR] RabbitMQ export failed: $OUT_FILE is empty" >&2
  exit 1
fi

echo "[OK] RabbitMQ definitions saved to: $OUT_FILE"
