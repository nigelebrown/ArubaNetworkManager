#!/usr/bin/env bash
# Separate interactive, explicitly approved SQL Server pilot setup. No migration here.
set -Eeuo pipefail
umask 077
[[ $EUID == 0 ]] || { echo 'Run with sudo' >&2;exit 2; }
cd "$(dirname "$0")/../.."
mkdir -p secrets sql-review
chmod 0700 secrets sql-review
[[ ! -e secrets/sql_connection.txt ]] || { echo 'Secret already exists; do not overwrite without backup/rotation';exit 3; }
read -r -p 'Disposable pilot database (JCFNetworkManagerPilot_*): ' DB
[[ "$DB" =~ ^JCFNetworkManagerPilot_[A-Za-z0-9_]{1,32}$ ]] || { echo 'Invalid database name';exit 4; }
read -r -s -p 'Encrypted SQL Server connection string (input hidden): ' CONN; echo
[[ "$CONN" == *"Database=$DB"* || "$CONN" == *"Initial Catalog=$DB"* ]] || { echo 'Connection must name the disposable pilot DB';exit 4; }
[[ "$CONN" == *'Encrypt=True'* || "$CONN" == *'Encrypt=Mandatory'* || "$CONN" == *'Encrypt=Strict'* ]] || { echo 'TLS required';exit 4; }
[[ "$CONN" != *'TrustServerCertificate=True'* ]] || { echo 'TLS certificate must be validated';exit 4; }
read -r -p 'Bootstrap admin username: ' ADMIN
[[ "$ADMIN" =~ ^[A-Za-z][A-Za-z0-9._-]{3,50}$ ]] || { echo 'Invalid admin name';exit 4; }
read -r -s -p 'Strong initial password, at least 14 characters: ' PASS; echo
[[ $PASS =~ [A-Z] && $PASS =~ [a-z] && $PASS =~ [0-9] && $PASS =~ [^A-Za-z0-9] && ${#PASS} -ge 14 ]] || { echo 'Password policy failed';exit 4; }
printf '%s' "$CONN" > secrets/sql_connection.txt
printf '%s' "$PASS" > secrets/bootstrap_admin_password.txt
chmod 0600 secrets/*.txt
unset CONN PASS
printf '\nJCF_ADMIN_USERNAME=%s\nJCF_PORT=5181\n' "$ADMIN" >> .env
chmod 0600 .env
echo 'Pilot secrets prepared; no migration or device changes made.'
echo 'Next: sudo bash deploy/docker/MIGRATE_SQL_PILOT.sh'
