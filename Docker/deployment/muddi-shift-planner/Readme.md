# Deploy Muddi Shift Planner

## Docker Images

- API: `ghcr.io/muddi-markt/shiftplanner/api:vXX` (e.g., v1.8)
- Client: `ghcr.io/muddi-markt/shiftplanner/client:vXX` (e.g., v1.8)

---

## Migrating from v1.7.x

If you are upgrading an existing v1.7.x deployment, see [Migration-v1.8.md](./Migration-v1.8.md).

---

## Step 1: Update docker-compose.yml

In `docker-compose.yml`, update the image tags to the desired release version.

> **Token issuer (production):** The API validates user tokens against the issuer set in
> `MuddiConnect__Authority` in `docker-compose.yml`. The default is the compose-internal URL
> `http://keycloak:8080/realms/<realm>` (works for local testing). In production, the browser
> fetches its tokens from your public Keycloak URL, and Keycloak stamps that URL as the token's
> issuer (`iss`) — so set `MuddiConnect__Authority` to your public Keycloak URL instead
> (e.g. `${SHIFT_PLANNER_KEYCLOAK_URL}/realms/${SHIFT_PLANNER_KEYCLOAK_REALM}`), otherwise the API
> rejects user tokens. Note that the API also fetches its own service account token and the OIDC
> metadata from that URL, so the server must be able to reach its own public Keycloak URL.

---

## Step 2: Configure .env File

Copy and modify the `.env` file with secure passwords:

```
SHIFT_PLANNER_POSTGRES_USER=shift_planner
SHIFT_PLANNER_POSTGRES_PASSWORD=<secure-password>
SHIFT_PLANNER_POSTGRES_DB=shift_planner
SHIFT_PLANNER_KEYCLOAK_URL=https://your-keycloak-url.com
SHIFT_PLANNER_KEYCLOAK_REALM=muddi
SHIFT_PLANNER_API_URL=https://your-api-endpoint.org
SHIFT_PLANNER_CLIENT_URL=https://your-client-url.com
SHIFT_PLANNER_CLIENT_ID=shift-planner-server-api
SHIFT_PLANNER_CLIENT_SECRET=<from-keycloak-step-4>
KEYCLOAK_POSTGRES_PASSWORD=<secure-password>
KEYCLOAK_ADMIN_PASSWORD=<secure-password>
```

