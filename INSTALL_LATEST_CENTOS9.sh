#!/usr/bin/env bash
# JCF Network Manager v0.9.0 Docker + offline AI installer
# Use after the release-import workflow has published the full app to main.
set -Eeuo pipefail
umask 027
fail(){ echo "[JCF] $*" >&2; exit 1; }
[[ $EUID -eq 0 ]] || fail "Run with sudo/root"
source /etc/os-release
[[ "$ID" == centos && "$(printf '%s' "$VERSION_ID" | cut -d. -f1)" == 9 ]] || fail "CentOS Stream 9 required"
if ! command -v git >/dev/null; then
  dnf install -y git ca-certificates >/dev/null || fail "Unable to install Git"
fi
TEMP="$(mktemp -d /tmp/jcf-latest.XXXXXXXX)"
trap 'rm -rf "$TEMP"' EXIT
echo '[JCF] Checking the real application release in GitHub...'
git clone --quiet --depth 1 --branch main https://github.com/nigelebrown/ArubaNetworkManager.git "$TEMP/src"   || fail "Unable to clone GitHub repository"
PROJ="$TEMP/src/src/Jcf.NetworkManager/Jcf.NetworkManager.csproj"
[[ -f "$PROJ" ]] || fail "Missing application project; repository was not fully imported"
if ! grep -Fq '<Version>0.9.0</Version>' "$PROJ"; then
  fail "GitHub still holds the older application. Upload the v0.9.0 release asset and complete the verified source-import workflow. Do not bypass this check."
fi
[[ -f "$TEMP/src/src/Jcf.NetworkManager/Migrations/20261106123050_DeviceIntegration.cs" ]]   || fail "Device-integration migration missing; refusing incomplete release"
[[ -f "$TEMP/src/deploy/docker/ai-reviewer/reviewer.py" ]] || fail "Local AI reviewer missing"
SCRIPT="$TEMP/src/INSTALL_DOCKER_CENTOS9.sh"
[[ -s "$SCRIPT" ]] || fail "Docker installer absent"
bash -n "$SCRIPT" || fail "Docker installer failed syntax verification"
echo "[JCF] Verified v0.9.0 source at commit $(git -C "$TEMP/src" rev-parse --short HEAD)."
echo '[JCF] Building Docker application and offline AI from GitHub...'
bash "$SCRIPT"
