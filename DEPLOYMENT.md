# Deploying Bugler

The `docker compose up --build -d` in [README.md](README.md) raises a whole world — a PostgreSQL,
a mailpit that swallows every message — and is meant for a laptop. On a real server both of those
already exist and belong to somebody else. This describes that deployment:
[docker-compose.prod.yml](docker-compose.prod.yml) runs **one container**, against the machine's
own PostgreSQL and its own mail relay.

The server never builds Bugler and never holds its source. It runs a published image.

## Where the image comes from

Bugler is published to **`svaca33/bugler`**, a private Docker Hub repository, so the machine has to
sign in once before it can pull:

```bash
docker login -u svaca33
```

Use a **read-only access token**, never the account password. Docker Hub issues them separately and
either side can revoke one without disturbing anything else — which matters, because `docker login`
leaves the credential in `~/.docker/config.json` base64-encoded rather than encrypted. Read-only is
what makes that acceptable: the token can fetch Bugler and do nothing else.

Note where this leaves the deployment: it depends on a personal Docker Hub account. Moving the image
to a registry the company owns, or making it public if Bugler is ever open-sourced, removes both the
token and that dependency.

### Publishing a version

Bugler's version is written in one place: `<Version>` in
[Directory.Build.props](Directory.Build.props). Every assembly is stamped from it, and the publish
script reads it — so the tag is never typed by hand, and an image cannot claim a version different
from the build inside it.

```bash
powershell -File scripts/publish-image.ps1 -Repository svaca33/bugler
```

That builds and tags, then stops and prints the push. Pushing needs credentials and puts the image
somewhere outside the machine, so it stays a deliberate second step — `-Push` does it once
`docker login` has been done with a token that may write.

Raise `<Version>` before each release, and never push over a tag that has already shipped: the
server pins its tag, and going back to it only means anything while it still holds what it held.

## What to ask of whoever administers the database

Bugler needs its own database and role, and none of this is something Bugler can do for itself.

```sql
CREATE ROLE bugler LOGIN PASSWORD '…';
CREATE DATABASE bugler OWNER bugler;
```

Four things worth settling at the same time, because each is far cheaper now than later:

**A volume of its own.** Bugler is about to become the largest and least predictable writer on that
cluster, and a full disk stops writes for *every* database on it, not only this one. A tablespace on
its own mount point contains that: when the volume fills, Bugler stops and nothing else notices.

```sql
CREATE TABLESPACE bugler_ts LOCATION '/mnt/bugler';
CREATE DATABASE bugler OWNER bugler TABLESPACE bugler_ts;
```

**Room.** Roughly **1 kB per log record**, all in. Multiply by the records a day and by the
retention — at eleven million a day and seven days that is about 73 GB, and the disk wants headroom
above the steady state rather than exactly it.

**The backup, and what it must leave out.** The telemetry schema is the enormous part and the
worthless one: it expires within the retention anyway. Everything irreplaceable is a few hundred
kilobytes. If the cluster is backed up whole (`pg_basebackup`, PITR, a snapshot), the telemetry
rides along into every nightly backup and its whole rotation — **check which kind runs here before
turning Bugler on.** A per-database dump can simply leave it out:

```bash
pg_dump -U bugler -d bugler --exclude-schema=telemetry -Fc > bugler.dump
```

**Reachability from the container.** `pg_hba.conf` has to admit connections from the docker bridge
network, or the very first migration fails.

## Configuring it

Copy [.env.example](.env.example) to `.env` beside `docker-compose.prod.yml` and fill it in. Nothing
secret belongs in the compose file itself, and `.env` is not committed.

```bash
cp .env.example .env
```

`BUGLER_IMAGE` is the version to run — pin it, never `latest`. Then:

```bash
docker compose -f docker-compose.prod.yml pull
docker compose -f docker-compose.prod.yml up -d
```

Only three files need to reach the server: `docker-compose.prod.yml`, the `.env` built from
`.env.example`, and this document. Everything else arrives in the image.

## What is exposed where

Bugler serves three surfaces on three ports, and [ListenerSurfaces.cs](src/Bugler.Host/ListenerSurfaces.cs)
keeps them apart by the port a connection arrived on — a sender pointed at an OTLP port can never
reach the UI or the REST API, whatever it puts in a Host header.

| Port | Surface | Who should reach it |
| --- | --- | --- |
| 8080 | UI + REST API + OpenAPI | the internal network only, through a reverse proxy that terminates TLS |
| 4318 | OTLP/HTTP | senders, including those outside the network — **behind TLS** |
| 4317 | OTLP/gRPC | senders on the local network |

Two things to get right, because neither fails loudly:

- **TLS on 4318 is not optional.** A Service API key travels in the `Authorization` header of every
  single export. Without TLS it is readable by anything on the path, and an IP allowlist is a filter,
  not encryption.
- **The session cookie needs the proxy to be honest.** Bugler sees plain HTTP behind a TLS proxy, so
  set `Cookie.SecurePolicy` accordingly if the deployment ever serves the UI over anything but the
  internal network.

An IP allowlist in front of 4318 is worth having where the senders are known and few. Treat it as a
second lock: the API key is what actually proves who is exporting.

## First run

Open the UI and create the first account immediately. **Whoever registers first becomes the
administrator**, and until somebody does, the setup page is open to anyone who can reach the server.
The window is short but it is real, so do not deploy and walk away.

Then register an Application, register its Services, issue an API key each, and point the senders
at the server:

```bash
OTEL_EXPORTER_OTLP_ENDPOINT=https://bugler.example.com:4318
OTEL_EXPORTER_OTLP_HEADERS="Authorization=Bearer blgr_…"
```

## Checking it actually works

- `curl -fsS https://bugler.example.com/health` — this answers for the database behind it, not
  merely for the process, so an `OK` here means Bugler can serve.
- Send a test mail from the admin screen. SMTP failures otherwise surface only in the container log,
  which means a relay that refuses Bugler looks exactly like a quiet week until the first real
  incident goes unannounced.
- Confirm telemetry arrives, then confirm it is still there tomorrow — that is the purge behaving.

## Backing it up

What is precious is small; what is enormous is disposable.

| Schema | Size | Losing it costs |
| --- | --- | --- |
| `telemetry` | tens of GB | nothing — it expires within the retention anyway |
| `registry` | kilobytes | re-registering every Service **and re-issuing every API key** |
| `access` | kilobytes | recreating the accounts and their grants |
| `alerting` | kilobytes | silence where the alerts used to be, and nobody notices silence |

API keys are stored only as SHA-256 hashes, so losing `registry` cannot be undone by reading a
backup of it out of some other machine — the plaintext exists nowhere but in the configuration of
the senders themselves. Nightly is plenty:

```bash
pg_dump -U bugler -d bugler --schema=access --schema=registry --schema=alerting -Fc > bugler-config.dump
```

Each schema carries its own `__ef_migrations_history`, so that dump restores as a consistent whole.

**Rehearse the restore once, before Bugler is carrying anything.** Restore into an empty database,
point a Bugler at it, sign in. An untested backup is always broken; the only question is when you
find out.