- `SHIFT_PLANNER_KEYCLOAK_URL` - your public Keycloak URL (used by the browser/client app)
- `SHIFT_PLANNER_KEYCLOAK_REALM` - the realm name (default `muddi`). You can rename it (e.g. `my-cool-party`) — the API and client pick it up from `.env` automatically
- `SHIFT_PLANNER_API_URL` - your public API URL (used by the client app)
- `SHIFT_PLANNER_CLIENT_URL` - your public client URL (used as the API's CORS origin)

> **Existing deployments (breaking):** The `MUDDI_*` env vars have been renamed to `SHIFT_PLANNER_*`,
> and the new URL/realm vars have been added — update your `.env` accordingly.

---

## Step 3: Deploy

> **Production:** Before deploying to production, set `MuddiConnect__Authority` in `docker-compose.yml`
> to your public Keycloak URL (e.g. `${SHIFT_PLANNER_KEYCLOAK_URL}/realms/${SHIFT_PLANNER_KEYCLOAK_REALM}`)
> — see the token issuer note in Step 1. The shipped value points at the container-internal Keycloak
> and only works for local testing.

```bash
docker-compose up -d
```

After changing any value in `.env`, `docker compose up -d` recreates the containers with the new values.

---

## Step 4: Setup Keycloak Realm

After first deployment, configure Keycloak at `http://localhost:28080`

### 4.1 Create the Realm

1. Login to Keycloak Admin Console
2. Click dropdown (top left) -> Create Realm
3. Name: the value of `SHIFT_PLANNER_KEYCLOAK_REALM` from `.env` (default: `muddi`)
   - You can use any name (e.g. `my-cool-party`) — the API and client read the realm name from `.env`

### 4.2 Create shift-planner Client

1. Clients -> Create Client
2. Client ID: `shift-planner`
3. Client authentication: OFF (public client)
4. Standard Flow ON & Direct access grants: OFF
5. Add valid redirection URI and web origins
6. Create the dedicated client scope (Keycloak 26 does **not** create it automatically):
   - Client scopes -> Create
   - Name: `shift-planner-dedicated`
   - Protocol: OpenID Connect
   - Clients -> `shift-planner` -> Client scopes -> Default client scopes -> Add -> `shift-planner-dedicated`
7. Add Mappers (Client scopes -> `shift-planner-dedicated` -> Mappers):
   - Add mapper -> By configuration
     - Select `User Client Role`
       - Name: `role-mapper`
       - Client ID: `shift-planner` (the client ID string, not the UUID)
       - Token Claim Name: `roles`
       - Add to access token: ON
       - Add to userinfo: ON
   - Add another mapper -> Select `Audience`
     - Name: `audience`
     - Included Custom Audience: `shift-planner`
8. Client scopes -> `roles` -> Mappers -> `client roles`
   - Set "Add to userinfo": ON

### 4.3 Create Client Roles

1. Go to Clients -> `shift-planner` -> Roles
2. Create roles:
   - `viewer` (view only)
   - `editor` (allowed to create/edit own shifts)
   - `admin` (allowed to create/update, delete shifts only)
   - `super-admin` (allowed to delete everything)
     - You may add `admin` in "Associated roles" tab via `Assign role -> Client roles`, so super-admins are also automatically admins
3. Go to Realm roles -> `default-roles-<realm-name>` (e.g. `default-roles-muddi`)
4. Assign default roles -> Filter by `shift-planner`
5. Add `editor` and `viewer` as defaults

### 4.4 Create Service Account Client

1. Clients -> Create Client
2. Client ID: `shift-planner-server-api`
3. Client authentication: ON (confidential)
4. Client Authorization: ON
5. Service accounts roles: ON
6. Standard Flow: OFF
7. After creation, go to Credentials tab
8. Copy the Client Secret -> update `SHIFT_PLANNER_CLIENT_SECRET` in `.env`
9. Go to Service Account Roles tab
10. Assign role -> Filter by `realm-management` -> Add `view-users`

### 4.5 Configure User Registration (optional)

1. Realm Settings -> Login
   - Enable user registration, forgot password, etc.
2. Realm Settings -> Email
   - Configure SMTP for email verification
3. Note: users created via the admin console (instead of self-registration) cannot log in with their
   password until their email is verified — password login fails with
   `invalid_grant: Account is not fully set up`. Set "Email verified" ON for the user in the admin
   console (or have them confirm via the verification email, which requires SMTP).

---

## Step 5: Restart API

After updating the client secret in `.env`:

```bash
docker-compose up -d shift-planner-api
```

---

## Optional: Customization

Customize the client appearance in `config/muddi/customization/`:
- `customization.css` - Custom styles
- `customization.json` - Custom configuration
- `favicon.png` - Custom favicon

---

## Optional: Shift Reminders

The `shift-planner-alerting` service reminds volunteers 24 hours and 30 minutes before each shift via
Telegram, WhatsApp (Twilio), Signal (signal-cli-rest-api) or e-mail. Users link their channels under
"Erinnerungen" in the client. It is off by default; enable it with the `notifications` compose profile:

```bash
docker compose --profile notifications up -d
```

Fill in the channels you want in `.env`; a channel is only offered to users when its handle is set:

| Channel  | `.env`                                                                                      |
|----------|---------------------------------------------------------------------------------------------|
| Telegram | `SHIFT_PLANNER_TELEGRAM_BOT_TOKEN` (from @BotFather) and `SHIFT_PLANNER_TELEGRAM_BOT_USERNAME` |
| WhatsApp | `SHIFT_PLANNER_TWILIO_ACCOUNT_SID`, `SHIFT_PLANNER_TWILIO_AUTH_TOKEN`, `SHIFT_PLANNER_WHATSAPP_NUMBER` |
| Signal   | `SHIFT_PLANNER_SIGNAL_API_URL` (your signal-cli-rest-api container) and `SHIFT_PLANNER_SIGNAL_NUMBER` |
| E-mail   | `SHIFT_PLANNER_EMAIL_ENABLED=true` plus `SHIFT_PLANNER_SMTP_*`; uses the verified Keycloak address |

`SHIFT_PLANNER_ALERTING_URL` is the public URL of the alerting service (port 5282). It is needed for the
Twilio webhook (`/webhooks/twilio/whatsapp`) and the release/unsubscribe links in e-mails; Telegram and Signal work
without a public URL.
