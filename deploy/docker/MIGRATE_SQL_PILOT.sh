#!/usr/bin/env bash
# Generate reviewable SQL, require DBA confirmation, then apply to a disposable pilot only.
set -Eeuo pipefail
umask 077
[[ $EUID == 0 ]] || { echo 'Run with sudo' >&2;exit 2; }
cd "$(dirname "$0")/../.."
[[ -s secrets/sql_connection.txt ]] || { echo 'Configure pilot SQL credentials first';exit 3; }
mkdir -p sql-review
chmod 0700 sql-review
read -r -p 'Type GENERATE-REVIEWED-SQL to generate migration SQL: ' CONFIRM
[[ "$CONFIRM" == GENERATE-REVIEWED-SQL ]] || { echo 'Cancelled';exit 4; }
docker compose -f compose.yaml -f compose.migrate.yaml --profile db-migration run --rm migrator script
[[ -s sql-review/MIGRATION_REVIEW.sql ]] || { echo 'Migration SQL missing';exit 5; }
chmod 0600 sql-review/MIGRATION_REVIEW.sql
echo 'Review sql-review/MIGRATION_REVIEW.sql with a DBA and back up the disposable pilot database.'
read -r -p 'Type APPLY-TO-DISPOSABLE-PILOT after review to execute: ' APPLY
[[ "$APPLY" == APPLY-TO-DISPOSABLE-PILOT ]] || { echo 'Cancelled; nothing applied';exit 4; }
docker compose -f compose.yaml -f compose.migrate.yaml --profile db-migration run --rm migrator update
echo 'Reviewed pilot migration applied. SQL app startup requires approved HTTPS reverse proxy.'
