#!/usr/bin/env bash
# Apply the checked-in EF Core migration ONLY after explicit review against a disposable pilot DB.
set -Eeuo pipefail
umask 077
[[ $EUID -eq 0 ]] || { echo 'Run with sudo' >&2; exit 2; }
ROOT=/opt/jcf-network-manager/source
CFG=/etc/jcfnetwork
ENVFILE="$CFG/production.env"
[[ -f "$ENVFILE" ]] || { echo "Missing $ENVFILE" >&2; exit 2; }
[[ -f "$ROOT/.config/dotnet-tools.json" ]] || { echo 'Run ONE_CLICK_INSTALL.sh --mode demo first to build source'; exit 2; }
read -r -p 'Type APPLY-TO-JCF-DISPOSABLE-PILOT-ONLY after DBA review AND full backup: ' CONFIRM
[[ "$CONFIRM" == 'APPLY-TO-JCF-DISPOSABLE-PILOT-ONLY' ]] || { echo 'Not approved; no changes made.'; exit 3; }
# EnvironmentFile is root-owned. Load trusted lines only, without executing contents as shell code.
CONN="$(python3 - "$ENVFILE" <<'PY'
import sys,shlex
for row in open(sys.argv[1],encoding='utf-8'):
 if row.startswith('ConnectionStrings__NetworkManager='):
  print(shlex.split(row.split('=',1)[1].strip())[0]); break
else: raise SystemExit('No SQL connection string')
PY
)"
[[ "$CONN" =~ (^|\;)Encrypt=(True|Mandatory|Strict)(\;|$) ]] || { echo 'Encrypted SQL required'; exit 4; }
[[ ! "$CONN" =~ TrustServerCertificate=(True|Yes) ]] || { echo 'Untrusted SQL certificate rejected'; exit 4; }
[[ "$CONN" =~ (^|\;)Database=(JCFNetworkManagerPilot_[A-Za-z0-9_]{1,32})(\;|$) || "$CONN" =~ (^|\;)Initial\ Catalog=(JCFNetworkManagerPilot_[A-Za-z0-9_]{1,32})(\;|$) ]] || { echo 'Only a disposable pilot database is allowed'; exit 4; }
cd "$ROOT"
# Do not print env or command trace; migration fails closed.
export ConnectionStrings__NetworkManager="$CONN"
export DOTNET_CLI_TELEMETRY_OPTOUT=1
unset CONN
# dotnet-ef runs as the isolated build user, not the systemd runtime identity.
# Keep password out of logs; the process env remains visible only to privileged local operators.
runuser -u jcfbuild -- env ConnectionStrings__NetworkManager="$ConnectionStrings__NetworkManager" \
   dotnet tool restore || { echo 'dotnet-ef restore failed'; exit 5; }
runuser -u jcfbuild -- env ConnectionStrings__NetworkManager="$ConnectionStrings__NetworkManager" \
   dotnet ef migrations script 0 --idempotent --project src/Jcf.NetworkManager/Jcf.NetworkManager.csproj \
     --startup-project src/Jcf.NetworkManager/Jcf.NetworkManager.csproj \
     --output /opt/jcf-network-manager/builds/REVIEW_MIGRATION.sql || exit 5
read -r -p 'Review /opt/jcf-network-manager/builds/REVIEW_MIGRATION.sql. Type REVIEWED-AND-APPROVED to proceed: ' SECOND
[[ "$SECOND" == 'REVIEWED-AND-APPROVED' ]] || { echo 'Cancelled before SQL update.'; exit 3; }
runuser -u jcfbuild -- env ConnectionStrings__NetworkManager="$ConnectionStrings__NetworkManager" \
   dotnet ef database update --project src/Jcf.NetworkManager/Jcf.NetworkManager.csproj \
     --startup-project src/Jcf.NetworkManager/Jcf.NetworkManager.csproj \
     --context NetworkDbContext || { echo 'SQL migration FAILED. No success marker created.'; exit 5; }
unset ConnectionStrings__NetworkManager
install -o root -g root -m 0600 /dev/null "$CFG/DB_MIGRATION_REVIEWED"
printf 'Migration applied and reviewed on %s (UTC)\n' "$(date -u '+%Y-%m-%dT%H:%M:%SZ')" > "$CFG/DB_MIGRATION_REVIEWED"
echo 'Pilot schema migration completed. Now run ONE_CLICK_INSTALL.sh --mode pilot.'
