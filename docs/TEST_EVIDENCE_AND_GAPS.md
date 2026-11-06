# Test report — Release 0.2.0

## Evidence produced in the preparation environment

| Test class | Outcome | Interpretation |
|---|---|---|
| Static source/security checks | **25/25 passed** | Source assertions against policy, DI boundaries, appsettings, hardware models and high-risk write gates |
| C# brace/structure check | Performed | Lexer/bracket scan, **not** a compiler or type checker |
| Appsettings JSON | Valid | Both live-write flags disabled |
| ZIP CRC/integrity | Performed after packaging | Verifies archive extraction, not application behavior |
| .NET solution compilation | **Not executed** | .NET 10 SDK absent from this environment |
| xUnit unit/integration tests | **Not executed** | Source test cases included; Windows test launcher executes them on a system with .NET SDK |
| SQL Server/Entra integration | **Not executed** | No connected JCF SQL Server, Entra registration or credential vault available |
| Physical Aruba API integration | **Not executed by this release** | Prior AP-505 lab API reports demonstrate certain operations, not validation of this application |
| End-to-end live AP + CX configuration | **Not certified** | Requires model-specific lab verification, client station, and controlled approval process |
| Production load, security, AG/DR | **Not executed** | Mandatory production acceptance gates |

## Source test coverage added in this release

- `ChangeWorkflowTests.cs`: policy rejection of 80/160 and DFS, payload normalization, independent approval, two reviewer ARM gate, mutation/hash protection, recovery alert on failed rollback, device-action mismatch.
- `RfTelemetryTests.cs`: strict numeric RF parsing and provenance, missing metrics stay unknown, DFS and prohibited-width recommendations without writes, sustained utilization rule.
- `PilotIntegrationTests.cs`: existing demo HTTP routes plus new routes and read-only radio policy endpoint; unauthenticated write bypass must not succeed.
- Existing `AnalysisTests.cs` and `FoundationsTests.cs` retained.
- `scripts/SMOKE_TEST_WINDOWS.ps1`: builds/tests, starts actual .NET demo on localhost, checks health, inventory, policy, route availability and 501 block on unapproved external execution API. Emits JSON evidence.

## Fix gating test procedure

On a Windows workstation with .NET 10 SDK installed, run `RUN_VALIDATE_WINDOWS.bat`; inspect `docs/ON_MACHINE_SMOKE_TEST_RESULTS.json` and any `docs/smoke_stderr.txt`. If any test fails, **do not deploy to real devices**. Send the error and generated JSON report for correction.

For lab hardware, certify AP-505/AP-535 and CX6100/CX6200 separately using REST API evidence and independent review. Never extrapolate single-AP capability to a different model, firmware or multi-member cluster. No 80/160 MHz tests or writes.
