#!/usr/bin/env bash
# CentOS Stream 9: native .NET 10 installation, NO Docker.
# Pulls source from the GitHub main branch; builds/tests/publishes under systemd.
set -Eeuo pipefail
umask 027
[[ ${EUID} -eq 0 ]] || { echo '[JCF Native] Run with sudo/root' >&2; exit 2; }
source /etc/os-release
[[ "${ID:-}" == centos && "${VERSION_ID%%.*}" == 9 ]] || {
  echo '[JCF Native] CentOS Stream 9 required' >&2; exit 2;
}
MODE="${1:-demo}"
[[ "$MODE" == demo || "$MODE" == pilot ]] || {
  echo '[JCF Native] Usage: sudo bash INSTALL_CENTOS9.sh demo|pilot' >&2; exit 2;
}
command -v git >/dev/null || dnf install -y git ca-certificates
TEMP="$(mktemp -d /tmp/jcf-native-source.XXXXXXXX)"
trap 'rm -rf "$TEMP"' EXIT
echo '[JCF Native] Cloning current application source from GitHub...'
git clone --quiet --depth 1 --branch main \
  https://github.com/nigelebrown/ArubaNetworkManager.git "$TEMP/repo"
[[ -s "$TEMP/repo/Jcf.NetworkManager.slnx" ]] || {
  echo '[JCF Native] Full .NET source is missing' >&2; exit 1;
}
APP_VERSION="$(sed -n 's:.*<Version>\([^<]*\)</Version>.*:\1:p' "$TEMP/repo/src/Jcf.NetworkManager/Jcf.NetworkManager.csproj" | head -1)"
[[ -n "$APP_VERSION" ]] || {
  echo '[JCF Native] Cannot determine real source version' >&2; exit 1;
}
echo "[JCF Native] Installing source version $APP_VERSION directly under systemd (NO Docker)."
bash "$TEMP/repo/deploy/centos9/ONE_CLICK_INSTALL.sh" \
  --mode "$MODE" --source-dir "$TEMP/repo"
