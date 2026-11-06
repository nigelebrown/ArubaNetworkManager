# JCF Intelligent Network Manager — CentOS Stream 9 build and integration lab

This is a **development/test** deployment of the C# / .NET 10 application. It is **not production certified**. Hardware configuration writes are always disabled in the default installation. SQL Server is hosted separately on a supported SQL Server platform; CentOS Stream 9 hosts only the application/build runner.

## Quick start (from the full source bundle)

Copy the `JCF_ArubaNetworkManager_CentOS9_Complete_Source.zip` file onto your CentOS Stream 9 test server and extract it:

```bash
sudo dnf install -y unzip
unzip JCF_ArubaNetworkManager_CentOS9_Complete_Source.zip
cd ArubaNetworkManager
sudo bash deploy/centos9/ONE_CLICK_INSTALL.sh --mode demo
```

The installer installs .NET 10 and other OS dependencies, compiles the application, runs real .NET tests, publishes a versioned build, installs a restricted systemd unit and checks `/healthz`. A failing build/test/health check stops the installation; no success is reported. `--mode demo` binds **only** to `127.0.0.1:5180` and uses an isolated in-memory database. No firewall ports are opened.

From your **Windows 11 PC**, use SSH port forwarding:

```powershell
ssh -L 5180:127.0.0.1:5180 YOUR_LINUX_USER@YOUR_TEST_SERVER
```

Then open `http://127.0.0.1:5180` locally.

## Set up the real SQL-backed integration pilot

Before this step, have a **separate** SQL Server instance with a disposable database name starting with `JCFNetworkManagerPilot_`, a TLS certificate trusted by CentOS, least-privilege application/DBA credentials, and a full database backup if applicable.

```bash
sudo bash deploy/centos9/CONFIGURE_SQL_PILOT.sh
sudo bash deploy/centos9/APPLY_PILOT_MIGRATIONS.sh
sudo bash deploy/centos9/ONE_CLICK_INSTALL.sh --mode pilot
```

- SQL credentials and the initial administrator password are prompted interactively and stored **outside the Git checkout** under `/etc/jcfnetwork` as root-owned, group-readable (0640) files. Never upload these files to GitHub.
- The migration script generates a reviewable SQL file, requires a second explicit approval and applies only to a disposable pilot database. **No migration occurs automatically on application startup.**
- The production-configured pilot binds **only** to `127.0.0.1:5181`. Access it through an approved internal TLS reverse proxy, not a public HTTP listener. The ASP.NET Identity cookie requires HTTPS.
- Remove `/etc/jcfnetwork/bootstrap-admin.secret` after first successful initial login and account verification. Keep a secure, audited backup of necessary recovery credentials.
- Neither mode automatically performs a configuration write to an AP or switch.

## GitHub Actions CI / test server runner

First, push the source to `nigelebrown/ArubaNetworkManager` with `deploy/centos9/PUSH_SOURCE_TO_GITHUB.sh` from a machine that already has working GitHub SSH key authentication. On GitHub, navigate to **Repository → Settings → Actions → Runners → New self-hosted runner** and copy the short-lived registration token.

```bash
sudo bash deploy/centos9/REGISTER_GITHUB_RUNNER.sh
```

The runner is registered under a dedicated `jcfci` account without sudo, SQL secrets or privileged device access. The workflow `.github/workflows/centos9-lab-acceptance.yml` is **manual-only** from the main branch. Run it from **Actions → JCF CentOS Stream 9 - Controlled Lab CI → Run workflow**. Untrusted PR code must never run on this self-hosted runner.

GitHub-hosted PR builds use the separate default `.github/workflows/ci.yml`, which should not have access to any JCF management networks.

## Verify status

```bash
sudo systemctl status jcf-network-manager
sudo journalctl -u jcf-network-manager -n 80 --no-pager
curl -fsS http://127.0.0.1:5180/healthz  # demo only
```

For pilot mode, use `curl -H 'X-Forwarded-Proto: https' http://127.0.0.1:5181/readyz` from the server. An HTTP status of 200 confirms the DB can be contacted, **not** complete device operational readiness.

## Remaining live acceptance gates

On your JCF network, verify the actual AP-505/AP-535 and CX 6100/6200 APIs, cluster membership, SQL retention, location-scoped permissions and controlled rollback before enabling any change execution. **Passing build and unit tests alone is not production certification.**
