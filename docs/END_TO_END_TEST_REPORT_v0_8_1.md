# JCF Intelligent Network Manager — End-to-End Test Report, Release 0.8.1

**Date:** 5 November 2026  **Input:** `JCF_Intelligent_Network_Manager_v0_8_SQL_Hardware_Pilot_Source.zip` (0.8.0)
**Output:** 0.8.1 (this package)

## 1. Summary

Release 0.8.0 had never been compiled or executed (its own result document says "dotnet build/test … not performed"). For this report it was built on .NET 10 and run end to end. Release 0.8.1 contains the fixes and the evidence.

| Stage | 0.8.0 as delivered | 0.8.1 |
|---|---|---|
| `dotnet build` (app + tests) | First compile: succeeded, 0 warnings | Succeeded |
| xUnit suite | 75 / 75 passed | **97 / 97 passed** (22 new tests) |
| Browser E2E (headless Chrome, real Blazor circuit) | **18 / 25** (launcher defect) | **25 / 25** |
| Pilot client offline tests (Python) | 6 / 6 | 6 / 6 |
| Static source verifier | 24 / 24 | 24 / 24 (was failing once migrations exist; fixed) |
| SQL Server model build + DDL generation | Never run | Runs; 0 EF model warnings |
| EF Core baseline migration | Absent | Generated, compiles, **no pending model changes** |

**Not tested (no access):** a live SQL Server instance, the AP-505 at 192.0.2.71, the AP-535 and the CX 6100/6200. Release blockers 2–7 in `RELEASE_0_8_RESULT_AND_GAPS.md` remain open. Blocker 1 is mostly done: the code compiles, the tests run, and the migration is generated. DBA review of the generated SQL is still required.

## 2. Defects found and fixed

| # | Severity | Defect | How it was found | Fix | Regression test |
|---|---|---|---|---|---|
| 1 | **High** (blocks write acceptance, blocker 5) | Lab write gateway compared the Instant login `Status` to `"success"` case-sensitively. Aruba Instant returns `"Success"`, so every approved lab change would have been refused at login. The read connector was already case-insensitive. | Code review, then a simulated Instant AP | Case-insensitive comparison; `"Failed"` write replies also matched case-insensitively | `SimulatedDeviceTests.Lab_gateway_*` (5 tests: **0/5 pass on 0.8.0, 5/5 on 0.8.1**) |
| 2 | **High** (configuration-safety) | If the operator's request was cancelled after the AP had applied a write, rollback ran on the same cancelled token and immediately gave up ("rollback unverified"), leaving the AP changed. | Simulated device that applies the write and then cancels | Compensation uses its own 90 s token (Instant ARM/SSID and CX VLAN paths); the catch now covers all `OperationCanceledException`s | `Lab_gateway_still_rolls_back_when_the_request_is_cancelled_…` (fails with the old token handling) |
| 3 | Medium | Two simultaneous **Execute** clicks on one approved change. On the in-memory store (demo/tests) both reached the device gateway. On SQL Server the existing `rowversion` stopped the second write, but the user got an unhandled `DbUpdateConcurrencyException` (HTTP 500). | Concurrency test with a forced interleave | `ConfigProposal.State` is also a concurrency token; the conflict becomes a clear 409 "changed by another user" | `ConcurrencyTests.Concurrent_execute_requests_call_the_device_gateway_once` (gateway called twice on 0.8.0, once on 0.8.1) |
| 4 | Medium | `scripts/RUN_UI_E2E_LINUX.sh` (used by the CI `browser-e2e` job) started the DLL from the repository root. ASP.NET Core never loaded `appsettings.json`, the management-CIDR allowlist was empty and every device was refused: 7 of 25 browser steps failed with misleading "not in approved subnet" messages. | Browser E2E | Script sets the content root. **The app now refuses to start** if `Jcf:AllowedManagementCidrs` is empty or contains a malformed CIDR, so the same mistake in a deployment fails loudly. | `ReleaseHardeningTests.App_refuses_to_start_…`, `Management_cidr_syntax_is_validated` (8 cases); browser E2E 25/25 |
| 5 | Medium (scale) | The 30-day retention purge filtered on `AtUtc` only, but every index leads with `DeviceId`: a full-table scan and a single unbounded DELETE every six hours on the largest tables. | Code review, then captured SQL | Time indexes on 5 telemetry tables; purge deletes in 4,000-row batches (`DELETE TOP(@p) … WHERE [AtUtc] < @threshold`) | Captured SQL in `verification-0.8.1/captured_sql.txt` |
| 6 | Low (schema) | Enum-as-text columns (`Devices.Kind`, `ConfigProposals.Action/State`, `DeviceCapabilities.State`, `Recommendations.State`) were `nvarchar(max)`; 9 measurement decimals had no precision (EF warned about silent truncation). | SQL Server model validation | `nvarchar(48)` and explicit `decimal(6,2)` / `(7,2)` | Migration snapshot; 0 model warnings |
| 7 | Low (security hygiene) | Two `Content-Security-Policy` headers: the app's `frame-ancestors 'none'` plus Blazor's default `frame-ancestors 'self'`. Browsers enforce the stricter one, but scanners flag the contradiction. | Browser E2E header capture | Blazor's default suppressed; one CSP header | `Health_version_matches_…_only_one_CSP_header_is_sent` |
| 8 | Low | Sidebar showed "ENTERPRISE SOURCE 0.7"; `/healthz` and the device User-Agent had separate hard-coded versions. | UI screenshot | Single `AppInfo.Version` from the project `<Version>` (0.8.1) | Same test |
| 9 | Low (tooling) | `verify_enterprise_source.py` linted `bin/obj` output, and its lexer fails on `#nullable disable`, so it would report STATIC_FAIL as soon as the team followed its own migration step. | Running the checker after generating the migration | Lints hand-written source only; tolerates preprocessor directives | Checker 24/24 |

