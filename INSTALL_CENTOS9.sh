#!/usr/bin/env bash
# Compatibility launcher. The legacy non-Docker v0.8.2 installer has been retired.
# Always require v0.9.0 Docker source and the reviewed DeviceIntegration migration.
set -Eeuo pipefail
[[ ${EUID} -eq 0 ]] || { echo "[JCF] Run with sudo." >&2; exit 1; }
[[ "${1:-}" == "demo" || "$#" -eq 0 ]] || { echo "[JCF] Only the safe default demo mode is available here." >&2; exit 2; }
echo "[JCF] Redirecting legacy INSTALL_CENTOS9.sh to the v0.9.0 Docker installer."
TMP="$(mktemp /tmp/jcf-latest-docker.XXXXXXXX.sh)"
trap 'rm -f "$TMP"' EXIT
curl -fsSLo "$TMP" https://raw.githubusercontent.com/nigelebrown/ArubaNetworkManager/main/INSTALL_LATEST_CENTOS9.sh
bash -n "$TMP"
bash "$TMP"
