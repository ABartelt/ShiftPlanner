# Deploy Muddi Shift Planner

## Docker Images

- API: `ghcr.io/muddi-markt/shiftplanner/api:vXX` (e.g., v1.3)
- Client: `ghcr.io/muddi-markt/shiftplanner/client:vXX` (e.g., v1.3)

---

## Step 1: Update docker-compose.yml

In `docker-compose.yml`, update the image tags to the desired release version.

Replace `Cors__Origins__0: https://your-client-url.com` with your client url, so where the frontend will be reached 

---

## Step 2: Configure .env File

Copy and modify the `.env` file with secure passwords:

```
MUDDI_POSTGRES_USER=muddi
MUDDI_POSTGRES_PASSWORD=<secure-password>
MUDDI_POSTGRES_DB=shift_planner
KEYCLOAK_POSTGRES_PASSWORD=<secure-password>
KEYCLOAK_ADMIN_PASSWORD=<secure-password>
MUDDI_CLIENT_ID=shift-planner-server-api
MUDDI_CLIENT_SECRET=<from-keycloak-step-5>
```

---

## Step 3: Configure Client

Update `config/muddi/appsettings.json`:

```json
{
  "OAuth": {
    "Audience": "shift-planner",
    "Authority": "https://your-keycloak-url.com/realms/muddi",
    "AllowRegistration": false
  },
  "ShiftApi": {
    "BaseUrl": "https://your-api-endpoint.com"
  }
}
```

`AllowRegistration` only controls whether "Registrieren" buttons are shown to visitors; set it to `true` when your
realm allows self-registration. Anonymous browsing of free shifts itself is off by default and is enabled by a
super-admin in the app under *Admin → Einstellungen → Öffentliche Schichtübersicht*.

---

## Step 4: Deploy

```bash
docker-compose up -d
```

---

## Step 5: Setup Keycloak Realm

After first deployment, configure Keycloak at `http://localhost:28080`

### 5.1 Create muddi Realm

1. Login to Keycloak Admin Console
2. Click dropdown (top left) -> Create Realm
3. Name: `muddi`

### 5.2 Create shift-planner Client

1. Clients -> Create Client
2. Client ID: `shift-planner`
3. Client authentication: OFF (public client)
4. Standard Flow ON & Direct access grants: OFF 
5. After creation, go to Client Scopes **tab**
6. Add Mappers:
   - Click `shift-planner-dedicated` -> Add mapper -> By configuration
   - Select `User Client Role`
     - Name: `role-mapper`
     - Client ID: `shift-planner`
     - Token Claim Name: `roles`
     - Add to access token: ON
     - Add to userinfo: ON
   - Add another mapper -> Select `Audience`
     - Name: `audience`
     - Included Custom Audience: `shift-planner`
7. Add valid redirection URI and web origins

### 5.3 Add Roles Mapper to userinfo

1. Client Scopes -> `roles` -> Mappers -> `client roles`
2. Set "Add to userinfo": ON

### 5.4 Create Client Roles

1. Go to Clients -> `shift-planner` -> Roles
2. Create roles:
   - `viewer` (view only)
   - `editor` (allowed to create/edit own shifts)
   - `admin` (allowed to create/update, delete shifts only)
   - `super-admin` (allowed to delete everything)
     - You may add `admin` in "Associated roles" tab via `Assign role -> Clent roles`, so super-admins are also automatically admins
3. Go to Realm roles -> `default-roles-muddi`
4. Assign default roles -> Filter by `shift-planner`
5. Add `editor` and `viewer` as defaults

### 5.5 Create Service Account Client

1. Clients -> Create Client
2. Client ID: `shift-planner-server-api`
3. Client authentication: ON (confidential)
4. Client Authorization: ON
5. Service accounts roles: ON
6. Standard Flow: OFF
7. After creation, go to Credentials tab
8. Copy the Client Secret -> update `MUDDI_CLIENT_SECRET` in `.env`
9. Go to Service Account Roles tab
10. Assign role -> Filter by `realm-management` -> Add `view-users`

### 5.6 Configure User Registration (optional)

1. Realm Settings -> Login
   - Enable user registration, forgot password, etc.
2. Realm Settings -> Email
   - Configure SMTP for email verification


---

## Step 6: Restart API

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
