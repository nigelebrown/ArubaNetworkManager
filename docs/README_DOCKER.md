# Docker / CentOS Stream 9 — JCF Intelligent Network Manager

**Status: controlled application demo and supervised SQL pilot, not production-certified.**

## One-click installation directly from GitHub

On an Internet-connected **CentOS Stream 9 x86_64** server, review the script before running with root privileges:

```bash
curl -fsSLo /tmp/JCF_DOCKER_INSTALL.sh   https://raw.githubusercontent.com/nigelebrown/ArubaNetworkManager/main/INSTALL_DOCKER_CENTOS9.sh
sudo bash /tmp/JCF_DOCKER_INSTALL.sh
```

The installer installs Docker Engine, Compose, Git, Python and certificates, clones the public repository, downloads a local coding model, compiles and tests the .NET 10 application, publishes the image, and starts the app and AI reviewer. It stops on failures; SQL and real Aruba writes are NOT configured by this first run. It does not install a public-facing listener or modify firewall rules.

**Default AI model:** qwen2.5-coder:1.5b (Apache 2.0, ~1 GB). For a larger server with 12+ GB RAM, you can request qwen2.5-coder:7b (~4.7 GB):

```bash
sudo JCF_AI_MODEL=qwen2.5-coder:7b bash /tmp/JCF_DOCKER_INSTALL.sh
```

## Security: AI cannot scan JCF infrastructure

The three Docker services use two different networks:

- **app**: on app_net only. Its configuration writes are disabled by default. The initial demo uses no device credentials.
- **ollama**: model container on ai_private only. No public ports.
- **ai-reviewer**: on ai_private only, with read-only mounts of src/, tests/ and sanitized build errors. Its only network request is to Ollama at http://ollama:11434/api/chat.

The Compose ai_private network has internal: true, **no external gateway**, and no attachment to app_net. The reviewer has no Docker socket, no SQL secrets, no device credentials, no root privileges, and cannot execute shell commands or make code changes. The coding model is downloaded **once** by a separate temporary container before the AI starts. No OpenAI API key or external AI calls are required afterward.

The AI runs its first review at startup and continues every six hours. It creates recommendations only, **never commits code or alters Aruba equipment**. Review output is a diagnostic aid; it cannot certify firmware-specific capabilities or production readiness.

## Access on Windows 11

App bound to the CentOS server's loopback only. Open an SSH tunnel from Windows:

```powershell
ssh -L 5180:127.0.0.1:5180 YOUR_USER@CENTOS_SERVER
```

Then browse http://localhost:5180.

## Review AI and application status

```bash
cd /opt/jcf-network-manager-docker/current
sudo docker compose ps
sudo docker compose logs --tail=50 app
sudo docker compose logs --tail=50 ai-reviewer
sudo docker compose exec -T ai-reviewer cat /reports/latest.md
sudo docker network inspect jcf-network-manager_ai_private --format '{{.Internal}}'
```

The final command should print true. The reviewer report is created after the initial local model run; CPU-only inference may be slow. Use docker compose logs while diagnosing a delayed review.

## SQL pilot (separate approval)

SQL Server is **not installed inside the containers**; it is managed on existing approved SQL infrastructure. Start in demo mode, then independently configure a **disposable** SQL Server test database and trusted certificate.

```bash
cd /opt/jcf-network-manager-docker/current
sudo bash deploy/docker/CONFIGURE_SQL_PILOT.sh
sudo bash deploy/docker/MIGRATE_SQL_PILOT.sh
```

This asks for secrets interactively, writes them to ignored files under secrets/, generates DBA-reviewable idempotent SQL and requires an explicit approval before migration. Do **not** configure real AP/CX management networks or enable hardware writes until firmware/model-specific and cross-location RBAC acceptance has passed.

For the SQL-backed app, arrange an **approved HTTPS reverse proxy and forwarded-header trust configuration** before enabling production Identity cookies; set JCF_PORT=5181 in .env, then:

```bash
sudo docker compose -f compose.yaml -f compose.pilot.yaml up -d --no-deps app
```

Verify the SQL readiness endpoint. There is no automatic database migration at container startup.

## Limitations and maintenance

- No live CentOS Docker run, .NET compile, SQL migration or Aruba hardware tests were available in the authoring environment. Test all of these on your server.
- Local 1.5B AI can miss serious software defects; it is **not** a replacement for compiled tests, developers and security review.
- Protect Docker root access: local root administrators can override container networking controls.
- Current Ollama first-install image tag uses latest. Pin approved image digests for controlled production release.
- Named Docker volumes persist across upgrades. The installer does not issue docker compose down -v.
- The SRS mandates 20/40 MHz-only RF operation, site-scoped authorization and human approval/verified rollback before configuration writes.
