# Release 0.8: real SQL Server + real Aruba acceptance gate

**Release status: source implementation delivered; NOT hardware accepted or production certified.**

This is a focused acceptance increment built on v0.7. It is **not** a claim that the full 26-module SRS or real-device configuration writes have passed. A lab operator must execute the included scripts on JCF infrastructure to generate operational evidence. Do not promote the system based on demonstration mode, an in-memory DB or static-analysis pass counts.

## Scope and safety

- Equipment: Aruba Instant AP-505 and AP-535 (InstantOS 8 REST on TCP/4343), AOS-CX 6100 and 6200 (REST over HTTPS). Actual hardware must be accessible from the **application server**, not merely from the administrator's PC.
- Pilot device is registered with an approved physical location; identity/firmware and cluster or port inventory are obtained through the **real native REST adapter**.
- Live sample is committed to **SQL Server** by `DeviceService`; `HardwareAcceptanceService` opens an **independent DbContext** and checks that the new rows can be read back.
- Sanitized result and an audit record are persisted in SQL; `GET /api/v1/pilot/acceptance-runs/{id}` retrieves the run for restart/persistence verification.
- `Analyze Now` reads persisted telemetry and produces recommendations only. No optimization change, SSID creation, channel change, VLAN change or switch port change is made by the acceptance process.
- Only **20 or 40 MHz** may be proposed by the platform. 80/160 MHz and excluded DFS channels are drift signals, never test changes.
- Test requires the `PlatformAdmin` role, HTTPS browser/API cookies + CSRF token, SQL provider, physical location and explicit pilot IP allowlist. Acceptance is disabled by default.
- Every incomplete data field stays missing/unknown; zero connected clients is not a DHCP/roaming measurement.

## What the operator needs

1. JCF **SQL Server 2019 Express** disposable pilot database (e.g. `JCFNetworkManagerPilot_01`) with an approved and trusted TLS certificate, SQL login or integrated identity, backup/restore plan and separate schema-deployment permissions.
2. .NET **10 SDK**, access to the approved package feed, PowerShell and Python 3. On the app server: corporate-trusted HTTPS certificate for ASP.NET Core Kestrel/reverse proxy.
3. Managed device credentials in the server's **secret environment**, never in source files or Git. For `PILOT_AP`:
   - `JCF_DEVICE_PILOT_AP_USER`
   - `JCF_DEVICE_PILOT_AP_PASS`
   - `JCF_DEVICE_PILOT_AP_CERT_SHA256` (optional if AP certificate chains to a trusted CA; use a verified pin for device self-signed TLS).
   For a CX switch, use the corresponding `JCF_DEVICE_PILOT_CX_*` variables.
4. An existing locally bootstrapped platform administrator (`Jcf:BootstrapAdmin:Username`, `Jcf:BootstrapAdmin:PasswordFile`); use a protected outside-webroot file and never a default password.
5. Confirm `192.0.2.71` is still the intended Instant VC/standalone IP. The application host must reach TCP 4343. An operator's successful ping from a different network does not establish app-server reachability.

## Stage A: build, migrations, SQL pilot

On a **test** Windows server, set `ConnectionStrings__NetworkManager` to a connection string using `Encrypt=True;TrustServerCertificate=False` and the **disposable pilot database**. Do not paste credentials into a ticket, chat or Git repository.

```powershell
# Build / tests: must pass before testing on any hardware
powershell -ExecutionPolicy Bypass -File scripts/TEST_ENTERPRISE_WINDOWS.ps1

# Prepare EF migration source and idempotent SQL for DBA review (no DB changes)
powershell -ExecutionPolicy Bypass -File scripts/PREPARE_AND_VALIDATE_PILOT_SQL_WINDOWS.ps1

# AFTER DBA review, full SQL backup and approval, apply ONLY to disposable pilot database
powershell -ExecutionPolicy Bypass -File scripts/PREPARE_AND_VALIDATE_PILOT_SQL_WINDOWS.ps1 `
  -ApplyToPilot -ExpectedPilotDatabase JCFNetworkManagerPilot_01 -IHaveReviewedSqlAndBackup
