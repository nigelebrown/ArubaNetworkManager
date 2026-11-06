# JCF Network Manager — CentOS Stream 9

Install **directly from GitHub**, without Windows or a ZIP:

```bash
curl -fsSLo /tmp/INSTALL_CENTOS9.sh https://raw.githubusercontent.com/nigelebrown/ArubaNetworkManager/main/INSTALL_CENTOS9.sh
sudo bash /tmp/INSTALL_CENTOS9.sh demo
```

The installer clones the application, installs .NET 10, builds and tests, publishes a versioned release, configures systemd and validates `/healthz`. It fails closed if tests or application health checks fail. The demo is **127.0.0.1:5180 only**, has no SQL/device access, and cannot configure Aruba equipment.

To access the demo from a workstation, forward SSH port 5180:

```bash
ssh -L 5180:127.0.0.1:5180 youruser@your-centos-server
```

Open http://localhost:5180 in your workstation browser.

For the SQL-backed pilot, review [deploy/centos9/README_CENTOS9.md](deploy/centos9/README_CENTOS9.md). SQL server configuration and migrations must be explicitly authorized. Codex code review runs only on GitHub-hosted infrastructure, not on this server.

**This is a pilot build, not production-certified.**
