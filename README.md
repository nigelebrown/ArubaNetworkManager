# JCF Intelligent Network Manager — CentOS Stream 9

This repository contains the JCF Intelligent Network Manager v0.8.2 application (C# / ASP.NET Core .NET 10, Blazor, EF Core SQL Server migrations) and offline application tests. This release is a **pilot**, not an accredited production replacement for Aruba Central.

## One-command installation from GitHub

On **CentOS Stream 9**, log in with sudo/root and run:

```bash
curl -fsSLo /tmp/INSTALL_CENTOS9.sh https://raw.githubusercontent.com/nigelebrown/ArubaNetworkManager/main/INSTALL_CENTOS9.sh
sudo bash /tmp/INSTALL_CENTOS9.sh demo
```

The default demo binds only to `127.0.0.1:5180`, does not need SQL credentials, and does not access Aruba hardware. From a workstation, use `ssh -L 5180:127.0.0.1:5180 youruser@server` and open http://localhost:5180.

## Real SQL pilot

Use `deploy/centos9/CONFIGURE_SQL_PILOT.sh` to store a connection string outside Git, obtain DBA approval and apply the reviewed EF migration using `deploy/centos9/APPLY_PILOT_MIGRATIONS.sh`; then run `sudo bash /tmp/INSTALL_CENTOS9.sh pilot`. Read `deploy/centos9/README_CENTOS9.md` before attempting a pilot. SQL Server runs on a separate supported SQL host.

## Security boundaries

- No AP or switch configuration writes are enabled by default.
- No Codex AI or GitHub self-hosted runner is installed on the network-management server.
- GitHub-hosted CI and read-only code review have no route to JCF management devices; GitHub review requires `OPENAI_API_KEY` Actions secret.
- Offline daily unit/integration tests use Linux `PrivateNetwork=true`.
- The management IP allowlist defaults to non-routable documentation examples. Operators must explicitly configure real management CIDRs in the secured local environment, **never in this public repository**.
- Production certification still requires a real .NET build, SQL integration, hardware acceptance, penetration testing, HA/DR and approved configuration rollback tests.
