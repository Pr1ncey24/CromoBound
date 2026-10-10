# CromoBound: Deploying the Server

The server runs on one small Linux VPS with Docker Compose: the app in one container and Caddy in front of it for HTTPS. The SQLite
file and the keys that encrypt session cookies live on named volumes, so they survive redeploys. Design: `docs/server.md`.

## 1. What you need
- A Linux VPS (1 vCPU and 1 GB of RAM are plenty) with Docker Engine and the Compose plugin.
- A domain name whose A (and AAAA) record points at the VPS. Pick a name that says nothing about the site: HTTPS certificates are
  listed in public Certificate Transparency logs, so the host name is public even though nothing behind the login is.
- Ports 80 and 443 open, plus SSH for administration, and nothing else.

## 2. Build the image
On the VPS, in a clone of the repository at the commit to deploy:

```bash
git pull
docker build --build-arg SOURCE_REVISION=$(git rev-parse HEAD) -t cromobound:latest .
```

The commit becomes part of the engine version that every saved match records, and the build refuses to run without it. A match saved
by one build can't be replayed by another, so deploy with maintenance on (section 6).

## 3. Compose file and Caddyfile
`/opt/cromobound/compose.yaml`:

```yaml
name: cromobound

services:
  app:
    image: cromobound:latest
    restart: unless-stopped
    environment:
      AllowedHosts: play.example.com
      CromoBound__KnownNetworks__0: 172.30.0.0/24
      # First start only: remove both lines once the admin account exists (section 5).
      CROMOBOUND_ADMIN_USER: owner
      CROMOBOUND_ADMIN_PASSWORD: a-long-first-password
    volumes:
      - db:/var/lib/cromobound/db
      - keys:/var/lib/cromobound/keys
      - ./card-images:/var/lib/cromobound/images:ro
    networks: [web]

  caddy:
    image: caddy:2
    restart: unless-stopped
    ports:
      - "80:80"
      - "443:443"
      - "443:443/udp"
    volumes:
      - ./Caddyfile:/etc/caddy/Caddyfile:ro
      - caddy-data:/data
      - caddy-config:/config
    networks: [web]

networks:
  web:
    ipam:
      config:
        - subnet: 172.30.0.0/24

volumes:
  db:
  keys:
  caddy-data:
  caddy-config:
```

`/opt/cromobound/Caddyfile`:

```
play.example.com {
    encode zstd gzip
    header -Server
    reverse_proxy app:8080
}
```

Replace `play.example.com` in both files with the domain. Caddy gets the certificate, redirects HTTP to HTTPS, passes WebSockets
through, and sends the client's address in `X-Forwarded-For`.

`CromoBound__KnownNetworks__0` must be the `web` network's subnet. The server trusts forwarded headers only from there. Without it,
the server takes Caddy for the client of every request, so every login would share one rate limit. The app service has no `ports:`, so Kestrel's port 8080 is reachable only from Caddy.

## 4. Settings
Environment variables on the `app` service. List settings take `__0`, `__1` and so on.

| Variable | Set by the image | Meaning |
|---|---|---|
| `CromoBound__DatabasePath` | `/var/lib/cromobound/db/cromobound.db` | The SQLite file, on the `db` volume |
| `CromoBound__KeysFolder` | `/var/lib/cromobound/keys` | The keys that encrypt session cookies, on the `keys` volume. Losing them signs everyone out |
| `CromoBound__DataFolder` | `/app/data` | The card data, built into the image |
| `CromoBound__CardImagesPath` | `/var/lib/cromobound/images` | The card images, mounted read-only from `/opt/cromobound/card-images` (section 5) |
| `CromoBound__KnownNetworks__0` | (none) | The proxy's network in CIDR form (section 3) |
| `CromoBound__KnownProxies__0` | (none) | Or a proxy's fixed address |
| `CromoBound__LoginRequestsPerMinute` | 10 | Login requests per client address per minute |
| `CromoBound__LockoutFailures`, `CromoBound__LockoutMinutes` | 5, 15 | Failed sign-ins that lock a username, and for how long |
| `CromoBound__CookieHours` | 12 | Sliding session lifetime |
| `CROMOBOUND_ADMIN_USER`, `CROMOBOUND_ADMIN_PASSWORD` | (none) | The first admin, used only while there are no users |
| `AllowedHosts` | `*` | The domain, so requests for other host names are refused |
| `ASPNETCORE_ENVIRONMENT` | `Production` | Never `Development`, which shows error details |

A setting that can't be used (a number below 1, an address or network that can't be read, a card data folder that can't be loaded)
stops the server, and the log names it.

## 5. First start

Fill the card images folder once, from the repository clone. The importer runs in a throwaway SDK container and downloads every
printing's image (about 1450 files) into `/opt/cromobound/card-images`:

```bash
mkdir -p /opt/cromobound/card-images
docker run --rm -v "$PWD":/src -v /opt/cromobound/card-images:/out -w /src mcr.microsoft.com/dotnet/sdk:10.0 \
  dotnet run --project tools/CromoBound.Importer -- images /out
```

The command can be run again at any time: it fetches only the images that are missing, and says which ones failed. A card whose
image is missing still shows on the board, as a placeholder with its name.

```bash
cd /opt/cromobound
docker compose up -d
docker compose logs app
```

The log says "Created the first admin account owner." Then remove the two `CROMOBOUND_ADMIN_*` lines from `compose.yaml` and run
`docker compose up -d` again.

Accounts are made with the admin API. The UI comes in Phase 4; until then, use curl with a cookie jar:

```bash
curl -c jar -H 'Content-Type: application/json' -d '{"userName":"owner","password":"a-long-first-password"}' https://play.example.com/login
curl -b jar -H 'Content-Type: application/json' -d '{"userName":"friend1","password":"a-long-password-1","isAdmin":false}' https://play.example.com/api/admin/users
curl -b jar https://play.example.com/api/admin/users
```

Passwords have at least 12 characters, and usernames 3 to 24 letters, digits, `_` or `-`.

## 6. Deploying a new version
1. Turn maintenance on, so no new challenges or matches start:
   `curl -b jar -H 'Content-Type: application/json' -d '{"on":true}' https://play.example.com/api/admin/maintenance`
2. Wait until `runningMatches` is 0: `curl -b jar https://play.example.com/api/admin/maintenance`
3. Build the new image (section 2), then run `docker compose up -d app`.
4. If the new version adds cards (a new set), fill the card images again with the command in section 5. The app doesn't need a
   restart for new images.

What a restart does:
- Maintenance is off again, since the switch lives in memory.
- A match still running when a new build starts is abandoned: every new commit changes the engine version. Its players are told the next time they connect.
- Open challenges and login lockouts are cleared, the admin's lockout included.
- Sessions survive, because the keys are on a volume.

## 7. Backups
An online backup of the SQLite file, from a throwaway container:

```bash
mkdir -p /opt/cromobound/backups
docker run --rm -v cromobound_db:/db -v /opt/cromobound/backups:/backup alpine:3.20 \
  sh -c 'apk add --no-cache sqlite >/dev/null && sqlite3 /db/cromobound.db ".backup /backup/cromobound-$(date +%F).db"'
```

To run it daily, put the command in a script, `/opt/cromobound/backup.sh`, and call that from cron. (In a crontab line, `%` has to be
written `\%`, so a script is simpler.)

```bash
#!/bin/sh
mkdir -p /opt/cromobound/backups
docker run --rm -v cromobound_db:/db -v /opt/cromobound/backups:/backup alpine:3.20 \
  sh -c 'apk add --no-cache sqlite >/dev/null && sqlite3 /db/cromobound.db ".backup /backup/cromobound-$(date +%F).db"'
```

```
0 3 * * * /bin/sh /opt/cromobound/backup.sh
```

Copy `/opt/cromobound/backups` off the VPS as well.

To restore a backup, the database runs in WAL mode, so the old `-wal` and `-shm` files must go, and the app runs as a non-root user
(`1654`, the image's `$APP_UID`), so the restored file must be handed to it:
1. Stop the app with `docker compose stop app`.
2. In one throwaway container with both volumes, delete the stale `-wal` and `-shm` files, copy the backup over `cromobound.db` and
   give the app's user the file (replace the date with the backup to restore):

   ```bash
   docker run --rm -v cromobound_db:/db -v /opt/cromobound/backups:/backup alpine:3.20 \
     sh -c 'rm -f /db/cromobound.db-wal /db/cromobound.db-shm \
       && cp /backup/cromobound-2026-01-31.db /db/cromobound.db \
       && chown 1654:1654 /db/cromobound.db'
   ```
3. Start the app again with `docker compose start app`.

The `keys` volume doesn't need a backup: losing it only signs everyone out.

## 8. Checklist
- [ ] `CromoBound__KnownNetworks__0` (or `KnownProxies`) names Caddy's network.
- [ ] The `db` and `keys` volumes are mounted.
- [ ] `/opt/cromobound/card-images` is filled and mounted read-only.
- [ ] `CROMOBOUND_ADMIN_PASSWORD` is removed after the first start.
- [ ] `ASPNETCORE_ENVIRONMENT` is `Production`.
- [ ] `Microsoft.AspNetCore.Authorization` logging is never set below `Warning`: lower levels write role identifiers to the log.
- [ ] The app service publishes no ports; only Caddy publishes 80 and 443.
- [ ] `AllowedHosts` is the domain.
- [ ] Backups run, and are copied off the VPS.

The log shows a data protection warning at every start ("No XML encryptor configured"). That is expected: the keys are protected by
the volume's permissions.
