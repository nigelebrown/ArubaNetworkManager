#!/usr/bin/env bash
# JCF Intelligent Network Manager - CentOS Stream 9 build + integration lab installer.
# No real-device configuration writes. Demo is deliberately loopback-only.
set -Eeuo pipefail
umask 027
MODE=demo
SOURCE_DIR=""
RUN_TESTS=1
LOG=""
ROOT=/opt/jcf-network-manager
CFG=/etc/jcfnetwork
SERVICE=jcf-network-manager
BUILD_USER=jcfbuild
APP_USER=jcfnetwork
PILOT_ENV="$CFG/production.env"

log(){ printf '[JCF %s] %s\n' "$(date '+%Y-%m-%d %H:%M:%S')" "$*"; }
fail(){ log "FAILED: $*" >&2; exit 1; }
help_text(){ cat <<'TXT'
Usage: sudo bash deploy/centos9/ONE_CLICK_INSTALL.sh [--mode demo|pilot] [--source-dir PATH] [--skip-tests]
  demo: safe, loopback-only Blazor demo (no SQL or device access), default
  pilot: real SQL-backed app; requires /etc/jcfnetwork/production.env and reviewed migrations
  --skip-tests: NOT recommended; allowed only for debugging. Will not enable device writes.
Run from an extracted source folder or specify --source-dir.
TXT
}
while (($#)); do
 case "$1" in
  --mode) [[ $# -ge 2 ]] || fail 'Missing --mode value'; MODE="$2"; shift 2;;
  --source-dir) [[ $# -ge 2 ]] || fail 'Missing --source-dir'; SOURCE_DIR="$2"; shift 2;;
  --skip-tests) RUN_TESTS=0; shift;;
  -h|--help) help_text; exit 0;;
  *) fail "Unknown argument: $1";;
 esac
done
[[ "$MODE" == demo || "$MODE" == pilot ]] || fail 'Mode must be demo or pilot'
[[ ${EUID} -eq 0 ]] || fail 'Run with sudo/root on CentOS Stream 9'
[[ -r /etc/os-release ]] || fail 'Cannot identify operating system'
source /etc/os-release
[[ "${ID:-}" == centos && "${VERSION_ID%%.*}" == 9 ]] || fail "Requires CentOS Stream 9; found ${PRETTY_NAME:-unknown}"
SOURCE_DIR="$(realpath "${SOURCE_DIR:-$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)}")"
[[ -f "$SOURCE_DIR/Jcf.NetworkManager.slnx" && -f "$SOURCE_DIR/src/Jcf.NetworkManager/Jcf.NetworkManager.csproj" ]] || fail 'Source folder missing solution/project; extract the source ZIP first.'
[[ "$SOURCE_DIR" != "$ROOT"* ]] || fail 'Do not run installer from the target deploy directory'
mkdir -p /var/log/jcfnetwork
LOG="/var/log/jcfnetwork/install-$(date -u +%Y%m%dT%H%M%SZ).log"
# Log without potentially sensitive env values; never use bash -x.
exec > >(tee -a "$LOG") 2>&1
log "CentOS Stream 9 / JCF v0.8.2; mode=$MODE; source=$SOURCE_DIR"
log 'Installing .NET 10 SDK and service dependencies from system repositories'
dnf install -y dotnet-sdk-10.0 git rsync curl ca-certificates openssl nginx python3 tar gzip findutils >/dev/null
command -v dotnet >/dev/null || fail '.NET SDK not installed'
dotnet --list-sdks | grep -q '^10\.' || fail '.NET 10 SDK not available'
for spec in "$BUILD_USER:/var/lib/$BUILD_USER:/bin/bash" "$APP_USER:/var/lib/$APP_USER:/sbin/nologin"; do
 IFS=: read -r u home shell <<<"$spec"
 if ! id "$u" &>/dev/null; then useradd --system --create-home --home-dir "$home" --shell "$shell" "$u"; fi
