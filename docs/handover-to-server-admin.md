# Bugler — what it needs from the server

Bugler is a self-hosted telemetry server: applications send it their logs and traces, and the team
reads them in a web UI. It runs as **a single container** and leans on two things this machine
already has — **PostgreSQL** and a **mail relay**.

This document is the whole of what it asks for. Everything else is done by whoever owns Bugler.

You will receive three things separately: `docker-compose.prod.yml`, a `.env` to complete, and a
Docker Hub access token.

---

## 1. A database and a role

```sql
CREATE ROLE bugler LOGIN PASSWORD '…';
CREATE DATABASE bugler OWNER bugler;
```

Bugler creates and migrates its own schemas on first start. It needs no rights outside its own
database.

## 2. A volume of its own — the one worth arguing about

Bugler is about to become the largest and least predictable writer on this cluster. A full disk
stops writes for **every** database on it, not only for Bugler. Putting it on its own tablespace
contains that: when the volume fills, Bugler stops and nothing else notices.

```sql
CREATE TABLESPACE bugler_ts LOCATION '/mnt/bugler';
CREATE DATABASE bugler OWNER bugler TABLESPACE bugler_ts;
```

If a separate volume is genuinely not possible, say so — the retention can be shortened to trade
history for space, but then Bugler's configuration is also load-bearing for your other databases,
and somebody should be watching free space with an alert.

## 3. Room

About **1 kB per log record** stored, measured, indexes included. At the expected volume — some
eleven million records a day, kept seven days — that is roughly **73 GB in steady state**.
**Ask for 150–200 GB**: a table wants headroom above its steady state, not exactly it.

## 4. Backup — and what to keep out of it

Bugler's data splits sharply in two:

| Schema | Size | If it is lost |
| --- | --- | --- |
| `telemetry` | tens of GB | nothing. It expires within days anyway |
| `registry`, `access`, `alerting` | kilobytes | every sending application has to be re-registered and re-keyed by hand |

So the enormous part is the worthless one. **Which kind of backup runs here matters:**

- **Per-database `pg_dump`** — add `--exclude-schema=telemetry` and the backup stays under a
  megabyte.
- **Whole-cluster** (`pg_basebackup`, PITR, storage snapshot) — the telemetry cannot be excluded,
  so tens of GB of week-old logs join every nightly backup and its entire rotation. Worth knowing
  before Bugler is switched on rather than after the backup target fills up.

Please say which one applies.

## 5. Reachability from the container

`pg_hba.conf` must admit connections from the docker bridge network. Without it the very first
start fails while migrating, before Bugler serves anything.

## 6. Mail

Bugler sends alerts when an application starts failing, and password-reset links. It needs:

- an SMTP relay it may send through from this host, and
- a **From address** it is allowed to use.

STARTTLS is used when the relay offers it. Authentication is supported but not required.

---

## Network exposure

Bugler serves three ports, and they are genuinely separate surfaces — an application pointed at a
telemetry port cannot reach the UI or the API, whatever it sends.

| Port | What it is | Who should reach it |
| --- | --- | --- |
| 8080 | web UI and REST API | **internal network only**, through a reverse proxy terminating TLS |
| 4318 | telemetry ingest (HTTP) | sending applications, including ones outside the network — **must be behind TLS** |
| 4317 | telemetry ingest (gRPC) | sending applications on the local network |

**TLS on 4318 is not optional.** Every export carries an API key in an `Authorization` header, so
without TLS that key is readable by anything on the path. An IP allowlist in front of it is welcome
as a second layer, but it is a filter, not encryption.

By default the compose file binds 8080 to `127.0.0.1`, so the only way in is a reverse proxy on this
same machine. If the proxy runs elsewhere, set `BUGLER_APP_BIND=0.0.0.0` in `.env` and let the
firewall decide who reaches it.

---

## Running it

The image is published to a private Docker Hub repository, so the machine signs in once:

```bash
docker login -u svaca33
```

Use the access token you were sent, not a password. Its scope is **Read only**, which is what
reaches a private repository — note that Docker Hub also offers a scope called *Public Repo
Read-only*, which does not, and whose failure looks like a mistyped image name.

Then, in the directory holding `docker-compose.prod.yml` and `.env`:

```bash
docker compose -f docker-compose.prod.yml pull
docker compose -f docker-compose.prod.yml up -d
```

Bugler migrates its own schemas at startup, which takes a few seconds on an empty database.

## Checking it worked

```bash
docker ps                       # the container should report (healthy)
curl -fsS http://localhost:8080/health
```

`/health` answers for the database behind it, not merely for the process — an `OK` there means
Bugler can actually serve. If the container is unhealthy, `docker compose -f docker-compose.prod.yml
logs --tail=50 bugler` says why; the usual first cause is `pg_hba.conf`.

Please **do not open the UI yourself** beyond confirming it loads: the first account created on a
fresh Bugler becomes its administrator, and that account belongs to the owner.

---

## What to hand back

- The database password — or, better, put it straight into `.env` as `BUGLER_DB_PASSWORD` and never
  send it anywhere.
- Which kind of backup runs on this cluster, and whether the telemetry schema is excluded.
- Whether Bugler got its own volume, and how much space it has.
- The URL the reverse proxy serves, so links inside Bugler's mail point at the right place.
