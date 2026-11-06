# JCF Intelligent Network Manager — End-to-end test and gap report (0.6 → 0.7)

**Date:** 5 November 2026  **Scope:** full compile, unit, integration, production-mode security, REST workflow and live browser (Blazor) testing of Enterprise Source 0.6; every gap found was fixed and re-tested in 0.7.

## 1. Headline

| | 0.6 as received | 0.7 |
|---|---|---|
| Application compiles | **No**: 2 compile errors | Yes, 0 errors, 0 warnings |
| Test project compiles | **No**: 11 compile errors | Yes |
| Demo / test host starts | **No**: crashes at startup | Yes |
| Original 57 tests | Could not run | **57/57 pass** |
| New end-to-end tests (production security + REST lifecycles) | none | **16/16 pass** |
| Browser E2E (real Chrome, live Blazor circuit) | none | **25/25 pass** |
| Project static checks (`verify_enterprise_source.py`) | 24/24 "pass" | 24/24 pass |

The 0.6 static checks reported PASS on code that did not compile. Only a real `dotnet build` / `dotnet test` gate, which `ci.yml` already runs, can be trusted as the release gate.

## 2. How this was tested (and what was substituted)

The build environment cannot reach nuget.org, so dependencies were built from their official sources at the **v10.0.0 tags**:

| Dependency | How it was provided | Fidelity |
|---|---|---|
| .NET 10 SDK 10.0.114 | Ubuntu package | Real |
| EF Core, EF Core Relational, EF Core InMemory | Compiled from `dotnet/efcore` v10.0.0 | Real code |
| ASP.NET Core Identity EF stores, TestHost, Mvc.Testing | Compiled from `dotnet/aspnetcore` v10.0.0 | Real code |
| `blazor.web.js` (Blazor client) | Built with npm/rollup from `dotnet/aspnetcore` v10.0.0 | Real code |
| xUnit | Minimal API-compatible shim and runner (only for running here) | Test code unchanged; re-run under real xUnit in CI |
| EF Core **SQL Server** provider, OpenAPI document generator | Compile-only stubs | **Not exercised.** SQL Server, migrations and `/api/openapi.json` need an on-site run |

Production-mode tests run the real production pipeline: Identity, cookie auth, antiforgery, role policies, lockout, rate limiting, HSTS, forwarded headers and the security-header middleware. Only the SQL Server store is swapped for an isolated in-memory store.

## 3. Results

### 3.1 C# suite: 73/73 passed (`docs/verification-0.7/test-run.log`)

| Class | Tests | Covers |
|---|---|---|
| FoundationsTests, EnterpriseCapabilityTests, EnterpriseTelemetryTests, RfTelemetryTests, AnalysisTests, ChangeWorkflowTests | 50 | Policy (20/40 MHz, DFS exclusion, 40 MHz pairing), parsers, append-only audit, separation of duties, payload hash binding, recovery states |
| PilotIntegrationTests | 7 | Demo host, routes, read-only policy, 501 on execution API |
| **ProductionSecurityEndToEndTests (new)** | 11 | 401 vs. login redirect; antiforgery on login; bootstrap admin; CSRF on API mutations; password policy; operator→approver→viewer separation of duties across real accounts; 5-failure lockout and admin unlock; disable/enable; self-lockout guards; role change; open-redirect protection; POST-only sign-out |
| **DemoApiEndToEndTests (new)** | 5 | Problem responses (400/404/409) instead of 500; probe → analysis → recommendation → independent review; compliance; snapshots contain no secrets; location/placement/space/incident lifecycle; CSV formula-injection defence; viewer read-only |

### 3.2 Browser end-to-end: 25/25 passed (`docs/verification-0.7/ui-e2e-results.json`, screenshots)

All 15 navigation pages render interactively. Register device, out-of-policy IP refused, read-only probe, Analyze Now, 80 MHz drift recommendation, creator cannot review own recommendation, independent acceptance, ARM proposal, requester cannot self-approve, first approval → PendingSecondApproval, same approver blocked, second approver → Approved, approver without operator role cannot execute, operator executes (simulation) → Applied with preflight and verification evidence, compliance evaluation, viewer refusals, admin-only capability certification, audit trail with real identities, security headers present, and no console errors, CSP violations or 5xx.

### 3.3 Startup fail-closed guards (run as a real process)

- Production with no SQL connection string: refused.
- DemoMode in Production: refused.
- Global device writes without the lab gateway: refused.

## 4. Gap register: found and fixed

