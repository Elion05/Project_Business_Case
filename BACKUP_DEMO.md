# Backup Demo Handleiding

Deze handleiding toont hoe je de backup-functionaliteit kunt testen en bewijzen dat alles correct werkt op de Ubuntu VM.

## 📁 Backup Locaties op de VM

| Component | Backup Directory | Bestandsformaat |
|-----------|-----------------|-----------------|
| **SQLite** | `/home/itproj/project_backups/sqlite/` | `idempotency_YYYYmmdd_HHMMSS.db` |
| **RabbitMQ** | `/home/itproj/project_backups/rabbitmq/` | `definitions_YYYYmmdd_HHMMSS.json` |

**Logs**: Cron output wordt geschreven naar `cron.log` in elke backup directory.

**Retention**: Automatisch bewaren van de **5 nieuwste** backups per type. Oudere backups worden verwijderd.

---

## 🔍 Demo Commando's

### A) Toon laatste 5 RabbitMQ backups

```bash
ls -lht /home/itproj/project_backups/rabbitmq/ | head -6
```

**Verwacht resultaat**: Lijst met max 5 `definitions_*.json` bestanden + 1 `cron.log` (totaal 6 regels + header).

**Voorbeeld output**:
```
total 124K
-rw-rw-r-- 1 itproj itproj  28K Jan 21 10:15 definitions_20260121_101530.json
-rw-rw-r-- 1 itproj itproj  28K Jan 21 09:15 definitions_20260121_091530.json
-rw-rw-r-- 1 itproj itproj  28K Jan 21 08:15 definitions_20260121_081530.json
-rw-rw-r-- 1 itproj itproj  28K Jan 21 07:15 definitions_20260121_071530.json
-rw-rw-r-- 1 itproj itproj  28K Jan 21 06:15 definitions_20260121_061530.json
-rw-rw-r-- 1 itproj itproj 1.2K Jan 21 10:15 cron.log
```

---

### B) Toon laatste 5 SQLite backups

```bash
ls -lht /home/itproj/project_backups/sqlite/ | head -6
```

**Verwacht resultaat**: Lijst met max 5 `idempotency_*.db` bestanden + 1 `cron.log`.

**Voorbeeld output**:
```
total 240K
-rw-r--r-- 1 itproj itproj  40K Jan 21 11:00 idempotency_20260121_110000.db
-rw-r--r-- 1 itproj itproj  40K Jan 21 10:00 idempotency_20260121_100000.db
-rw-r--r-- 1 itproj itproj  40K Jan 21 09:00 idempotency_20260121_090000.db
-rw-r--r-- 1 itproj itproj  40K Jan 21 08:00 idempotency_20260121_080000.db
-rw-r--r-- 1 itproj itproj  40K Jan 21 07:00 idempotency_20260121_070000.db
-rw-rw-r-- 1 itproj itproj 2.1K Jan 21 11:00 cron.log
```

---

### C) Bewijs: Queue is durable + heeft DLQ arguments

```bash
sudo rabbitmqctl list_queues name durable arguments
```

**Verwacht resultaat**: Toont dat `BestelAppQueue` durable is en de juiste DLQ arguments heeft.

**Voorbeeld output**:
```
Timeout: 60.0 seconds ...
Listing queues for vhost / ...
name                   durable  arguments
BestelAppQueue         true     [{"x-dead-letter-exchange",""},{"x-dead-letter-routing-key","BestelAppQueue_DLQ"}]
BestelAppQueue_DLQ     true     []
```

**Bewijs**:
- ✅ `BestelAppQueue` heeft `durable = true` → Overleeft RabbitMQ restart
- ✅ `x-dead-letter-routing-key` verwijst naar `BestelAppQueue_DLQ`
- ✅ Dead Letter Queue (DLQ) bestaat en is ook durable

---

### D) Bewijs: Message persistence (delivery_mode=2)

**Stap 1**: Publish een persistent message naar de queue

```bash
sudo rabbitmqadmin publish routing_key="BestelAppQueue" \
  payload="Test persistent message" \
  properties='{"delivery_mode":2}'
```

> **Note**: `delivery_mode=2` betekent **persistent** (wordt naar disk geschreven).

**Stap 2**: Controleer dat message in queue staat

```bash
sudo rabbitmqctl list_queues name messages
```

**Verwacht**: `BestelAppQueue` heeft 1+ message(s).

**Stap 3**: Restart RabbitMQ server

```bash
sudo systemctl restart rabbitmq-server
```

**Wacht ~5 seconden** tot RabbitMQ volledig opgestart is.

**Stap 4**: Controleer opnieuw of message nog in queue staat

```bash
sudo rabbitmqctl list_queues name messages
```

