#!/usr/bin/env bash
# CentOS Stream 9: uninstall Docker Engine while preserving the native .NET service.
# SAFETY: Refuses to remove Docker if any Docker container is currently running.
# Does not delete Docker images, volumes, /var/lib/docker, or any JCF application files.
set -Eeuo pipefail
umask 027

say() { printf '[JCF Native] %s\n' "$*"; }
fail() { say "STOPPED: $*" >&2; exit 1; }

[[ ${EUID} -eq 0 ]] || fail "Run as root: sudo bash $0"
[[ -r /etc/os-release ]] || fail "Cannot verify operating system"
# shellcheck disable=SC1091
source /etc/os-release
[[ "${ID:-}" == "centos" && "${VERSION_ID%%.*}" == "9" ]] || fail "Requires CentOS Stream 9"

# The application must already be running outside Docker before we uninstall Docker.
systemctl is-active --quiet jcf-network-manager.service ||
  fail "Native JCF service is not active. Leaving Docker unchanged."
command -v curl >/dev/null || fail "curl unavailable; cannot check native application health"
curl --fail --silent --show-error --max-time 5 \
  http://127.0.0.1:5180/healthz | grep -q '"status":"up"' ||
  fail "Native .NET application health check failed. Leaving Docker unchanged."

say 'Native JCF .NET application is healthy on 127.0.0.1:5180.'
if command -v docker >/dev/null 2>&1; then
    # Never uninstall the container engine while it has running workloads.
    if ! running="$(docker ps --format '{{.Names}} ({{.Image}})' 2>/dev/null)"; then
      fail 'Docker daemon is not responding; cannot verify containers. Inspect manually before removal.'
    fi
    if [[ -n "$running" ]]; then
      say 'Docker currently hosts running containers:'
      printf '%s\n' "$running"
      fail "Stop and assess each container separately before uninstalling Docker."
    fi
    say 'No running Docker containers were reported.'
    say 'Stopped containers, images, volumes and Docker data will be retained.'
fi

packages=()
for package in \
  docker-ce docker-ce-cli containerd.io docker-buildx-plugin \
  docker-compose-plugin docker-ce-rootless-extras
do
  if rpm -q --quiet "$package"; then
    packages+=("$package")
  fi
done

if systemctl is-active --quiet kubelet.service; then
  fail 'Kubernetes kubelet is active; container runtime might be shared. Leaving Docker unchanged.'
fi
say 'Disabling Docker Engine; leaving the native jcf-network-manager service untouched.'
systemctl disable --now docker.service docker.socket containerd.service >/dev/null 2>&1 || true

if (( ${#packages[@]} > 0 )); then
  say "Uninstalling Docker packages: ${packages[*]}"
  dnf -y --setopt=clean_requirements_on_remove=False remove "${packages[@]}"
else
  say 'No Docker Engine packages are currently installed.'
fi

if [[ -f /etc/yum.repos.d/docker-ce.repo ]]; then
  mv /etc/yum.repos.d/docker-ce.repo /etc/yum.repos.d/docker-ce.repo.disabled
  say 'Disabled the Docker CE yum repository without deleting its configuration.'
fi

# Protect service and source tree; never touch /opt/jcf-network-manager, /etc/jcfnetwork,
# /var/lib/jcfnetwork, /var/log/jcfnetwork, /var/lib/docker or /var/lib/containerd.
systemctl is-active --quiet jcf-network-manager.service ||
  fail 'Native service is no longer active. Check journalctl -u jcf-network-manager immediately.'
curl --fail --silent --show-error --max-time 5 \
  http://127.0.0.1:5180/healthz | grep -q '"status":"up"' ||
  fail 'Native .NET application is not responding. Check journalctl -u jcf-network-manager.'

say 'SUCCESS: Docker Engine removed; native JCF Network Manager remains healthy.'
say 'Web application: http://127.0.0.1:5180 (server only; SSH tunnel for remote access).'
say 'Native service: systemctl status jcf-network-manager --no-pager'
say 'Offline tests: systemctl list-timers jcf-offline-tests.timer'
say 'Docker images/volumes/data were intentionally preserved. Do not delete them until reviewed.'