done
install -d -m 0755 -o root -g root "$ROOT"
install -d -m 0750 -o "$BUILD_USER" -g "$BUILD_USER" "$ROOT/source" "$ROOT/builds"
install -d -m 0750 -o root -g "$APP_USER" "$ROOT/releases"
install -d -m 0750 -o "$APP_USER" -g "$APP_USER" /var/lib/jcfnetwork
install -d -m 0750 -o root -g "$APP_USER" "$CFG"
install -d -m 0750 -o root -g "$APP_USER" /var/log/jcfnetwork
log 'Copying reviewed source to isolated build workspace'
rsync -a --delete \
 --exclude .git/ --exclude '**/bin/' --exclude '**/obj/' --exclude '**/.vs/' \
 --exclude '**/.env' --exclude '**/*.pfx' --exclude '**/*.pem' --exclude '**/*.key' \
 --exclude 'docs/verification-0.8.1/screenshots/' --exclude '.github/' \
 "$SOURCE_DIR/" "$ROOT/source/"
chown -R "$BUILD_USER:$BUILD_USER" "$ROOT/source" "$ROOT/builds"
# Restore/build/test under an unprivileged identity. Fail closed on NU1902+ advisories.
BUILD_CMD='set -euo pipefail; export DOTNET_CLI_TELEMETRY_OPTOUT=1 DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1; cd /opt/jcf-network-manager/source; dotnet restore Jcf.NetworkManager.slnx -p:NuGetAuditLevel=moderate -p:NuGetAuditMode=all -warnaserror:NU1902,NU1903,NU1904; dotnet build Jcf.NetworkManager.slnx -c Release --no-restore -warnaserror:NU1902,NU1903,NU1904'
log 'Restoring dependencies and compiling real .NET application'
runuser -u "$BUILD_USER" -- bash -c "$BUILD_CMD" || fail 'Restore/build failed. Existing service not replaced.'
if [[ $RUN_TESTS == 1 ]]; then
 log 'Running .NET unit/integration tests and offline acceptance tests'
 runuser -u "$BUILD_USER" -- bash -c 'set -euo pipefail; cd /opt/jcf-network-manager/source; dotnet test tests/Jcf.NetworkManager.Tests.csproj -c Release --no-build; python3 scripts/TEST_REAL_PILOT_CLIENT_OFFLINE.py' || fail 'Tests failed; existing service not replaced.'
else
 log 'WARNING: tests skipped by operator; this is NOT a verified release.'
fi
STAMP="$(date -u +%Y%m%dT%H%M%SZ)"
PUBLISH="$ROOT/builds/publish-$STAMP"
log 'Publishing .NET application'
runuser -u "$BUILD_USER" -- dotnet publish "$ROOT/source/src/Jcf.NetworkManager/Jcf.NetworkManager.csproj" -c Release --no-restore -o "$PUBLISH" || fail 'Publish failed'
[[ -f "$PUBLISH/Jcf.NetworkManager.dll" && -f "$PUBLISH/appsettings.json" ]] || fail 'Incomplete publish output'
DEST="$ROOT/releases/$STAMP"
install -d -m 0750 -o "$APP_USER" -g "$APP_USER" "$DEST"
rsync -a "$PUBLISH/" "$DEST/"
chown -R root:"$APP_USER" "$DEST"
chmod -R go-w "$DEST"
if [[ "$MODE" == pilot ]]; then
 [[ -f "$PILOT_ENV" ]] || fail "Pilot SQL environment file missing: $PILOT_ENV; run CONFIGURE_SQL_PILOT.sh first. Existing release untouched."
 [[ $(stat -c '%a' "$PILOT_ENV") == 640 || $(stat -c '%a' "$PILOT_ENV") == 600 ]] || fail 'SQL environment file must have mode 0640 or 0600'
 grep -Eq '^ConnectionStrings__NetworkManager=' "$PILOT_ENV" || fail 'Missing SQL connection string'
 grep -Eq '^Jcf__DemoMode=false$' "$PILOT_ENV" || fail 'Pilot may not use demo mode'
 grep -Eq '^Jcf__EnableDeviceWrites=false$' "$PILOT_ENV" || fail 'Pilot must disable hardware writes'
 grep -Eq '^Jcf__LiveLabWrites__Enabled=false$' "$PILOT_ENV" || fail 'Pilot must disable lab writes'
 [[ "$(stat -c '%U' "$PILOT_ENV")" == root ]] || fail 'Pilot env file must be root owned'
 [[ -f "$CFG/DB_MIGRATION_REVIEWED" ]] || fail 'Reviewed SQL migration approval marker missing. See README; do not silently migrate a database.'
 fi
