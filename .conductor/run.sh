#!/bin/zsh
# Conductor run script. Usage: run.sh dev|dev-blazor|alerting
set -euo pipefail
cd "${0:A:h}/.."
source .conductor/common.sh

require_sdk
start_infra

run_api() {
	ASPNETCORE_ENVIRONMENT=Development ASPNETCORE_URLS="http://localhost:${API_PORT}" \
		dotnet run --no-build --no-launch-profile --project Muddi.ShiftPlanner.Server.Api &
}

case "${1:-dev}" in
dev)
	write_frontend_config
	dotnet build Muddi.ShiftPlanner.Server.Api

	trap 'kill 0' INT TERM EXIT
	run_api
	VITE_DEV_API="http://localhost:${API_PORT}" VITE_DEV_CONFIG="$PWD/$FRONTEND_CONFIG" \
		npm run --prefix frontend dev -- --port "$FRONTEND_PORT" --strictPort &

	echo
	echo "API:      http://localhost:${API_PORT}"
	echo "Frontend: http://localhost:${FRONTEND_PORT}  (login dev@muddimarkt.org / test)"
	echo

	wait
	;;
dev-blazor)
	write_client_config

	# Build sequentially first, both projects share referenced projects.
	# The WASM environment is baked in at build time, it makes the client load appsettings.Conductor.json.
	dotnet build Muddi.ShiftPlanner.Server.Api
	dotnet build Muddi.ShiftPlanner.Client -p:WasmApplicationEnvironmentName=Conductor

	trap 'kill 0' INT TERM EXIT
	run_api
	ASPNETCORE_URLS="http://localhost:${CLIENT_PORT}" \
		dotnet run --no-build --no-launch-profile --project Muddi.ShiftPlanner.Client &

	echo
	echo "API:    http://localhost:${API_PORT}"
	echo "Client: http://localhost:${CLIENT_PORT}  (login dev@muddimarkt.org / test)"
	echo

	wait
	;;
alerting)
	if ! dotnet user-secrets list --project Muddi.ShiftPlanner.Services.Alerting 2>/dev/null | grep -q "Telegram:ApiToken"; then
		echo "Warning: Telegram:ApiToken is not set. See Readme -> Dotnet secrets." >&2
	fi

	ASPNETCORE_ENVIRONMENT=Development ASPNETCORE_URLS="http://localhost:${ALERTING_PORT}" \
	MuddiShiftApi__BaseUrl="http://localhost:${API_PORT}" \
	MuddiShiftClient__BaseUrl="http://localhost:${FRONTEND_PORT}" \
		exec dotnet run --no-launch-profile --project Muddi.ShiftPlanner.Services.Alerting
	;;
*)
	echo "Usage: run.sh dev|dev-blazor|alerting" >&2
	exit 1
	;;
esac
