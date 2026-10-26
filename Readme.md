# Muddi's Shift Planner

![Screenshot](docs/screenshot-01.png)

## Domain Concepts

The shift planner organizes work shifts using the following hierarchy:

### Season

A **Season** represents a time-bounded event period (e.g., "Summer Festival 2024"). All configuration and shift data
belongs to exactly one season. Seasons have a name, start date, and end date.

### Shift Locations

**Locations** represent physical places where shifts occur (e.g., "Main Bar", "Entry Gate", "Backstage"). Each location
has a type (e.g., "Bar", "Security Post").

### Shift Types

**Shift Types** define the categories of work that can be assigned (e.g., "Bar Staff", "Security", "Setup Crew"). Each
type has:

- **Name** - Display name
- **Color** - For visual identification in the UI
- **StartingTimeShift** - Time offset for when this type of shift typically starts
- **OnlyAssignableByAdmin** - Whether only admins can assign this type
- **Description** - Optional details about the role

### Shift Frameworks

**Frameworks** are templates that define a shift pattern. They specify:

- **Name** - e.g., "Morning Bar Shift", "Night Security"
- **SecondsPerShift** - Duration of each shift slot
- **ShiftTypeCounts** - How many workers of each ShiftType are needed (e.g., "3x Bar Staff, 2x Security")

### Containers

**Containers** are the concrete, scheduled instances that combine a Framework with a Location at a specific time. For
example:

- Location: "Main Bar"
- Framework: "Evening Bar Shift" (4 hours, needs 3 Bar Staff)
- Start: June 15, 2024 at 18:00
- TotalShifts: 3 (meaning 3 consecutive 4-hour blocks)

This creates the actual shift slots that employees can sign up for.

### Shifts

**Shifts** are individual assignments of an employee to a specific time slot within a Container.

```
Season
├── Shift Types (what kind of work)
├── Frameworks (shift templates with duration & staffing needs)
├── Locations (where)
│   └── Containers (when + which framework)
│       └── Shifts (who is assigned)
```

## Public browsing (optional)

By default every page and every API endpoint requires a login. A super-admin can switch on **Öffentliche
Schichtübersicht** under *Admin → Einstellungen* (`ApplicationSettings.PublicBrowsingEnabled`). Then visitors
without an account can browse the current season: locations, free/occupied places per time and shift type, and the
next free shifts. The moment they click a free slot they pick the shift type, are sent to Keycloak to log in or
register, and come back to that slot with the booking dialog pre-filled. Nothing is booked without an explicit
confirmation.

What stays private no matter what: who booked a shift, block reasons, admin-only locations, admin-only shift types,
other seasons, and every write operation. The only anonymous API surface is `GET /public/*` (plus `GET /version`);
an architecture test in `Muddi.ShiftPlanner.Tests.Api` fails if any other endpoint becomes anonymous or if a public
response type gains a personal-data property.

Client side, set `OAuth:AllowRegistration` in `wwwroot/appsettings.json` to `true` if the realm allows
self-registration, so that "Registrieren" buttons are shown.

## Deploy

See [deploy readme](./Docker/deployment/muddi-shift-planner/Readme.md)

## How to contribute

First install dotnet 10 sdk and docker, then:

```shell
cd Docker/muddi-shiftplanner-compose/
docker compose up
```

this will start up the services you need for development.

### Keycloak

In [keycloak admin console](http://localhost:28080/admin/) login with user `admin` and pass `admin`

You can set user roles via Client Roles for shift-planner (e.g. admin, editor, super-admin or viewer)

### Tests

```shell
dotnet test Muddi.ShiftPlanner.Tests.Unit -c Release
dotnet test Muddi.ShiftPlanner.Tests.Api            # needs Docker: starts a throw-away Postgres via Testcontainers
```

EF Core migrations use the local tool manifest (`.config/dotnet-tools.json`), e.g.
`dotnet dotnet-ef migrations add <Name> --project Muddi.ShiftPlanner.Server.Database --startup-project Muddi.ShiftPlanner.Server.Api`.

### Dotnet secrets

You have to
init [dotnet secrets](https://learn.microsoft.com/en-us/aspnet/core/security/app-secrets?view=aspnetcore-7.0&tabs=windows)
if you want to use the alerting service:

##### secrets.json:

```json
{
  "Telegram": {
    "ApiToken": "YOUR-API-TOKEN"
  }
}
```

### Building Images

To build, run the .sh files in ./Docker. You may have to change the tag.

### Running

In your IDE run those three projects:

* Muddi.ShiftPlanner.Client
* Muddi.ShiftPlanner.Server.Api
* Muddi.ShiftPlanner.Services.Alerting

Then you can login at http://localhost:5254 with default admin `dev@muddimarkt.org` and password `test` or default user
`user@muddimarkt.org` and password `test`