```

**Important:** a committed EF Core baseline migration was not generated in the preparation environment, which lacked the SDK. The script must create the migration, output `docs/pilot-sql/INITIAL_SCHEMA_REVIEW.sql`, and the team must review and commit the generated `Migrations/` directory before approving a tagged production release. Application startup will not automatically migrate a database. EF migrations are deployed under a dedicated deployment identity, not the app's normal SQL login. The migration script is deliberately not enabled against a production-named database.

## Stage B: deploy production-mode *pilot* app

Set all configuration in the controlled **server environment**, not in `appsettings.json` or a repo. Example keys:

```text
ASPNETCORE_ENVIRONMENT=Production
ConnectionStrings__NetworkManager=<trusted TLS SQL connection, supplied securely>
Jcf__DemoMode=false
Jcf__EnableDeviceWrites=false
Jcf__LiveLabWrites__Enabled=false
Jcf__PilotAcceptance__Enabled=true
Jcf__PilotAcceptance__AllowedDeviceIps__0=192.0.2.71
Jcf__AllowedManagementCidrs__0=192.0.2.0/24
Jcf__BootstrapAdmin__Username=<admin account name>
Jcf__BootstrapAdmin__PasswordFile=<protected absolute path to bootstrap secret>
JCF_DEVICE_PILOT_AP_USER=<network operator's API account>
JCF_DEVICE_PILOT_AP_PASS=<secret>
JCF_DEVICE_PILOT_AP_CERT_SHA256=<out-of-band verified TLS certificate fingerprint>
AllowedHosts=<pilot application hostname>
```

For a switch, add its IP to `Jcf__PilotAcceptance__AllowedDeviceIps__1` and configure the corresponding CX credentials/pin. The ASP.NET Core app endpoint must serve **trusted HTTPS**. Do not disable certificate validation to get through the test. If using a reverse proxy, validate its origin, forwarding restrictions and TLS before login.

When the pilot app is running, `GET /healthz` must show `demo:false`, and `GET /api/v1/pilot/readiness` (admin-only) must show a real reachable SQL provider and at least one applied EF migration.

## Stage C: execute real-hardware acceptance

From a Windows workstation with Python 3 and trusted HTTPS access to the running *pilot* application:

```powershell
python scripts/VERIFY_REAL_ARUBA_SQL_PILOT.py `
  --base-url https://jcf-pilot.example.internal `
  --username <local-platform-admin> `
  --location "JCF API Pilot Lab" `
  --ap-ip 192.0.2.71 --ap-model Ap505 --ap-secret-ref PILOT_AP
```

The script prompts for the **application password** (not stored). Device passwords are only present on the application host. The script uses the logged-in app and anti-forgery token, registers a location/device if absent, runs the real Aruba connector, checks the SQL-persisted telemetry and cluster membership, invokes Analyze Now, and saves `pilot_evidence/JCF_SQL_ARUBA_LIVE_PILOT_*.json`.

Add an actual CX 6100 or 6200 to the same run with `--switch-ip`, `--switch-model` and `--switch-secret-ref` after its API is enabled and certified.

### Persistence across application restart

1. Retain the JSON evidence and its `runId` (per device).
2. Restart the *real production-mode pilot app* without dropping SQL Server or reseeding a new database.
3. Run the script again with `--verify-run-id <previous-run-id>`; it must retrieve the previous run **from SQL Server** before conducting a fresh read-only run.
4. Check SQL backups, admin authentication, audit rows and client-privacy redaction independently.

## Evidence criteria

| Gate | Required proof | Failure outcome |
|---|---|---|
| Software build | `dotnet build` and `dotnet test` under real NuGet/xUnit | Stop |
| Real SQL | SQL Server provider, actual connection, EF schema migration applied | Stop |
| Security | Identity login, anti-forgery, admin-only endpoint, HTTPS/certificate trust | Stop |
| Device registration | Existing/new device ID assigned to correct physical location | Stop |
| AP read | Device API auth, required command success, AP membership persisted | Stop/NotVerified |
| CX read | Device API auth, interface/port sample persisted | Stop/NotVerified |
| Telemetry | Fresh row independently read back from SQL | Stop/NotVerified |
| Analysis | SQL-backed run stored, zero config writes | Stop/NotVerified |
| Restart | Prior evidence retained across application process restart | Stop/NotVerified |
| Device writes | **Not run in this release's acceptance script** | Separate later controlled lab sign-off |

**Release 0.8 verdict is NOT VERIFIED unless the JSON evidence from the actual JCF server and pilot AP reports `READ_PATH_VERIFIED_ON_LIVE_HARDWARE`.** This verdict is limited to the *read and persistence path*. Live configuration write/readback/rollback across AP-505/AP-535 and CX 6100/6200, multi-AP clusters, production scale, HA/DR and penetration tests remain independent acceptance gates before an Aruba Central replacement can go live.

## End-to-end source trace

`VERIFY_REAL_ARUBA_SQL_PILOT.py` → HTTPS application login / CSRF → `/api/v1/pilot/readiness` → `/api/v1/locations` / `/api/v1/devices` → `/api/v1/pilot/devices/{id}/read-acceptance` → `HardwareAcceptanceService` → `DeviceService` → `InstantConnector`/`CxConnector` → real device REST login / monitoring GET / logout → `NetworkDbContext` SQL commit → independent SQL readback → `HardwareAcceptanceRun` SQL record → `/api/v1/pilot/acceptance-runs/{id}` → `/api/v1/analysis-runs` → JSON proof.
