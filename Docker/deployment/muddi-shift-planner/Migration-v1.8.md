# Migration from v1.7.x to v1.8.x

v1.8 changes how per-instance configuration is delivered: instead of a mounted client
`appsettings.json` and placeholder values in `docker-compose.yml`, all instance-specific
values (Keycloak URL, realm name, API/client URLs, CORS origin) come from environment variables.

**What is NOT affected:**

- No database migration — the API runs its EF migrations on startup as usual.
- No Keycloak changes — existing realms keep working as-is.
- No browser cache issues — the service worker does not cache `appsettings.json` (already true in v1.7),
  so the re-rendered client config takes effect immediately.
- Keycloak realm setup (Readme step 4) only needs to be repeated for a fresh deployment.

**The breaking changes:**

1. The client container no longer serves a mounted `appsettings.json`. It renders one from env vars on
   every container start. **Without the env vars the container fails fast** (by design) — set them in the
   same change as the image bump.
2. The API image no longer contains a CORS origin — `Cors__Origins__0` must be set, otherwise the API
   refuses to start with `You have to set Cors__Origins array in settings`.
3. In the shipped compose/`.env`, the `MUDDI_*` env vars are renamed to `SHIFT_PLANNER_*` and new
   URL/realm vars are added.
4. `config/.../appsettings.json` is no longer mounted — delete the file.
5. `MuddiConnect__Authority` must match how your users' browsers reach Keycloak. The shipped compose
   sets it to the container-internal URL `http://keycloak:8080/realms/<realm>` (works for local
   testing). In production, the browser fetches tokens from your public Keycloak URL and Keycloak
   stamps that URL as the token issuer (`iss`) — so set it in `docker-compose.yml` to your public
   Keycloak URL (e.g. `${SHIFT_PLANNER_KEYCLOAK_URL}/realms/${SHIFT_PLANNER_KEYCLOAK_REALM}`),
   otherwise the API rejects user tokens. The API also fetches its service account token and the
   OIDC metadata from that URL, so the server must be able to reach its public Keycloak URL.

---

## Case A: You use the shipped `docker-compose.yml` + `.env`

1. Replace `docker-compose.yml` and `.env` with the v1.8 versions from the release.
2. Fill in `.env`:
   - keep your postgres/keycloak passwords and client secret,
   - `SHIFT_PLANNER_CLIENT_URL` — your client URL (what was the `Cors__Origins__0` placeholder),
   - `SHIFT_PLANNER_API_URL` — your public API URL,
   - `SHIFT_PLANNER_KEYCLOAK_URL` — your public Keycloak URL,
   - `SHIFT_PLANNER_KEYCLOAK_REALM` — your realm name (default `muddi`).
3. In `docker-compose.yml`, set `MuddiConnect__Authority` to your public Keycloak URL
   (e.g. `${SHIFT_PLANNER_KEYCLOAK_URL}/realms/${SHIFT_PLANNER_KEYCLOAK_REALM}`) — see breaking change 5.
4. Delete `config/muddi/appsettings.json`.
5. `docker compose up -d`.
6. Verify (see checklist below).

## Case B: You use a customized `docker-compose.yml`

(e.g. your own reverse proxy / traefik, own service names, `ConnectionStrings__ShiftPlannerDb`)

1. Bump the image tags: `api:v1.7` → `api:v1.8`, `client:v1.7` → `client:v1.8`.
2. **Client service** — the breaking part:
   - remove the `appsettings.json` volume mount, if you have one,
   - add the env vars (public, browser-reachable URLs):
     ```
     SHIFT_PLANNER_KEYCLOAK_URL=https://connect.example.com
     SHIFT_PLANNER_KEYCLOAK_REALM=muddi
     SHIFT_PLANNER_API_URL=https://api.example.com
     ```
