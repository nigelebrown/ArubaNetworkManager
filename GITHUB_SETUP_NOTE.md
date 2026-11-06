# JCF Aruba Network Manager — GitHub source is published

The application source, solution, EF Core SQL Server migrations, automated tests, and CentOS Stream 9 installer are published on `main`.

**One-command CentOS Stream 9 demo installation:**
```bash
curl -fsSLo /tmp/INSTALL_CENTOS9.sh https://raw.githubusercontent.com/nigelebrown/ArubaNetworkManager/main/INSTALL_CENTOS9.sh
sudo bash /tmp/INSTALL_CENTOS9.sh demo
```

The default demo is loopback-only (127.0.0.1:5180), uses simulated equipment, and makes no Aruba configuration changes. SQL-backed pilot setup requires a separate SQL Server and explicit schema review. No Windows upload steps or ZIP downloads are required.

Code review AI and CI use GitHub-hosted runners, **not** a GitHub runner installed on the JCF network-management host. Offline application tests on CentOS are isolated using `PrivateNetwork=true`.

No operational passwords, client identities, SQL secrets or lab IPs should be committed to this public repository. This release is not yet production-certified.
