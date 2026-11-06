# Change log

## 0.8.2 — Location security, polling and API hardening (5 Nov 2026)

- Added hierarchy-aware site authorization for location-scoped viewer, operator and approver accounts; no-grant and unassigned-device access fail closed.
- Scoped REST and Blazor reads, operational actions, recommendations and approved change lifecycle; grant/revoke available to PlatformAdmin and audited.
- Restricted global audit, fleet analysis history and detailed cluster identities to admins until these objects have reliable site ownership.
- Changed worker probe privileges to a distinct internal method and redacted sensitive probe details for scoped operators.
- Narrowed pilot management CIDR defaults; fixed unclassified AP polling grouping and local Identity launcher documentation.
- Added xUnit regressions and separate offline source-level tests.
- **No claims of compiled tests, real SQL or live Aruba verification for v0.8.2**; read the current acceptance guide.



## 0.8.1 — End-to-end verification and fixes (5 Nov 2026)

First release that has actually been compiled and executed: .NET 10 build, 97 xUnit tests, 25-step browser E2E, SQL Server model/migration generation. See `docs/END_TO_END_TEST_REPORT_v0_8_1.md`.

Fixed
- **Lab write gateway rejected every real Aruba Instant login.** It compared the login `Status` case-sensitively to `"success"`; Instant replies `"Success"`. The read connector already compared case-insensitively. `"Failed"` write replies are now also detected case-insensitively.
- **Lab write rollback skipped when the request was cancelled.** If the operator's request was cancelled after the AP applied a change, compensation reused the cancelled token and returned "rollback unverified". Rollback now runs on its own 90 s token.
- **Concurrent Execute of one approved change.** On SQL Server the existing `rowversion` blocked the second write but surfaced as HTTP 500; on the in-memory store both requests reached the device gateway. `ConfigProposal.State` is now also a concurrency token and the losing request gets a clear 409 conflict.
- **Browser E2E launcher (and CI `browser-e2e` job) ran the app outside its content root**, so `appsettings.json` was never loaded, the management-CIDR allowlist was empty and 7/25 UI steps failed. Script fixed; the app now refuses to start if `Jcf:AllowedManagementCidrs` is empty or malformed.
- **Retention purge scanned whole telemetry tables.** Added time indexes; purge now deletes in 4,000-row batches (`DELETE TOP(@p)`).
- SQL Server schema: enum-as-text columns bounded to `nvarchar(48)` (were `nvarchar(max)`); explicit precision on 9 measurement decimals (removed all EF `DecimalTypeDefaultWarning`s).
- Duplicate Content-Security-Policy header (Blazor's `frame-ancestors 'self'` alongside the app's `'none'`) removed.
- Sidebar said "Enterprise Source 0.7"; `/healthz`, sidebar and device User-Agent now all derive from the assembly version.
- `scripts/verify_enterprise_source.py` linted `bin/obj` output and failed on EF-generated `#nullable` migrations; it now lints hand-written source only and tolerates preprocessor directives.

Added
- EF Core baseline migration `InitialProduction` (generated with EF Core 10.0.0 design-time services; no pending model changes) and DBA review script `docs/pilot-sql/INITIAL_SCHEMA_REVIEW.sql` (idempotent). `PREPARE_AND_VALIDATE_PILOT_SQL_WINDOWS.ps1` detects the existing migration and only regenerates the SQL.
- 22 new tests: simulated Aruba Instant / AOS-CX transport tests for both connectors and the lab gateway (login, reads, CSRF, write, readback, restore, cancellation, multi-member block, logout), concurrency, CIDR validation, startup guard, CSP/version, production DI wiring.
- `DeviceHttpFactory` test seam (optional transport override; not registered in DI).

Unchanged: device writes remain disabled by default; no hardware or live SQL Server was contacted.

## 0.8.0 — Real SQL Server / Aruba read-path acceptance increment (5 Nov 2026)

- Adds fail-closed, `PlatformAdmin`-only hardware acceptance service against explicitly allowlisted AP/CX management IPs.
- Records device API and independent SQL-persistence evidence, cluster members/port observations and a durable acceptance run.
- Adds pilot SQL schema readiness, approval-gated migration script and standalone Windows/Python acceptance procedures.
- Serializes same-device REST probes within an app instance to avoid overlapping read sessions.
- Preserves existing features and vendor-integration safety. No AP/switch configuration change is performed by the pilot acceptance script.
- Production writes remain disabled by default, without implying that all 26 SRS modules are complete.

See `docs/RELEASE_0_8_SQL_HARDWARE_ACCEPTANCE.md` and `docs/RELEASE_0_8_RESULT_AND_GAPS.md`.
