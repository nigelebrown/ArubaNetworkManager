#!/usr/bin/env bash
# Interactive one-time CentOS Stream 9 SQL-backed lab setup (no database change).
set -Eeuo pipefail
umask 077
[[ $EUID -eq 0 ]] || { echo 'Run sudo bash deploy/centos9/CONFIGURE_SQL_PILOT.sh' >&2; exit 2; }
id jcfnetwork >/dev/null 2>&1 || { echo 'First run ONE_CLICK_INSTALL.sh --mode demo to install service user/dependencies.' >&2; exit 2; }
CFG=/etc/jcfnetwork
install -d -m 0750 -o root -g jcfnetwork "$CFG"
[[ -f "$CFG/production.env" ]] && { echo 'Existing config found; this script will NOT overwrite it. Rename/backup explicitly for a new pilot.' >&2; exit 3; }
echo 'The SQL database must be on a separate SQL Server, with a trusted TLS certificate.'
echo 'Do not use a production JCF database or TrustServerCertificate=True.'
read -r -p 'Disposable pilot database name (JCFNetworkManagerPilot_...): ' DATABASE
[[ "$DATABASE" =~ ^JCFNetworkManagerPilot_[A-Za-z0-9_]{1,32}$ ]] || { echo 'Invalid pilot database name.' >&2; exit 4; }
read -r -s -p 'Full encrypted SQL Server connection string (input hidden): ' CONNECTION
echo
[[ "$CONNECTION" == *"Database=$DATABASE"* || "$CONNECTION" == *"Initial Catalog=$DATABASE"* ]] || { echo 'Connection string must select this specific pilot DB.' >&2; exit 4; }
[[ "$CONNECTION" =~ (^|\;)Encrypt=(True|Mandatory|Strict)(\;|$) ]] || { echo 'SQL requires Encrypt=True/Mandatory/Strict (case sensitive).'; exit 4; }
[[ ! "$CONNECTION" =~ TrustServerCertificate=(True|Yes) ]] || { echo 'TrustServerCertificate=True is forbidden.'; exit 4; }
read -r -p 'Initial administrator username: ' ADMIN
[[ "$ADMIN" =~ ^[a-zA-Z][a-zA-Z0-9._-]{3,45}$ ]] || { echo 'Invalid administrator username'; exit 4; }
read -r -s -p 'Initial administrator password (14+ chars, mixed case, digit and symbol): ' P1
echo
read -r -s -p 'Confirm administrator password: ' P2
echo
[[ "$P1" == "$P2" && ${#P1} -ge 14 && "$P1" =~ [A-Z] && "$P1" =~ [a-z] && "$P1" =~ [0-9] && "$P1" =~ [^A-Za-z0-9] ]] || { echo 'Passwords do not match or do not meet policy.'; exit 4; }
printf '%s' "$P1" > "$CFG/bootstrap-admin.secret"
chown root:jcfnetwork "$CFG/bootstrap-admin.secret"
chmod 0640 "$CFG/bootstrap-admin.secret"
# Quote the environment value using Python JSON double-quote escaping; never interpolate SQL credentials in shell commands.
export JCF_INSTALL_CONN="$CONNECTION" JCF_INSTALL_ADMIN="$ADMIN"
python3 - "$CFG/production.env" <<'PY'
import os,json,sys
out=sys.argv[1]
lines={
 'ConnectionStrings__NetworkManager': os.environ['JCF_INSTALL_CONN'],
 'Jcf__BootstrapAdmin__Username': os.environ['JCF_INSTALL_ADMIN'],
 'Jcf__BootstrapAdmin__PasswordFile': '/etc/jcfnetwork/bootstrap-admin.secret',
 'Jcf__DemoMode': 'false',
 'Jcf__EnableDeviceWrites': 'false',
 'Jcf__LiveLabWrites__Enabled': 'false',
 'ASPNETCORE_URLS': 'http://127.0.0.1:5181',
 'AllowedHosts': 'localhost;127.0.0.1',
}
with open(out,'x',encoding='utf-8') as f:
 for k,v in lines.items(): f.write(k+'='+json.dumps(v)+'\n' if k in ('ConnectionStrings__NetworkManager','AllowedHosts') else k+'='+v+'\n')
PY
unset JCF_INSTALL_CONN JCF_INSTALL_ADMIN CONNECTION P1 P2
chown root:jcfnetwork "$CFG/production.env"
chmod 0640 "$CFG/production.env"
echo 'Pilot environment prepared. NO SQL migration or AP changes performed.'
echo 'Next: sudo bash deploy/centos9/APPLY_PILOT_MIGRATIONS.sh (requires DBA review)'
