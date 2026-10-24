# Shared helpers for the Conductor setup and run scripts. Sourced, not executed.

COMPOSE=(docker compose -p muddi-shift-planner-dev -f Docker/development/muddi-shift-planner-dev/docker-compose.yml)

API_PORT=${CONDUCTOR_PORT:-5083}
CLIENT_PORT=$((API_PORT + 1))
ALERTING_PORT=$((API_PORT + 2))
FRONTEND_PORT=$((API_PORT + 3))

FRONTEND_CONFIG=frontend/config.conductor.json

require_sdk() {
	if [[ -z "$(dotnet --list-sdks 2>/dev/null)" ]]; then
		echo "No .NET SDK found. Install it with: brew install --cask dotnet-sdk" >&2
		exit 1
	fi
}

# The Vue frontend reads /config.json at runtime. Locally it uses the dev realm's public "shift-planner" client,
# and the API is reached through the Vite proxy (VITE_DEV_API).
write_frontend_config() {
	node -e '
		const fs = require("fs")
		const config = JSON.parse(fs.readFileSync("frontend/public/config.json", "utf8"))
		Object.assign(config, { authority: "http://localhost:28080/realms/muddi", clientId: "shift-planner", apiBaseUrl: "/api" })
		fs.writeFileSync(process.argv[1], JSON.stringify(config, null, 2) + "\n")
	' "$FRONTEND_CONFIG"
}

# The Blazor client reads its config from wwwroot, so the per-workspace API port has to be baked into a file.
write_client_config() {
	cat > Muddi.ShiftPlanner.Client/wwwroot/appsettings.Conductor.json <<EOF
{
  "OAuth": {
    "Authority": "http://localhost:28080/realms/muddi"
  },
  "ShiftApi": {
    "BaseUrl": "http://localhost:${API_PORT}"
  }
}
EOF
}

# Postgres and Keycloak are shared by all workspaces.
start_infra() {
	if ! docker info >/dev/null 2>&1; then
		echo "Docker is not running. Start Docker Desktop and try again." >&2
		exit 1
	fi
	"${COMPOSE[@]}" up -d

	echo "Waiting for Postgres..."
	for _ in {1..60}; do
		if "${COMPOSE[@]}" exec -T postgres pg_isready -q -h 127.0.0.1 -U postgres 2>/dev/null &&
			[[ "$("${COMPOSE[@]}" exec -T postgres psql -U postgres -tAc "select 1 from pg_database where datname = 'shift_planner'" 2>/dev/null)" == 1 ]]; then
			return
		fi
		sleep 1
	done
	echo "Postgres did not become ready in time." >&2
	exit 1
}