| # | Severity | Gap found | Fix in 0.7 | Verified by |
|---|---|---|---|---|
| 1 | Critical | `InstantConnector.cs` did not compile: regex patterns used non-verbatim strings (`\(` is an invalid C# escape) | Verbatim strings | Build; RfTelemetryTests |
| 2 | Critical | `Operations.razor` did not compile: injected property named `Operations` clashed with the component class | Renamed to `OperationsService` | Build; UI E2E |
| 3 | Critical | Test project did not compile: test factories called `CreateDbContextAsync` (a default interface method) through the concrete class | Explicit async implementation | Test build |
| 4 | Critical | App crashed at startup in demo and test hosts: admin user endpoints required `UserManager`, which only exists in production mode | User admin endpoints mapped only when Identity is registered | All integration tests |
| 5 | High | Service validation errors (bad IP, unknown device, policy violations, invalid JSON) returned **HTTP 500** | `ApiExceptionHandler`: RFC 7807 400/403/404/409. Only JCF-thrown messages are shown; framework errors stay generic | DemoApiEndToEndTests |
| 6 | High | Login POST with a missing or invalid antiforgery token returned **HTTP 500** | Mapped to 400 with a safe message | ProductionSecurity tests |
| 7 | High | Unauthenticated REST calls got a 302 redirect to the HTML login page instead of 401/403 | API-aware cookie events | ProductionSecurity tests |
| 8 | High | **Login rate limit of 7 per 5 min per IP.** Every officer behind one station NAT or the reverse proxy shares that budget, so one shift change could block a site | Configurable `Jcf:LoginRateLimit` (default 30 per 5 min per IP). Identity lockout (5 failures per account) still stops password guessing | Config; tests |
| 9 | High | No account lifecycle: `IsDisabled` existed but could never be set; no unlock or role change; a disabled user's session kept working | `OperatorAccountService` with API and UI to disable/enable (revokes sessions via security stamp, now re-validated every 1 min), unlock and change role, all audited. Guards against self-disable and removing the last active admin | 3 ProductionSecurity tests |
| 10 | High | Role checks differed between UI and API: engineers could act via API but not UI; compliance was operator-level in UI but admin-only in API | `JcfRoles` is the single source for policies and pages; operator-level `POST /api/v1/compliance/evaluate` added | UI E2E viewer/operator steps |
| 11 | High | Demo approvals used fabricated reviewer names (`demo-reviewer-one/two`) regardless of who clicked, so separation of duties was never really shown | Real demo personas (operator, two approvers, admin, viewer) via a sidebar switcher. The approvals board records the signed-in identity | UI E2E; audit shows real identities |
| 12 | Medium | Recommendation review existed in service and API but had no UI; the page still said "future sprints" | Accept/Defer/Reject for approvers, hidden for the creator; accurate banner | UI E2E |
| 13 | Medium | No `#blazor-error-ui` or error boundary: an unhandled UI exception would silently kill the session | Error UI and an `ErrorBoundary` with recovery | UI E2E (no errors) |
| 14 | Medium | No browser security headers (clickjacking, MIME sniffing, CSP) on a police operations console | CSP compatible with Blazor Server, `frame-ancestors 'none'`, nosniff, no-referrer, Permissions-Policy | UI E2E header check, no CSP violations |
| 15 | Medium | Enum fields in the REST API needed integer codes (`"kind":0`) and responses returned numbers | `JsonStringEnumConverter`: `"Ap505"`, `"CreateSsid"`, `"Approved"` | DemoApi/Production tests |
| 16 | Medium | Login ignored `ReturnUrl`, always landing on `/` | Safe local-only `returnUrl` (blocks `//host`, absolute URLs, backslashes) | Open-redirect test |
| 17 | Medium | No REST access to the audit trail, although an `Auditor` role exists | `GET /api/v1/audit?take=&beforeUtc=` | Production + demo tests |
| 18 | Low | Incident `LocationId` not validated; null description crashed | Validated → 404; null-safe | DemoApi test |
| 19 | Low | Retention worker ran SQL-only `ExecuteDelete` against the demo in-memory store every 6 h and logged errors | Skipped for non-relational stores | Code review |
| 20 | Low | Capability page validated the form before checking the admin role | Role checked first | UI E2E |
| 21 | Low | Unused OpenID Connect package and dead `Authentication` settings implied Entra SSO that was not implemented | Removed | Build |
| 22 | Low | Sign-out link shown in demo (no endpoint); missing favicon caused a 404 on every page | Hidden in demo; favicon added | UI E2E console check |
| 23 | Low | Demo test fixtures shared one hard-coded in-memory DB name, so parallel xUnit classes could race | `Jcf:DemoDatabaseName`; isolated fixture | Test design |

## 5. Remaining gaps: need the JCF environment (not closable here)

1. **SQL Server and migrations.** No migrations are in source, so production startup fails closed until `scripts/CREATE_BASELINE_MIGRATION_WINDOWS.ps1` is run and DBA-reviewed. Run the production tests against a disposable SQL Server too.
2. **Re-run the suite under real xUnit** in `ci.yml` (`dotnet test`) with nuget.org access. No test code depends on the shim.
3. **Aruba hardware certification:** AP-505/AP-535 and CX 6100/6200 probes and lab-gateway writes against real firmware. Here only the demo connector and payload/policy layers were exercised.
4. **OpenAPI document** (`/api/openapi.json`) was compiled against a stub; check it on-site.
5. **Load, HA/DR, penetration test** and reverse-proxy/TLS/`AllowedHosts` configuration for the real hostname.
6. If Entra/AD SSO is required, it is a new feature: local Identity is the only implemented sign-in.

## 6. How to reproduce

- Unit, integration and E2E: `scripts/TEST_LINUX.sh` or `scripts/TEST_ENTERPRISE_WINDOWS.ps1` (includes `tests/EndToEndWorkflowTests.cs`).
- Browser E2E: `pip install playwright`, then `scripts/RUN_UI_E2E_LINUX.sh` (headless Chrome/Chromium, set `CHROME_PATH` if needed; demo mode only).
