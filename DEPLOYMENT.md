# Deploying Bugler

The `docker compose up --build -d` in [README.md](README.md) raises a whole world — a PostgreSQL,
a mailpit that swallows every message — and is meant for a laptop. On a real server both of those
already exist and belong to somebody else. This describes that deployment:
[docker-compose.prod.yml](docker-compose.prod.yml) runs **one container**, against the machine's
own PostgreSQL and its own mail relay.

The server never builds Bugler and never holds its source. It runs a published image.

Whoever administers that machine does not need this document — it talks about publishing images and
links into the repository, neither of which is their concern.
[docs/handover-to-server-admin.md](docs/handover-to-server-admin.md) is the standalone version to
send them: the six things Bugler asks of the server, the commands to run, and nothing else. Keep the
two in step when what the server needs changes.

## Where the image comes from

Bugler is published to **`ghcr.io/svaca33/bugler`**, publicly, so **nothing has to sign in to pull
it** — no token on the server, no credential in `~/.docker/config.json`, nothing to rotate. Every
published version is listed under [Releases](https://github.com/Svaca33/Bugler/releases).

```bash
docker pull ghcr.io/svaca33/bugler:0.20.0
```

There is no `latest`, on purpose. A server pins the version it was told to run: rolling back is then
a one-line edit and a restart, and two servers on one tag are running the same thing.

### Publishing a version

A version is `major.minor.patch`. The first two are raised by hand when a change deserves saying so;
the patch is the number of commits made **since that major.minor began**, so every commit is a
version of its own, no two builds can claim the same one, and the count starts again at 0 whenever
a release line does — `0.1.68`, `0.1.69`, then `0.2.0`, `0.2.1`.

Both live in [Directory.Build.props](Directory.Build.props), and only
[scripts/bump-version.ps1](scripts/bump-version.ps1) should write them:

```bash
powershell -File scripts/bump-version.ps1 -Minor    # 0.1.68 -> next commit is 0.2.0
powershell -File scripts/bump-version.ps1 -Major    # 0.1.68 -> next commit is 1.0.0
```

It edits the file and stops — committing and publishing stay yours. It needs a clean tree, because
it counts its own edit as the first commit of the new line; anything else waiting to be committed
would land in the new line while belonging to the old one.

That count cannot be worked out inside the container — `.dockerignore` keeps `.git` out of the build
context, and should — so it is counted outside and passed in. Which is what makes the tag on the
registry and the assemblies inside the image provably the same number, rather than two things
somebody kept in step by hand.

**Releasing is pushing a tag.** Commit the work, then:

```bash
git tag -a v0.20.0 -m "0.20.0"
git push origin v0.20.0
```

[`.github/workflows/release.yml`](.github/workflows/release.yml) does the rest: it builds the image,
pushes it to GHCR and opens the GitHub Release. It authenticates with the `GITHUB_TOKEN` of that
run, so no registry credential is stored anywhere — not in this repository and not on your machine —
and the image is built on a clean runner rather than on whatever a laptop happens to contain.

The workflow refuses a tag that disagrees with the commit it points at: it derives the version from
`Directory.Build.props` and the commit count, and fails if `v…` says something else. The version is
derived, never typed, so the way to release a different number is to tag the commit that number
belongs to. Never move a tag that has already shipped — the server pins it, and going back to it
only means anything while it still holds what it held.

[`scripts/publish-image.ps1`](scripts/publish-image.ps1) still exists for a local build, and can
push with `-Push` if a release ever has to be made by hand. It is not the normal path.

Note what this rules out: the published version cannot be written down anywhere in this repository,
because writing it down is a commit, which makes it the previous version. It belongs in the `.env`
of the server running it, and in whatever you send whoever installs it.

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

### The AI readings, if you want them

Bugler can have a language model write a short reading of what is likely going on as an Episode
opens, and carry it into the alert ([README](README.md#the-reading-beside-the-evidence)). It is off
until configured, and configuring it is normally done **on Administration → Server while Bugler
runs** — the settings are stored in the database and win whole over anything in the environment
until reset there, exactly as the SMTP settings do
([ADR 0027](docs/adr/0027-ai-completions-leave-through-a-shared-transport.md)). Nothing here needs
setting for that, and the API key never has to touch the server's filesystem.

A deployment that configures everything from the environment instead can pass the same facts as
`Ai__Provider`, `Ai__BaseUrl`, `Ai__ApiKey`, `Ai__Model` and `Ai__PatienceSeconds` — the compose file
carries them commented out. Either way, note what the choice of provider means for a self-hosted
server: **Anthropic** sends the opening evidence of an Episode out of the building to a third party,
whereas an **OpenAI-compatible** base URL pointed at your own Ollama or vLLM keeps it on your own
metal. And it is only ever the evidence of an Application whose **AI consent** an Admin has switched
on ([ADR 0028](docs/adr/0028-telemetry-reaches-an-ai-provider-only-by-consent.md)); consent is off by
default and is read at the moment the data would leave, so withdrawing it stops the next disclosure
rather than the one after.

### Upgrade notes

Upgrading **to 0.17** is the first upgrade where pulling the image is not the whole of it: it
changes `docker-compose.prod.yml` itself, so send the new one. Bugler now runs as an unprivileged
user inside its container rather than as root — that half rides along in the image and needs
nothing done — while the `cap_drop` and `security_opt` lines are the compose file's, and a server
still running its old copy quietly keeps every capability the container was ever handed. Nothing
breaks either way, and there is nothing to chown: the container mounts no volume.

Upgrading **to 0.15** signs everyone out once, and only once: on a server reached over HTTPS the
Session cookie changes name to `__Host-bugler.session` (ADR 0019), and cookies under the old name
are simply not sent any more. Signing in again is the whole of it. That same upgrade also makes
`BUGLER_PUBLIC_BASE_URL` mandatory — the compose file now refuses to start without it, rather than
starting with password reset silently switched off.

## What is exposed where

Bugler serves four surfaces on four ports, and [ListenerSurfaces.cs](src/Bugler.Host/ListenerSurfaces.cs)
keeps them apart by the port a connection arrived on — a sender pointed at an OTLP port can never
reach the UI or the REST API, whatever it puts in a Host header.

| Port | Surface | Who should reach it |
| --- | --- | --- |
| 8080 | UI + REST API + OpenAPI | the internal network only, through a reverse proxy that terminates TLS |
| 4318 | OTLP/HTTP | senders — **never unwrapped, whatever fronts it** |
| 4317 | OTLP/gRPC | senders, over a proxy that speaks HTTP/2, or the local network |
| 8081 | MCP — the machine door | nobody at all, unless you mean to: **not published by `docker-compose.prod.yml`**, and shut inside Bugler until an Admin opens it |

**None of the four carries TLS itself.** Kestrel serves all of them as plain HTTP
([appsettings.json](src/Bugler.Host/appsettings.json)), so whatever terminates TLS sits in front —
and that leaves two shapes of deployment.

### One hostname on 443 — the arrangement to reach for

The reverse proxy already terminating TLS for the UI can carry the telemetry too. Senders then need
no port at all, and only 443 is open to the world:

```nginx
location / {
    proxy_pass http://127.0.0.1:8080;          # UI, REST API, OpenAPI
}
location ~ ^/v1/(logs|traces)$ {
    proxy_pass http://127.0.0.1:4318;          # OTLP/HTTP
}
location /opentelemetry.proto.collector. {
    grpc_pass grpc://127.0.0.1:4317;           # OTLP/gRPC — needs `listen 443 ssl http2`
}
```

gRPC reaches its surface this way only if the proxy speaks HTTP/2 to 4317; without `grpc_pass` the
export lands on the app surface and comes back a 404. With this in place the OTLP ports need not be
published outside the host at all — bind them to the loopback in `docker-compose.prod.yml` the way
8080 already is.

The cost is that all three surfaces share one origin, so the isolation the table above describes is
enforced by the proxy's routing rules rather than by the network. Inside the container the port
check still holds — the proxy connects to three distinct ports — but a sender holding the export URL
now also holds the UI's address.

### The machine door, if you want it at all

Port 8081 answers **MCP**, so an agent at somebody's editor can read this server's telemetry
([README](README.md#letting-your-editor-read-the-telemetry)). Nothing about it is on by default, and
it takes three independent yeses before a single record leaves that way: the port has to be
published, an Admin has to open the door on **Administration → Server**, and a person has to hold a
machine delegation they issued themselves. Leave the port out of the compose file and the other two
cannot matter — which is the point of it being a Surface of its own
([ADR 0030](docs/adr/0030-the-machine-door-is-a-surface-of-its-own.md)).

Publishing it is the same job as publishing the UI, with the same rule about TLS: a machine
delegation's secret travels in an `Authorization` header on every call. Under the one-hostname
arrangement it is one more location:

```nginx
location /mcp {
    proxy_pass http://127.0.0.1:8081;          # MCP — only if you mean to expose it
    proxy_http_version 1.1;                    # the transport streams: HTTP/1.0 and
    proxy_buffering off;                       # response buffering would hold the answers
}
```

Then uncomment the `8081` line and `BUGLER_PUBLIC_MCP_URL` in
[docker-compose.prod.yml](docker-compose.prod.yml) and [.env.example](.env.example). That address is
printed into the connect command shown beside a new machine delegation and read nowhere else, so it
decides nothing about security — it only decides whether the line a user copies is the right one.

### Ports published directly

If senders reach 4318 and 4317 as ports instead, TLS has to be arranged for them specifically: a
proxy listening on those ports with the certificate, or Kestrel given one. `https://…:4318` does not
work against the shipped configuration, because nothing there terminates TLS.

### Either way

Two things to get right, because neither fails loudly:

- **TLS in front of the OTLP surfaces is not optional.** A Service API key travels in the
  `Authorization` header of every single export. Without TLS it is readable by anything on the path,
  and an IP allowlist is a filter, not encryption.
- **The Session cookie's protection is read off `BUGLER_PUBLIC_BASE_URL`.** Bugler sees plain HTTP
  behind a TLS proxy, so it cannot tell from a request whether the browser reached it over HTTPS —
  it goes by the address you told it it answers at. Name the **https** address there and the Session
  cookie is minted `Secure` and named `__Host-bugler.session`, which is the browser refusing to send
  it over plain HTTP at all. Name an http address, or leave it out, and it is not: a bookmark, a
  mistyped scheme or an `<img>` on any page anywhere then puts a full sign-in on the wire, and a
  proxy redirecting to HTTPS does not save it — the cookie already left in the request. There is no
  separate switch, and there deliberately is not one: two settings describing the same fact would
  eventually disagree (ADR 0019). Bugler says which of the two it chose in its startup log, and
  warns when the answer is the wrong one.

**Do not have the proxy strip unknown request headers.** Every mutation from the UI carries
`Bugler-Request`, and Bugler refuses one that arrives without it — that header is what stops a page
on another origin from spending a signed-in visitor's Session (ADR 0025). A proxy configured to pass
only an allowlist of headers will remove it, and the symptom is a UI that reads perfectly and cannot
save anything, every write coming back `403`. Reads are unaffected, which is what makes it look like
a Bugler bug rather than a proxy setting.

**Do not have the proxy add a Content-Security-Policy.** Bugler serves the UI under its own, fitted
to what the SPA loads (ADR 0022). A browser given two of them enforces both, and a policy is
enforced as the *intersection* of the two — so a second one that merely looks stricter, or is simply
somebody's default, silently subtracts from Bugler's and the UI breaks in the browser, where nothing
in a log will say why. The same goes for `X-Content-Type-Options` and `Referrer-Policy`, which
Bugler also sends. If your proxy adds security headers by default, exclude Bugler's routes from
that.

An IP allowlist in front of the ingest paths is worth having where the senders are known and few.
Treat it as a second lock: the API key is what actually proves who is exporting.

**Bugler counts guesses, not requests.** Signing in and asking for a reset link are budgeted per
e-mail address, so guessing at an account is bounded no matter where the guessing comes from
(ADR 0021). Nothing is counted per client address, because behind your proxy every request arrives
from the same one and a limit keyed on it would refuse the whole company together. A ceiling on raw
request volume is therefore the proxy's to set, in the same way the certificate and the HTTPS
redirect are.

## First run

Open the UI and create the first account immediately. **Whoever registers first becomes the
administrator**, and until somebody does, the setup page is open to anyone who can reach the server.
The window is short but it is real, so do not deploy and walk away.

Then register an Application, register its Services, issue an API key each, and point the senders
at the server:

```bash
OTEL_EXPORTER_OTLP_ENDPOINT=https://bugler.example.com
OTEL_EXPORTER_OTLP_HEADERS="Authorization=Bearer blgr_…"
```

That is the endpoint for the arrangement above, where the proxy on 443 carries the telemetry: no
port, and the same URL works for gRPC and HTTP alike. Where the OTLP ports are published directly,
name the port and the protocol's own scheme instead — and see that TLS actually terminates there.

## Checking it actually works

- `curl -fsS https://bugler.example.com/health` — this answers for the database behind it, not
  merely for the process, so an `OK` here means Bugler can serve.
- Send a test mail from the admin screen. SMTP failures otherwise surface only in the container log,
  which means a relay that refuses Bugler looks exactly like a quiet week until the first real
  incident goes unannounced.
- Confirm telemetry arrives, then confirm it is still there tomorrow — that is the purge behaving.
- If you configured AI, press *Ask a test question* on the same screen. It asks the saved provider
  outright and prints what it answered, which is the only way to tell a wrong key or an unreachable
  local model from a provider that is merely slow — an alert whose reading never arrives simply
  leaves without one and says nothing about why.
- If you published the machine door, prove the surface on the host first:
  `curl -fsS http://127.0.0.1:8081/health` answers on that listener too — the door sits at `/mcp`
  rather than at the root precisely so that it can. Then, through the proxy, a `POST` to `/mcp` with
  no credential must come back as an authentication refusal:

  ```bash
  curl -s -o /dev/null -w '%{http_code}\n' -X POST \
    -H 'Content-Type: application/json' --data '{}' https://bugler.example.com/mcp
  ```

  `401` is the door answering. HTML or a `200` means the proxy sent it to 8080 instead, and a
  connection error means it never left your proxy.

## Backing it up

What is precious is small; what is enormous is disposable.

| Schema | Size | Losing it costs |
| --- | --- | --- |
| `telemetry` | tens of GB | nothing — it expires within the retention anyway |
| `registry` | kilobytes | re-registering every Service, **re-issuing every API key**, and every Application's AI consent back to off |
| `access` | kilobytes | recreating the accounts and their grants, and re-issuing every machine delegation |
| `alerting` | kilobytes | silence where the alerts used to be, and nobody notices silence — plus the readings written so far, which were never worth keeping |
| `server` | bytes | re-entering the SMTP and AI settings saved from the admin screen, and re-opening the machine door |

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
