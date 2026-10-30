#!/bin/bash
set -e

psql -v ON_ERROR_STOP=1 --username "$POSTGRES_USER" <<-EOSQL
    create user ${SHIFT_PLANNER_POSTGRES_USER} with password '${SHIFT_PLANNER_POSTGRES_PASSWORD}';
    create database ${SHIFT_PLANNER_POSTGRES_DB} with owner ${SHIFT_PLANNER_POSTGRES_USER};
EOSQL