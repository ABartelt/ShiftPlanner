# ShiftPlanner frontend (Vue 3)

Replacement for the Blazor client: Vue 3, Vite, TypeScript, Tailwind 4, shadcn-vue (reka-ui), vue-router,
Pinia, vue-i18n, `oidc-client-ts` for the Keycloak login and vite-plugin-pwa. It talks to the existing
ShiftPlanner API only; no server-side part.

## Development

```sh
cd frontend
npm ci
npm run dev        # http://localhost:5173, API proxied from VITE_DEV_API (default: api.shifts.tohoop-festival.de)
npm run test:unit
npm run lint && npm run type-check && npm run build
```

`public/config.json` is the runtime configuration (see below). For local development it points at the
production Keycloak, so the Keycloak client needs `http://localhost:5173/*` as a valid redirect URI and
`http://localhost:5173` as a web origin. The API is reached through the Vite proxy (`/api` → `VITE_DEV_API`),
so no CORS entry is needed for localhost.

## Runtime configuration

The image serves static files; everything deployment specific is read at start-up from `/config.json`
(mount it over `/usr/share/nginx/html/config.json`):

| Key | Meaning |
|---|---|
| `authority` | Keycloak realm URL, e.g. `https://auth.example.org/realms/muddi` |
| `clientId` | public OIDC client (code flow with PKCE); roles come from the `roles` claim of the access token |
| `apiBaseUrl` | ShiftPlanner API, absolute (`https://api.example.org`) or relative behind a proxy |
| `timeZone` | IANA zone for every displayed date (`Europe/Berlin`) |
| `scopes` | OIDC scopes (`openid profile email`) |
| `brand.name` | fallback title until the app settings are loaded |
| `brand.primaryColor` | overrides `--primary` (any CSS colour, e.g. `oklch(0.7 0.2 25)` or `#e42725`) |
| `brand.logoUrl`, `brand.footer`, `brand.footerUrl` | optional logo in the sidebar and footer text/link |

Further overrides: `/customization/customization.css` (any CSS variable of `src/styles/main.css`),
`/customization/favicon.png`, `/manifest.webmanifest` and `/icons/*.png`. Titles, greeting, contact,
member name and the visible day window still come from the API's app settings (`/admin/settings`).

## Keycloak requirements

- Public client, redirect URIs `https://<host>/*`, post-logout redirect `https://<host>/`, web origin
  `https://<host>`, PKCE `S256`.
- Mappers on that client: audience `shift-planner-backend` and a "User Client Role" mapper for
  `shift-planner-backend` writing the multivalued claim `roles` into the access token
  (`admin`, `super-admin`, `editor`, `viewer`).
- When the session expires (refresh fails) the app redirects to the login page instead of showing an error.

## Structure

```
src/api        fetch wrapper (ApiError with FastEndpoints error bodies) and one module per resource
src/auth       oidc-client-ts UserManager and the useAuth composable (roles, login, logout)
src/domain     pure logic: time zone helpers, container slots, free-slot filling, settings
src/stores     Pinia: settings, season, locations, app (PWA update)
src/components shell, ShiftGrid (day/week time grid), form fields, shadcn-vue ui/
src/views      helper pages and admin pages, src/dialogs the forms
src/i18n       German texts (de.json); add a locale by adding a file
```

Container editing (drag/resize and the dialog) needs `PUT /containers/{id}` from the API in this repository
(`Endpoints/Containers/UpdateEndpoint.cs`); older API versions only allow creating and deleting containers.

## Build

```sh
docker build -f frontend/Dockerfile --build-arg APP_VERSION=v1.8.0 -t shiftplanner/client-vue .
```

The publish workflow builds it as the `client-vue` image next to `client`, `api` and `alerting`.