## 3. What each test layer proves

**xUnit, 97 tests.** Policy (DFS exclusion, 20/40 MHz only, channel 165, AP model set), change workflow (separation of duties, two-approver RF changes, payload hash tampering, 24 h approval expiry, firmware drift), production-mode security through real ASP.NET Core Identity (lockout after 5 failures, CSRF on login and API mutations, safe post-login redirects, sign-out, role changes, disabled accounts), demo API end to end, telemetry parsers, audit immutability, and hardware acceptance fail-closed behaviour.

New in 0.8.1:
- **Simulated-device tests (10).** The real `InstantConnector`, `CxConnector` and `ArubaLabApprovedChangeGateway` run against in-process simulations of the Instant 8.x and AOS-CX REST session flows: login, `show-cmd` reads, CSRF, ARM write and readback, SSID and VLAN create, verified restore, cancellation, multi-member block and logout on every path. These prove the adapter logic. They cannot prove that the real firmware accepts the exact payloads; only the supervised hardware acceptance can.
- Concurrency, CIDR validation, startup guard, CSP/version, and production DI wiring of the real adapters.

**Browser E2E, 25 steps.** Headless Chrome drives the Blazor UI through every navigation page and through device registration, out-of-policy IP refusal, probe, Analyze Now, independent recommendation review, two-approver RF change with self-approval and same-approver blocks, simulated execution, compliance, viewer and operator refusals, audit identities, security headers, and zero console errors, CSP violations or 5xx responses.

**SQL Server model.** The production `NetworkDbContext` was built with the EF Core 10 SQL Server provider and the full DDL generated: 39 entities, 39 indexes. No index key exceeds SQL Server's 1,700-byte limit, and no column with an index or key is `nvarchar(max)`. Cascade paths are the standard Identity ones.

## 4. Baseline migration and DBA review script

- `src/Jcf.NetworkManager/Migrations/*_InitialProduction.cs` was generated through EF Core 10.0.0's own `MigrationsOperations` (what `dotnet ef migrations add` calls) using the project's `NetworkDbContextDesignTimeFactory`.
- `docs/pilot-sql/INITIAL_SCHEMA_REVIEW.sql` is the idempotent script (`migrations script 0 --idempotent`). **Nothing was applied to any database.**
- `PREPARE_AND_VALIDATE_PILOT_SQL_WINDOWS.ps1` sees the existing migration, skips generation and regenerates the SQL with official `dotnet-ef`. Diff the two SQL files as part of DBA review. `CREATE_BASELINE_MIGRATION_WINDOWS.ps1` will now refuse to run because the migration exists, as designed.

## 5. Test environment and its limits

- Ubuntu 24.04, .NET SDK 10.0.114, Python 3.12, Playwright 1.48 client with Chrome for Testing 153.
- **nuget.org was not reachable from the test environment.** NuGet dependencies were compiled from their official open-source tags instead: EF Core `v10.0.0` (Core, Relational, InMemory, SqlServer, Design), ASP.NET Core `v10.0.0` (TestHost, Mvc.Testing, Identity.EntityFrameworkCore, Blazor `blazor.web.js`), and xUnit `v2-2.9.3` assertions. Three substitutions apply:
  - `Microsoft.Data.SqlClient` is a compile-only stub. It is used for model building and SQL generation and cannot open a connection.
  - `Microsoft.AspNetCore.OpenApi` is a shim, so `/api/openapi.json` was not verified.
  - Tests ran through a minimal xUnit-compatible runner rather than the VSTest adapter.
- **Re-run on the official toolchain before sign-off:** `scripts/TEST_ENTERPRISE_WINDOWS.ps1` (official NuGet packages, `dotnet test`) and `scripts/RUN_UI_E2E_LINUX.sh`. No test or product code depends on the rebuilt packages.

## 6. Open items observed (not changed in 0.8.1)

1. **Polling at fleet scale:** every AP whose `ClusterName` is blank falls into one `instant:unclassified` group and is polled serially. At hundreds of APs a cycle can exceed the 2-minute interval. Set `ClusterName` during registration, or treat unclassified APs as independent groups.
2. **Recommendation reviews** have no concurrency token. Two simultaneous reviewers are both audited and the last one wins. Low impact, because recommendations never touch devices.
3. **Real Aruba payload contract** for `/rest/arm` and `/rest/ssid` (Instant) and `/rest/latest/system/vlans` (CX) is unverified until supervised hardware write acceptance (blocker 5).
4. Previously documented blockers remain: location-scoped RBAC, distributed job leases, secret vault, load/DR testing, penetration test.

## 7. Evidence (`docs/verification-0.8.1/`)

- `test-results.json`: per-test results for all 97 tests.
- `ui-e2e-results.json` and screenshots: browser run, 25/25.
- `ui-e2e-before-fix.json`: the 18/25 run on 0.8.0's launcher.
- `gateway-tests-on-0.8.0.log`: the new gateway tests failing 5/5 against the 0.8.0 gateway.
- `create_sqlserver.sql`: full SQL Server DDL for the 0.8.1 model.
- `captured_sql.txt`: exact SQL for the batched purge and the concurrency-guarded state claim.

**Disposition:** 0.8.1 is a compiled, tested candidate for supervised SQL and read-only device acceptance in an isolated lab. It is **not production ready**, and it does not replace Aruba Central.
