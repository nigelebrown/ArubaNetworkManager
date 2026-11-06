# JCF Network Manager — CentOS Stream 9 Deployment

This repository contains the .NET 10 / Blazor application source, EF Core SQL Server migrations and CentOS deployment scripts. It remains a **testing/pilot release**, not a production-certified Aruba Central replacement. Live device writes are **disabled**.

## One-click demo installation from GitHub

```bash
curl -fsSLo /tmp/INSTALL_CENTOS9.sh https://raw.githubusercontent.com/nigelebrown/ArubaNetworkManager/main/INSTALL_CENTOS9.sh
sudo bash /tmp/INSTALL_CENTOS9.sh demo
```

Installs prerequisites, clones the GitHub source, builds, runs offline tests, publishes, registers systemd, and verifies application health. Demo uses an in-memory database and binds to `127.0.0.1:5180` only. No AP/switch REST calls or configuration changes.

Inspect the service:

```bash
sudo systemctl status jcf-network-manager --no-pager
sudo journalctl -u jcf-network-manager -n 100 --no-pager
curl -fsS http://127.0.0.1:5180/healthz
```

From your workstation: `ssh -L 5180:127.0.0.1:5180 user@centos-server`, then browse `http://127.0.0.1:5180`.

## SQL-backed hardware pilot — separately approved

Provide a **separate supported SQL Server host**, disposable pilot database named `JCFNetworkManagerPilot_*`, trusted SQL TLS certificate and least-privilege database credentials. Configure secrets **outside the Git checkout**, under `/etc/jcfnetwork`. Then execute the steps below from the cloned source directory:

```bash
sudo bash deploy/centos9/CONFIGURE_SQL_PILOT.sh
sudo bash deploy/centos9/APPLY_PILOT_MIGRATIONS.sh
sudo bash deploy/centos9/ONE_CLICK_INSTALL.sh --mode pilot
```

The migration helper generates a reviewable SQL script and requires explicit DBA approval. Database migrations never run silently during startup. Pilot app is loopback-only at `127.0.0.1:5181`; it requires a trusted TLS reverse proxy before user login. Hardware access needs additional allowlists and credential provisioning outside Git. No AP/switch configuration changes are enabled.

## Application-only CI and AI review

- GitHub-hosted Actions compile and test the application. No JCF network access is provided.
- Codex code review runs **only** on GitHub-hosted runners and reads application code with no device credentials. It requires a dedicated `OPENAI_API_KEY` GitHub Actions repository secret.
- Daily offline .NET regression tests on CentOS are executed under a separate account with `PrivateNetwork=true`. No network scans.
- **Do not install a GitHub self-hosted runner or AI agent on this management server.** Older runner registration instructions are obsolete.

## Acceptance limitations

A passing build or demo test does not certify production. Before enabling device writes, validate real SQL persistence, AP-505/AP-535 and CX 6100/6200 API access, cluster blast radius, independently approved configuration changes, readback, rollback, site-scoped RBAC, security/load and HA/DR.
