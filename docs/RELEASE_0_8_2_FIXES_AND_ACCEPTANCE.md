# JCF Intelligent Network Manager v0.8.2 — Gap remediation and acceptance

**Date:** 5 November 2026  
**Input:** user-supplied `JCF_Intelligent_Network_Manager_v0_8_1_Tested_Source.zip`  
**Disposition:** Source hardening candidate; **NOT CERTIFIED FOR PRODUCTION**.

## Corrected in source

1. **Location-scoped authorization (previously a critical gap).** Added `SiteAccessService`, using existing SQL `SitePermissions`, Identity users/roles, and the actual location parent tree. Non-admin accounts receive **no access without explicit grants**. The site grant's role limits read, operate and approve permissions separately; admin accounts retain global access. Unassigned devices are admin-only. Changes to site permissions take effect on the next request; no cached grants. The admin interface and `/api/v1/admin/site-permissions` endpoints grant/revoke with audit entries.
2. **REST and UI data isolation.** Scoped fleet inventory, locations, telemetry, client observations, alert/incident history, RF, switches, recommendations, configuration proposals and reports. Per-device read endpoints now enforce the target location. Global audit and full Instant cluster-member details are administrator-only until all audit targets and every discovered AP member have verified physical site attribution. Scoped probe results withhold unverified cluster-member identities and raw client MAC addresses. Global-only analysis-run list is admin-only (runs lack target-site ID in the existing schema).
3. **Mutation permissions.** Added service-level checks to device registration/probes, physical placement, spaces, proposals, approvals, execution, incidents and diagnostic TCP checks. Analyze Now and compliance operate only on the authorized device set. The internal monitoring worker has a distinct method rather than an actor-name privilege bypass. Demo mode continues to use simulated personas and equipment without site grants.
4. **Management addressing.** Replaced the overly broad default `10.0.0.0/8`, `172.16.0.0/12` and `192.168.0.0/16` CIDRs with `192.0.2.0/24` and `198.51.100.0/24`, **pilot examples only**. On production deployment, set `Jcf__AllowedManagementCidrs__*` to your approved management networks. Startup fails closed if the allowlist is empty or malformed. No new network scanning feature was enabled.
5. **Fleet polling.** Unclassified APs use their own per-device scheduling key rather than being grouped into one serial `unclassified` queue; known named VC clusters remain serialized. Inter-process/distributed fleet locks still need HA acceptance.
6. **Installer clarity.** Removed misleading requirement for Entra ID in the local pilot launcher. ASP.NET Core Identity remains primary, with no default administrator password.
7. **Regression coverage.** Added `tests/SiteAccessRegressionTests.cs` to validate grants, inherited child scope, sibling isolation, unassigned devices, mutating-service denial, role separation and site-restricted analysis. Updated the production Identity integration tests to create location grants explicitly rather than assuming operators can create locations. Added an API-level cross-location test covering inventory, direct resource reads, probes, proposals and unassigned-device registration. Updated demo audit expectations to require admin authorization. Added 12 additional offline source-level assertions.

## Tests performed in this environment

- `python scripts/verify_enterprise_source.py` — **24/24 source checks PASS**. The C# lexical/balanced-delimiter checks passed; this does **not** prove compilation.
- `python scripts/VERIFY_RELEASE_0_8_2.py` — **12/12 source-level security assertions PASS**.
- `python scripts/TEST_REAL_PILOT_CLIENT_OFFLINE.py` — **6/6 HTTP-client safety tests PASS**.
- `python -m compileall -q scripts` — Python script syntax passed.
- ZIP integrity will be verified at packaging time.

**Not run:** `dotnet build`, `dotnet test` including new C# regressions, SQL Server migration or query translation, actual browser E2E, or live Aruba hardware checks. The available execution environment has no .NET SDK, SQL Server, authorized connection to JCF's private management networks, or AP/CX firmware sessions. The **previous v0.8.1 test evidence remains historical**; it is not evidence that v0.8.2 changes compile or pass the full suite.

## Mandatory on-site release gates

1. **Build/test in CI:** Install .NET 10 SDK; run `dotnet restore`, `dotnet build -c Release`, `dotnet test -c Release`; run both verification Python scripts. Fix any compile/runtime/EF migration defect found.
2. **Real SQL pilot:** Use a disposable SQL Server 2019+ database, encrypted connection, trusted SQL certificate, and DBA-reviewed baseline migration. Verify first-run local Identity provisioning and admin grant/revoke persisted across restart. Ensure indexes and query translation perform with at least 500 devices/1000 locations.
3. **Authorization integration:** Create two operators with distinct site grants, an approver with the appropriate grant and an admin. Prove that list, dashboard, detail, direct REST URL, configuration proposal, approval, diagnostics, reports, Blazor pages and server-side service calls cannot cross sites. Prove grants inherited to children but not siblings; no access to unassigned devices; disabled accounts cannot act.
4. **Real hardware read and persistence:** Enable only supervised `PilotAcceptance` for the explicit AP/CX IP allowlist and use the included `VERIFY_REAL_ARUBA_SQL_PILOT.py`. Require successful API reads, SQL persistence, and restart-readback for AP-505, AP-535, CX 6100 and CX 6200 separately. **No device configuration write is performed by this acceptance.**
5. **Supervised write/readback/restore:** Only after firmware-specific certification and independent approvals, validate approved SSID/ARM and switch VLAN/port operations against isolated lab ports/SSIDs. Require proof of exact readback, scope/blast radius and verified compensation on timeout/cancellation. Production writes remain OFF by default.
6. **Security/availability:** Penetration test, TLS/Host validation, secret rotation, long-duration polling, counter load, SQL restore/AG and DR failover. Certify 20/40 MHz-only, excluded DFS channel controls and no autonomous optimization writes.

## Still outstanding / not represented as fixed

- Live SQL Server/EF provider integration and DBA migration validation.
- AP-505/AP-535/CX 6100/CX 6200 live REST monitoring and firmware-specific write certification.
- Full configuration feature coverage for AAA, SSID, switch ports/PoE/QoS and verified multi-AP VC blast-radius operation.
- Complete site-aware audit/analysis-run schema and per-member cluster location ownership. This release restricts those global views to admins instead of falsely filtering them.
- Independent SQL-backed distributed locks for multi-instance polling/job execution, production-scale load, HA/DR and penetration testing.
- Remaining partial SRS modules and model/firmware-specific unsupported features.

## Run on a JCF pilot machine

```powershell
cd <extracted-release-folder>
python scripts/verify_enterprise_source.py
python scripts/VERIFY_RELEASE_0_8_2.py
python scripts/TEST_REAL_PILOT_CLIENT_OFFLINE.py

dotnet restore Jcf.NetworkManager.slnx
dotnet build Jcf.NetworkManager.slnx -c Release
dotnet test Jcf.NetworkManager.slnx -c Release --no-build
```

Then follow `docs/RELEASE_0_8_SQL_HARDWARE_ACCEPTANCE.md` for DBA and read-only hardware steps. Set a secure application hostname/HTTPS and **do not commit credentials**. There are no default passwords or lab-device write flags in this release.
