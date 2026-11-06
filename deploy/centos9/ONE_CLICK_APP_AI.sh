#!/usr/bin/env bash
# CentOS Stream 9 — JCF network manager application and offline regression installer.
# Codex NEVER runs on the CentOS server.
set -Eeuo pipefail
umask 027
MODE=demo
while (($#)); do
  case "$1" in
    --mode) MODE="$2"; shift 2;;
    -h|--help) echo "Usage: sudo bash ONE_CLICK_APP_AI.sh --mode demo|pilot"; exit 0;;
    *) echo "Unknown argument: $1" >&2; exit 2;;
  esac
done
[[ "$MODE" == demo || "$MODE" == pilot ]] || { echo "Invalid mode" >&2; exit 2; }
[[ $EUID -eq 0 ]] || { echo "Run with sudo/root" >&2; exit 2; }
source /etc/os-release
[[ "$ID" == centos && "$VERSION_ID" == 9* ]] || { echo "Requires CentOS Stream 9" >&2; exit 2; }
log(){ printf '[JCF] %s\n' "$*"; }
die(){ log "ERROR: $*" >&2; exit 1; }
dnf install -y git curl ca-certificates dotnet-sdk-10.0 rsync python3 nginx >/dev/null
dotnet --list-sdks | grep -q '^10\.' || die ".NET 10 SDK not installed"
T="$(mktemp -d /tmp/jcf-network-installer.XXXXXXXX)"
trap 'rm -rf "$T"' EXIT
log "Cloning main from GitHub"
git clone --quiet --depth 1 --branch main https://github.com/nigelebrown/ArubaNetworkManager.git "$T/repo" || die "GitHub clone failed"
[[ -s "$T/repo/Jcf.NetworkManager.slnx" && -s "$T/repo/src/Jcf.NetworkManager/Jcf.NetworkManager.csproj" ]] || die "The repository lacks full application source. Upload it first; no app deployed."
[[ -s "$T/repo/deploy/centos9/ONE_CLICK_INSTALL.sh" ]] || die "Missing application installer"
if grep -RIl --include='*.json' --include='*.cs' -E 'sk-proj-[[:alnum:]_-]{12,}|ghp_[[:alnum:]]{20,}' "$T/repo" >/dev/null; then
 die "Possible token in public repository; resolve before installing"
fi
bash "$T/repo/deploy/centos9/ONE_CLICK_INSTALL.sh" --mode "$MODE" --source-dir "$T/repo"
# No self-hosted GitHub Actions runner is installed on JCF infrastructure.
cat >/usr/local/bin/jcf-offline-tests <<'TEST'
#!/usr/bin/env bash
set -Eeuo pipefail
cd /opt/jcf-network-manager/source
/usr/bin/dotnet test tests/Jcf.NetworkManager.Tests.csproj -c Release --no-build --no-restore
/usr/bin/python3 scripts/TEST_REAL_PILOT_CLIENT_OFFLINE.py
TEST
chmod 0755 /usr/local/bin/jcf-offline-tests
cat >/etc/systemd/system/jcf-offline-tests.service <<'SERVICE'
[Unit]
Description=JCF Application Code Tests - No Network Access
[Service]
Type=oneshot
User=jcfbuild
Group=jcfbuild
WorkingDirectory=/opt/jcf-network-manager/source
ExecStart=/usr/local/bin/jcf-offline-tests
NoNewPrivileges=true
PrivateNetwork=true
PrivateTmp=true
ProtectHome=true
ProtectSystem=full
ReadWritePaths=/opt/jcf-network-manager/source /var/lib/jcfbuild
UMask=0027
SERVICE
cat >/etc/systemd/system/jcf-offline-tests.timer <<'TIMER'
[Unit]
Description=Daily Offline JCF Application Tests
[Timer]
OnCalendar=*-*-* 08:15:00 UTC
Persistent=true
RandomizedDelaySec=3m
[Install]
WantedBy=timers.target
TIMER
systemctl daemon-reload
systemctl enable --now jcf-offline-tests.timer >/dev/null
log "SUCCESS: Application and isolated daily test timer installed"
log "Codex runs ONLY in GitHub-hosted Actions. No agent or GitHub runner is on this server."
log "Configure OPENAI_API_KEY in GitHub Actions Secrets to activate AI feedback."
log "Check systemctl status jcf-network-manager and journalctl -u jcf-offline-tests.service"
