#!/usr/bin/env bash
# CentOS Stream 9: build .NET 10 in Docker + local, network-isolated code-review AI.
# Re-run for upgrades; no database migration or Aruba configuration writes are automatic.
set -Eeuo pipefail
umask 027
REPO=https://github.com/nigelebrown/ArubaNetworkManager.git
BASE=/opt/jcf-network-manager-docker
MODEL=qwen2.5-coder:1.5b
PORT=5180
if [[ -v JCF_AI_MODEL ]]; then MODEL="$JCF_AI_MODEL"; fi
if [[ -v JCF_PORT ]]; then PORT="$JCF_PORT"; fi
say(){ printf '[JCF Docker] %s\n' "$*"; }
die(){ say "FAILED: $*" >&2; exit 1; }
[[ $EUID -eq 0 ]] || die 'Run with sudo/root'
[[ $# -eq 0 ]] || die 'No arguments supported; set JCF_AI_MODEL or JCF_PORT environment instead'
source /etc/os-release
[[ "$ID" == centos && "$VERSION_ID" == 9* ]] || die 'CentOS Stream 9 required'
[[ "$(uname -m)" == x86_64 ]] || die 'Only x86_64 validated for this installer'
[[ "$MODEL" == qwen2.5-coder:1.5b || "$MODEL" == qwen2.5-coder:7b ]] || die 'Use 1.5b or 7b model only'
[[ "$PORT" =~ ^[0-9]{4,5}$ ]] && ((PORT>=1024 && PORT<=65535)) || die 'Invalid port'
MEM=$(awk '/MemTotal/{print $2}' /proc/meminfo)
if [[ "$MODEL" == qwen2.5-coder:7b ]]; then
  ((MEM>=12582912)) || die '7B needs 12 GB RAM'
else
  ((MEM>=4194304)) || die 'At least 4 GB RAM required'
fi
say 'Installing Docker Engine/Compose and Git using official repositories'
dnf install -y git curl ca-certificates dnf-plugins-core python3 >/dev/null
if ! command -v docker >/dev/null || ! docker compose version >/dev/null 2>&1; then
  dnf config-manager --add-repo https://download.docker.com/linux/centos/docker-ce.repo >/dev/null
  dnf install -y docker-ce docker-ce-cli containerd.io docker-buildx-plugin docker-compose-plugin >/dev/null
fi
systemctl enable --now docker >/dev/null
docker info >/dev/null || die 'Docker daemon unavailable'
mkdir -p "$BASE/releases" "$BASE/logs"
chmod 0750 "$BASE" "$BASE/releases" "$BASE/logs"
LOG="$BASE/logs/install-$(date -u +%Y%m%dT%H%M%SZ).log"
exec > >(tee -a "$LOG") 2>&1
STAGING=$(mktemp -d "$BASE/releases/.stage-XXXXXXXX")
PREVIOUS=$(readlink -f "$BASE/current" 2>/dev/null || true)
BOOTSTRAP=''
cleanup(){
  if [[ -n "$BOOTSTRAP" ]]; then docker stop "$BOOTSTRAP" >/dev/null 2>&1 || true; fi
  if [[ -d "$STAGING" && ! -f "$STAGING/.jcf-installed" ]]; then
    say "Incomplete release retained at $STAGING for troubleshooting"
  fi
}
trap cleanup EXIT
say 'Cloning main from GitHub, not a downloaded application ZIP'
git clone --quiet --depth 1 --branch main "$REPO" "$STAGING" || die 'GitHub clone failed'
for file in Jcf.NetworkManager.slnx src/Jcf.NetworkManager/Jcf.NetworkManager.csproj Dockerfile compose.yaml deploy/docker/ai-reviewer/reviewer.py deploy/docker/sanitize_log.py deploy/docker/app-entrypoint.sh deploy/docker/tests/test_reviewer.py; do
  [[ -s "$STAGING/$file" ]] || die "Missing source file $file"
done
SHA=$(git -C "$STAGING" rev-parse --short=12 HEAD)
printf 'JCF_RELEASE_TAG=%s\nJCF_PORT=%s\nJCF_AI_MODEL=%s\n' "$SHA" "$PORT" "$MODEL" > "$STAGING/.env"
chmod 0600 "$STAGING/.env"
mkdir -p "$STAGING/ai-input"
chmod 0750 "$STAGING/ai-input"
cd "$STAGING"
docker volume create jcf_ollama_models >/dev/null
docker compose config --quiet || die 'Invalid Docker Compose configuration'
python3 deploy/docker/tests/test_reviewer.py || die 'AI safety tests failed'
say 'Downloading local AI model; temporary downloader has Internet only for this step'
docker pull ollama/ollama:latest >/dev/null
AVAILABLE=0
if [[ -n "$PREVIOUS" && -f "$PREVIOUS/compose.yaml" ]]; then
  if (cd "$PREVIOUS" && docker compose exec -T ollama ollama list 2>/dev/null | grep -Fq "$MODEL"); then AVAILABLE=1; fi
fi
if ((AVAILABLE==0)); then
  if [[ -n "$PREVIOUS" && -f "$PREVIOUS/compose.yaml" ]]; then
    (cd "$PREVIOUS" && docker compose stop ai-reviewer ollama) || true
  fi
  BOOTSTRAP="jcf-model-pull-$$"
  docker run -d --rm --name "$BOOTSTRAP" --network bridge \
    --mount type=volume,source=jcf_ollama_models,target=/root/.ollama \
    ollama/ollama:latest >/dev/null || die 'Ollama downloader failed'
  READY=0
  for i in $(seq 1 30); do
    if docker exec "$BOOTSTRAP" ollama list >/dev/null 2>&1; then READY=1;break;fi
    sleep 2
  done
  ((READY==1)) || die 'Ollama startup failed during model preparation'
  docker exec "$BOOTSTRAP" ollama pull "$MODEL" || die 'Model download failed'
  docker stop "$BOOTSTRAP" >/dev/null 2>&1 || true
  BOOTSTRAP=''
fi
say 'Building .NET application with NuGet audit, compiler and xUnit gates'
set +e
docker compose build app > "$BASE/logs/build-$SHA.log" 2>&1
BUILD_RC=$?
set -e
tail -30 "$BASE/logs/build-$SHA.log"
if ((BUILD_RC!=0)); then
  python3 deploy/docker/sanitize_log.py "$BASE/logs/build-$SHA.log" "$STAGING/ai-input/build-failure.txt" || true
  docker compose up -d --build ollama ai-reviewer || true
  die 'Application build failed: AI reviewer can inspect sanitized build errors. Prior application unchanged.'
fi
rm -f "$STAGING/ai-input/build-failure.txt"
say 'Starting the offline Ollama model and AI code reviewer'
docker compose up -d --build ollama ai-reviewer || die 'Isolated AI services failed'
ln -sfn "$STAGING" "$BASE/current.next"
mv -Tf "$BASE/current.next" "$BASE/current"
if ! docker compose up -d --no-deps app; then
  if [[ -n "$PREVIOUS" && -d "$PREVIOUS" ]]; then
    ln -sfn "$PREVIOUS" "$BASE/current"
    (cd "$PREVIOUS" && docker compose up -d --no-deps app) || true
  fi
  die 'Application Docker service failed'
fi
OK=0
for i in $(seq 1 45); do
  if curl -fsS --max-time 3 "http://127.0.0.1:$PORT/healthz" >/dev/null 2>&1; then OK=1;break;fi
  sleep 2
done
if ((OK==0)); then
  docker compose logs --tail=40 app || true
  if [[ -n "$PREVIOUS" && -d "$PREVIOUS" ]]; then
    ln -sfn "$PREVIOUS" "$BASE/current"
    (cd "$PREVIOUS" && docker compose up -d --no-deps app) || true
  fi
  die 'App health check failed; no success declared'
fi
touch "$STAGING/.jcf-installed"
say 'SUCCESS: application and local AI deployed'
say "Application URL (on server only): http://127.0.0.1:$PORT"
say 'From Windows: ssh -L 5180:127.0.0.1:5180 USER@SERVER'
say 'Review: cd /opt/jcf-network-manager-docker/current && docker compose exec -T ai-reviewer cat /reports/latest.md'
say 'AI has no external network, database, device credentials, code-write permissions or Docker socket.'
say 'SQL Server and real Aruba integration are separate approved steps, not part of the demo installer.'
