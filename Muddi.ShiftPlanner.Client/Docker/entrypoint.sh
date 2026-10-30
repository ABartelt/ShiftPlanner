#!/bin/sh
set -eu

: "${SHIFT_PLANNER_KEYCLOAK_URL:?not set - e.g. https://keycloak.example.com}"
: "${SHIFT_PLANNER_KEYCLOAK_REALM:?not set - e.g. muddi}"
: "${SHIFT_PLANNER_API_URL:?not set - e.g. https://api.example.com}"

# Optional: show "Registrieren" buttons to visitors (only if the realm allows self-registration)
export SHIFT_PLANNER_ALLOW_REGISTRATION="${SHIFT_PLANNER_ALLOW_REGISTRATION:-false}"
case "$SHIFT_PLANNER_ALLOW_REGISTRATION" in
  true|false) ;;
  *) echo "SHIFT_PLANNER_ALLOW_REGISTRATION must be true or false, got '$SHIFT_PLANNER_ALLOW_REGISTRATION'" >&2; exit 1 ;;
esac

envsubst < /appsettings.template.json > /usr/share/nginx/html/appsettings.json
echo "rendered /usr/share/nginx/html/appsettings.json"

exec "$@"
