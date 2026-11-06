#!/usr/bin/env bash
# JCF v0.9.1 production Identity + SQL with simulated equipment.
# Native CentOS Stream 9 only; never installs Docker or exposes an insecure demo.
set -Eeuo pipefail
umask 027
fail(){ printf '[JCF Native v0.9.1] STOPPED: %s\n' "$*" >&2; exit 1; }
[[ ${EUID} -eq 0 ]] || fail 'Run with sudo/root.'
source /etc/os-release
[[ "${ID:-}" == centos && "${VERSION_ID%%.*}" == 9 ]] || fail 'CentOS Stream 9 required.'
MODE="${1:-prepare}"
[[ "$MODE" == prepare || "$MODE" == activate ]] || fail 'Usage: sudo bash INSTALL_NATIVE_091.sh prepare|activate'
command -v git >/dev/null || dnf install -y git ca-certificates
ROOT=/opt/jcf-native-v091-source
mkdir -p "$ROOT"
if [[ "$MODE" == prepare ]]; then
  STAGE="$(mktemp -d "$ROOT/.source.XXXXXXXX")"
  cleanup(){ if [[ -d "$STAGE" ]]; then rm -rf "$STAGE"; fi; }
  trap cleanup EXIT
  echo '[JCF Native v0.9.1] Cloning reviewed source from GitHub...'
  git clone --quiet --depth 1 --branch main https://github.com/nigelebrown/ArubaNetworkManager.git "$STAGE"
  P="$STAGE/src/Jcf.NetworkManager/Jcf.NetworkManager.csproj"
  [[ -f "$P" ]] && grep -Fq '<Version>0.9.1</Version>' "$P" ||
    fail 'GitHub does not yet contain native v0.9.1 application; publish the verified source release first.'
  for req in deploy/native/PREPARE_NATIVE.sh deploy/native/ACTIVATE_NATIVE.sh \
    deploy/native/TEST_NATIVE_SAFETY.py \
    src/Jcf.NetworkManager/Migrations/20261106123050_DeviceIntegration.cs; do
    [[ -f "$STAGE/$req" ]] || fail "Incomplete native release: missing $req"
  done
  bash -n "$STAGE/deploy/native/PREPARE_NATIVE.sh" "$STAGE/deploy/native/ACTIVATE_NATIVE.sh" ||
    fail 'Native installer syntax check failed.'
  echo '[JCF Native v0.9.1] Running offline deployment safety checks...'
  python3 "$STAGE/deploy/native/TEST_NATIVE_SAFETY.py" || fail 'Native safety checks failed.'
  SHA="$(git -C "$STAGE" rev-parse --short=12 HEAD)"
  DEST="$ROOT/source-$SHA"
  [[ ! -e "$DEST" ]] || fail "Source revision $SHA already prepared. Do not silently overwrite."
  mv "$STAGE" "$DEST"
  trap - EXIT
  ln -sfn "$DEST" "$ROOT/current.next"
  mv -Tf "$ROOT/current.next" "$ROOT/current"
  echo '[JCF Native v0.9.1] Building/tests/migration SQL preparation (old 0.8.2 unaffected)...'
  bash "$ROOT/current/deploy/native/PREPARE_NATIVE.sh"
  echo '[JCF Native v0.9.1] Build completed. DBA review required before activation.'
  echo '[JCF Native v0.9.1] See /opt/jcf-network-manager-v091/builds/DBA_REVIEW_IDEMPOTENT.sql'
else
  [[ -f "$ROOT/current/deploy/native/ACTIVATE_NATIVE.sh" ]] ||
    fail 'Run prepare first and arrange DBA-approved SQL schema and TLS certificate.'
  bash "$ROOT/current/deploy/native/ACTIVATE_NATIVE.sh"
fi