3. **API service** — mostly unchanged:
   - keep `Cors__Origins__0` (now required, see above),
   - keep `ConnectionStrings__ShiftPlannerDb` — still supported in v1.8 (it takes precedence over the
     `Database__*` vars),
   - keep `MuddiConnect__Authority` pointing at your **public** Keycloak URL — required in production
     (see the token-issuer note in the Readme),
   - `MuddiConnect__ClientSecret` / `MuddiConnect__ClientId` — unchanged.
4. **Keycloak service** — no changes required. Recommended (also shipped in v1.8):
   - `KC_DB_URL_HOST` must match your postgres **service name** (the shipped v1.7 compose had
     `KC_DB_URL_HOST: postgresql` while the service was named `postgres` — fixed in v1.8; if you
     worked around it with a `postgresql` service name, keep it consistent),
   - `command: start` (not `start --optimized`, which fails on a first-ever start) plus a persistent
     `keycloak_data` volume at `/opt/keycloak/data` — avoids the full optimized rebuild on every
     container recreation.
5. **Postgres service** — no changes required. Optional: add the v1.8 healthcheck and
   `condition: service_healthy` so the API/Keycloak wait for postgres instead of crash-looping briefly.
   The `MUDDI_*` → `SHIFT_PLANNER_*` rename in the postgres env vars is cosmetic on an existing
   deployment — the initdb scripts only run on a first-ever volume.
6. `docker compose up -d` — the api/client containers are recreated with the new images (a short
   blip while they restart). No data migration.

---

## Example: customized deployment (traefik) migrated

```yaml
shift-planner-client:
    restart: unless-stopped
    image: ghcr.io/muddi-markt/shiftplanner/client:v1.8        # was v1.7
    networks: [proxy-tier]
    environment:
      - VIRTUAL_HOST=client.example.com
      - LETSENCRYPT_HOST=client.example.com
      - LETSENCRYPT_EMAIL=cert@example.com
      # NEW in v1.8 - the client renders appsettings.json from these
      - SHIFT_PLANNER_KEYCLOAK_URL=https://connect.example.com
      - SHIFT_PLANNER_KEYCLOAK_REALM=muddi
      - SHIFT_PLANNER_API_URL=https://api.example.com
    volumes:
      - ./config/shift-planner/customization:/usr/share/nginx/html/customization
      # REMOVED in v1.8: - ./config/shift-planner/appsettings.json:/usr/share/nginx/html/appsettings.json

shift-planner-api:
    restart: unless-stopped
    image: ghcr.io/muddi-markt/shiftplanner/api:v1.8           # was v1.7
    # everything below is UNCHANGED
    environment:
      - VIRTUAL_HOST=api.example.com
      - VIRTUAL_PORT=8080
      - LETSENCRYPT_HOST=api.example.com
      - LETSENCRYPT_EMAIL=cert@example.com
      - MuddiConnect__ClientSecret=REDACTED
      - MuddiConnect__Authority=https://connect.example.com/realms/muddi
      - ConnectionStrings__ShiftPlannerDb=Server=postgresql;Port=5432;Database=shift_planner;User Id=myuser;Password=mypassword
      - Cors__Origins__0=https://client.example.com
    depends_on: [postgresql]
```

Keycloak and postgres stay as they were (plus the optional keycloak/postgres improvements from step 4/5).

---

## Verify

1. `curl https://client.example.com/appsettings.json` → shows your Keycloak URL, realm and API URL
   (this file is now rendered from env vars, not mounted).
2. `docker logs <api container>` → no `You have to set Cors__Origins array in settings` error.
3. `docker logs <client container>` → shows `rendered /usr/share/nginx/html/appsettings.json`
   (if the env vars are missing, it fails fast with which variable is unset).
4. Log in with an existing user → shifts load (if the API rejects the token, `MuddiConnect__Authority`
   is probably still the container-internal URL — see breaking change 5).

## Rollback

1. Bump the image tags back to v1.7.
2. Restore the client `appsettings.json` mount and file (the v1.7 client image serves the mounted file).
3. If you changed the API env vars, restore the v1.7 values.

No data migration is needed in either direction — the database schema is unchanged between v1.7 and v1.8.
