# Release 0.8 implementation & validation result

## Code completed in this increment

- Database-backed and allowlisted **hardware acceptance service**, registration/placement prerequisite, sanitized evidence row and retrieval API.
- Admin-only pilot endpoints: SQL readiness, read acceptance, prior-run retrieval, latest sample. All API mutations retain cookie session + antiforgery requirements.
- Concurrent probes for the same registered device now serialize within one server instance, preventing an hourly poll from racing an operator probe. Multi-instance/distributed locking is still outstanding.
- Production-mode Windows client for hardware acceptance and database persistence/restart validation; no credentials in the client or exported JSON.
- DBA-reviewed SQL migration generation and disposable-pilot-only application script.
- Hardware TLS certificate SHA-256 fingerprint capture (out-of-band verification required, never used to authenticate).
- Fail-closed test cases for disabled pilot and fake/in-memory database.

## Verification performed in the code-generation environment

- Python acceptance client syntax checks and local parser/guard tests.
- PowerShell scripts reviewed as source; PowerShell runtime was not available in the authoring environment.
- Source structure/safety checks, policy restrictions and archive integrity.

**Not performed in this environment:** dotnet build/test, EF SQL migration generation/application, login through real ASP.NET Core on SQL Server, or hardware connectivity. No route to the JCF private management network was available. No configuration writes were sent to an AP or CX switch.

## Open release blockers (must not be marked passed)

1. Generate/review/commit EF Core baseline migration; compile on .NET 10 with official NuGet packages; run real xUnit and UI tests.
2. Execute with SQL Server 2019 Express pilot and restore-test a database backup; verify schema, indexes, user roles and persisted samples after process restart.
3. Read actual AP-505 at 192.0.2.71 through the application host; supply sanitized JSON evidence. Repeat with AP-535 and multi-member cluster.
4. Read actual CX 6100 and CX 6200 through the application host; certify REST session, VLANs, interfaces and PoE (where supported).
5. Separately authorize and witness device configuration **write/readback/compensation** tests for each model/firmware, under the SRS approval gate. This test script is read-only by design.
6. Enforce all user-location scoping across existing REST reads and Blazor UI; currently incomplete for organization-wide tenancy.
7. Finish full 26-module SRS coverage, production secret vault, distributed job leases, performance, penetration test, SQL Always On/DR failover and the operational sign-off.

**Release disposition: candidate for supervised SQL + read-only device acceptance in an isolated lab. NOT production ready.**
