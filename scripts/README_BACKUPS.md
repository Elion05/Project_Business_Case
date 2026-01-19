# Backup & Reliability Strategy

## RabbitMQ
### Reliability
- **Queues**: Configured as `durable: true`. This ensures queues survive a broker restart.
- **Messages**: Publisher sends messages with `Persistent = true`. This ensures messages are written to disk and survive a broker crash.
- **Configuration Backup**: We export the full RabbitMQ configuration (definitions) including queues, exchanges, and bindings.

### Backup Method
- **Script**: `scripts/backup_rabbitmq.ps1`
- **Mechanism**: Calls the RabbitMQ Management API (`/api/definitions`) to export settings to JSON.
- **Frequency**: Daily (Recommended).
- **Target**: `http://10.2.160.221:15672`

## SQLite (Idempotency)
### Backup Method
- **Script**: `scripts/backup_sqlite.ps1`
- **Mechanism**: Copies the `idempotency.db` file (from Consumer data folder) to a timestamped backup.
- **Frequency**: Hourly (Recommended).
- **Locations**: `backups/sqlite/`

## Salesforce
We treat Salesforce as an external system.
- **Data Recovery**: Relies on Salesforce's internal availability.
- **Reliability**:
    - **Idempotency**: We ensure message processing handles duplicate events via local SQLite DB.
    - **Dead Letter Queue (DLQ)**: Failed messages are moved to a DLQ for manual inspection/replay.
    - **Retry Policy**: Transient errors trigger automatic retries.

## Storage Locations
- **Local**: `backups/` folder in the project root.
- **Remote**: A copy of the `backups/` folder should be synchronized to private shared storage (e.g., Network Share, S3, or OneDrive) for disaster recovery.

## Scheduling (Windows)
To schedule these scripts automatically:

1.  Open **Task Scheduler**.
2.  **Create Basic Task**.
3.  **Trigger**: "Daily" (for RabbitMQ) or "Hourly" (for SQLite).
4.  **Action**: "Start a Program".
    - **Program/script**: `powershell.exe`
    - **Add arguments**: `-ExecutionPolicy Bypass -File "C:\path\to\project\scripts\backup_rabbitmq.ps1"` (or `backup_sqlite.ps1`)
    - **Start in**: `C:\path\to\project\scripts`
