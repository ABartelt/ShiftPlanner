#!/bin/zsh
# Conductor setup script: restores packages and writes the per-workspace client configs.
set -euo pipefail
cd "${0:A:h}/.."
source .conductor/common.sh

require_sdk
dotnet restore Muddi.ShiftPlanner.sln
npm ci --prefix frontend

if [[ "${CONDUCTOR_IS_LOCAL:-1}" == 1 ]]; then
	write_client_config
	write_frontend_config
fi
