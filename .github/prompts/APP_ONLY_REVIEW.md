# Application-only code review: JCF Network Manager
Review only the checked-out ASP.NET Core/Blazor C# application, tests, SQL migration files and deployment scripts.
Never connect to, probe, resolve, scan or enumerate network devices, IP addresses or subnets. Do not use SSH, nmap, SNMP, ping, curl, sockets or web requests to test the JCF network.
Do not use device credentials, account data, SQL credentials or private telemetry. Do not run database migrations, configuration writes or deployments.
Read source only. No file edits, commits or pull requests. The agent runs on a GitHub-hosted runner with no access to the JCF lab network.
Identify actionable defects: location-scoped RBAC, authentication, secret management, vendor REST contracts, RF width 20/40 MHz policy, DFS restrictions, cluster blast radius, approval/readback/rollback safety, SQL migrations and performance.
Report severity, file/line, evidence, recommended fix and reproducible offline test. Don't echo secrets or internal addresses.
State clearly that code review is not evidence of real Aruba hardware testing.
