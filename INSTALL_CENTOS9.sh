#!/usr/bin/env bash
# CentOS Stream 9 bootstrap. Download and review before running as root.
set -Eeuo pipefail
[[ $EUID -eq 0 ]] || { echo "Run with sudo." >&2; exit 2; }
source /etc/os-release
[[ ${ID:-} == centos && ${VERSION_ID%%.*} == 9 ]] || { echo "Requires CentOS Stream 9" >&2; exit 2; }
command -v curl >/dev/null || dnf install -y curl ca-certificates
URL="https://raw.githubusercontent.com/nigelebrown/ArubaNetworkManager/main/deploy/centos9/ONE_CLICK_APP_AI.sh"
TMP="$(mktemp /tmp/jcf-installer.XXXXXXXX.sh)"
trap 'rm -f "$TMP"' EXIT
curl --fail --silent --show-error --location --proto '=https' --tlsv1.2 "$URL" -o "$TMP"
[[ -s "$TMP" ]] || { echo "Installer not published yet. Upload complete source first." >&2; exit 3; }
bash -n "$TMP"
MODE="${1:-demo}"
[[ "$MODE" == demo || "$MODE" == pilot ]] || { echo "Mode must be demo or pilot" >&2; exit 2; }
bash "$TMP" --mode "$MODE"