**Verwacht resultaat**: Message is **nog steeds aanwezig** in `BestelAppQueue`.

**Bewijs**: 
- ✅ Message overleeft RabbitMQ restart → **persistence werkt**
- ✅ Combinatie van durable queue + persistent messages = **geen dataverlies bij restart**

---

## 🔁 Idempotency Bewijs (SQLite)

Het systeem gebruikt de `message_state` tabel met `MessageId` als **PRIMARY KEY** om duplicate messages te voorkomen.

### Query 1: Bewijs dat MessageId uniek is (geen duplicates)

```bash
sqlite3 /home/data/idempotency.db "
SELECT MessageId, COUNT(*) as Occurrences
FROM message_state
GROUP BY MessageId
HAVING COUNT(*) > 1;
"
```

**Verwacht resultaat**: **Geen output** → PRIMARY KEY garandeert dat elke MessageId max 1x voorkomt.

---

### Query 2: Toon RetryCount progression

```bash
sqlite3 /home/data/idempotency.db "
SELECT MessageId, RetryCount, Status, CreatedAt
FROM message_state
ORDER BY UpdatedAt DESC
LIMIT 10;
"
```

**Voorbeeld output**:
```
DEMO-IDEMP-1|2|Failed|2026-01-20 12:56:42
DEMO-PERSIST-1|2|Failed|2026-01-20 12:35:22
21fa9b89-...|1|Failed|2026-01-20 12:22:07
8f0e0265-...|1|Failed|2026-01-20 12:17:00
```

---

### Query 3: Statistieken per status

```bash
sqlite3 /home/data/idempotency.db "
SELECT Status, COUNT(*) as Count, AVG(RetryCount) as AvgRetries
FROM message_state
GROUP BY Status;
"
```

**Voorbeeld output**:
```
Failed|7|1.43
Success|15|0.0
```

**Bewijs**:
- ✅ `MessageId` is **PRIMARY KEY** → Database blokkeert duplicates automatisch
- ✅ `RetryCount` kan oplopen (0 → 1 → 2 bij retries)
- ✅ **Idempotency gegarandeerd**: Duplicate messages zijn onmogelijk door PRIMARY KEY constraint

---

## 🧪 Handmatige Backup Test

Test de backup scripts direct (zonder te wachten op cron):

```bash
# SQLite backup
/home/itproj/backup_sqlite.sh

# RabbitMQ backup
/home/itproj/backup_rabbitmq.sh
```

Check output voor `[OK]` berichten en verifieer dat nieuwe bestanden zijn aangemaakt:

```bash
ls -lh /home/itproj/project_backups/sqlite/
ls -lh /home/itproj/project_backups/rabbitmq/
```

---

## 📋 Cron Schedule Verificatie

```bash
crontab -l | grep backup
```

**Verwacht output**:
```
0 * * * * /home/itproj/backup_sqlite.sh >> /home/itproj/project_backups/sqlite/cron.log 2>&1
0 2 * * * /home/itproj/backup_rabbitmq.sh >> /home/itproj/project_backups/rabbitmq/cron.log 2>&1
```

**Betekenis**:
- SQLite: Elk uur op minuut 0 (00:00, 01:00, 02:00, ...)
- RabbitMQ: Dagelijks om 02:00

---

## 📊 Retention Policy Verificatie

Run een backup script **6+ keer** om retention te testen:

```bash
# Test retention (voer 7x uit met korte pauze)
for i in {1..7}; do /home/itproj/backup_sqlite.sh; sleep 2; done

# Tel aantal backup bestanden (exclusief cron.log)
ls /home/itproj/project_backups/sqlite/idempotency_*.db | wc -l
```

**Verwacht resultaat**: Precies **5** backup bestanden (oudste zijn verwijderd).

Check de `cron.log` voor retention berichten:

```bash
tail -20 /home/itproj/project_backups/sqlite/cron.log
```

Je zou moeten zien:
```
[2026-01-21 11:05:23] Retention: kept newest 5 backups, deleted 2 old backup(s)
```

---

## ✅ Complete Verificatie Checklist

- [ ] **A**: Laatste 5 RabbitMQ backups tonen
- [ ] **B**: Laatste 5 SQLite backups tonen
- [ ] **C**: Queue durable + DLQ arguments bewijzen
- [ ] **D**: Message persistence na RabbitMQ restart bewijzen
- [ ] **Idempotency**: MessageId is uniek in database
- [ ] **Retention**: Max 5 backups bewaard, oudere verwijderd
- [ ] **Cron**: Scheduled jobs correct geconfigureerd

**Success criteria**: Alle checklist items ✅ = Backup systeem volledig functioneel!