# Create unit; service accesses DB only if pilot mode and environment configured.
cat >"/etc/systemd/system/$SERVICE.service" <<UNIT
[Unit]
Description=JCF Intelligent Network Manager ($MODE)
Wants=network-online.target
After=network-online.target

[Service]
Type=simple
User=$APP_USER
Group=$APP_USER
WorkingDirectory=$ROOT/current
Environment=DOTNET_CLI_TELEMETRY_OPTOUT=1
Environment=ASPNETCORE_ENVIRONMENT=$([[ "$MODE" == demo ]] && echo Development || echo Production)
Environment=ASPNETCORE_URLS=http://127.0.0.1:$([[ "$MODE" == demo ]] && echo 5180 || echo 5181)
$([[ "$MODE" == demo ]] && echo 'Environment=Jcf__DemoMode=true' || echo 'EnvironmentFile=/etc/jcfnetwork/production.env')
ExecStart=/usr/bin/dotnet $ROOT/current/Jcf.NetworkManager.dll
Restart=on-failure
RestartSec=5
NoNewPrivileges=true
PrivateTmp=true
ProtectSystem=strict
ProtectHome=true
ReadWritePaths=/var/lib/jcfnetwork
UMask=0027
TimeoutStopSec=35

[Install]
WantedBy=multi-user.target
UNIT
# Keep previous symlink until all preflight checks passed above.
PREVIOUS="$(readlink -f "$ROOT/current" 2>/dev/null || true)"
ln -sfn "$DEST" "$ROOT/current.next"
mv -Tf "$ROOT/current.next" "$ROOT/current"
systemctl daemon-reload
systemctl enable "$SERVICE" >/dev/null
log 'Starting JCF service'
if ! systemctl restart "$SERVICE"; then
 [[ -n "$PREVIOUS" && -d "$PREVIOUS" ]] && ln -sfn "$PREVIOUS" "$ROOT/current" && systemctl restart "$SERVICE" || true
 fail "Service start failed; see journalctl -u $SERVICE"
fi
PORT=$([[ "$MODE" == demo ]] && echo 5180 || echo 5181)
READY=0
for i in {1..30}; do
 if curl -fsS --max-time 2 -H 'X-Forwarded-Proto: https' "http://127.0.0.1:$PORT/healthz" >/dev/null 2>&1; then READY=1; break; fi
 sleep 2
done
if [[ $READY != 1 ]]; then
 log 'Startup health check failed; attempting release rollback'
 if [[ -n "$PREVIOUS" && -d "$PREVIOUS" ]]; then ln -sfn "$PREVIOUS" "$ROOT/current"; systemctl restart "$SERVICE" || true; fi
 journalctl -u "$SERVICE" -n 35 --no-pager || true
 fail 'Application health check failed; not declaring success.'
fi
if [[ "$MODE" == pilot ]]; then
 curl -fsS --max-time 8 -H 'X-Forwarded-Proto: https' "http://127.0.0.1:$PORT/readyz" >/dev/null || fail 'SQL readiness check FAILED. Check migration, login, TLS and SQL access.'
fi
log "SUCCESS: $SERVICE is running on 127.0.0.1:$PORT (local only)"
log 'Hardware writes remain DISABLED. No public ports or HTTP access were opened.'
log "View logs: sudo journalctl -u $SERVICE -f"
log "Install log: $LOG"
if [[ "$MODE" == demo ]]; then log 'From your workstation: ssh -L 5180:127.0.0.1:5180 user@your-server; open http://127.0.0.1:5180'; fi
