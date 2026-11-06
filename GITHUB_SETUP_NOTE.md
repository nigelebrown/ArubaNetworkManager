# JCF Aruba Network Manager — repository initialization

This private repository is being prepared for the CentOS Stream 9 build and integration-test environment.

**Current status:** The repository has been initialized. The complete v0.8.2 application source has **not yet been pushed**. Do not attempt a deployment from this repository until the application files, `Jcf.NetworkManager.slnx`, `src/` and `deploy/centos9/`, are present.

The source release package includes:
- C# / .NET 10 ASP.NET Core and Blazor application with EF Core SQL Server migration;
- CentOS Stream 9 one-command build/test and loopback-only service deployment;
- manually reviewed SQL pilot migration and local administrator bootstrap;
- manual-only, read-only GitHub Actions test runner with separate unprivileged identity.

To complete initial source upload, extract the full source ZIP on a GitHub-authenticated Linux or Windows machine and use `deploy/centos9/PUSH_SOURCE_TO_GITHUB.sh` (Linux) or equivalent authenticated `git add/commit/push`. Never commit SQL passwords, AP credentials, API tokens or runtime environment files.

Real hardware configuration writes remain disabled, and production certification has not been completed.
