# Build and test report — Release 0.1.0

## Execution environment

The file-generation environment has Python and Node.js, **but does not have .NET SDK/runtime or SQL Server**. Consequently, this package is not being represented as compiled, live-device-tested, or production-ready.

## Tests included (to run on a .NET 10 development workstation)

- Unit: management-subnet allowlist, ARM config sanitization, conservative AP cluster parsing, supported device model set.
- Integration: ASP.NET Core `/healthz`, demo inventory, disabled execution endpoint, Blazor route availability.
- Interactive operator acceptance: device registration, read-only probe results, location hierarchy, wireless and switch status, Analyze Now, schedule, audit.
- Lab device certification: AP-505/AP-535 actual TLS/API auth, CX 6100/6200 actual API readback; manual radio/client validation where required.

## Mandatory release gate

Run `scripts/TEST_WINDOWS.ps1` or `scripts/TEST_LINUX.sh` on a .NET 10 workstation and document full passing results before live pilot. Do not treat source-level checks as an end-to-end run.

## Security gate

No AP/switch configuration writes are included; `EnableDeviceWrites=true` prevents startup. Credentials are environment-only. TLS is validated against PKI or per-device pinned certificate, never bypassed globally. Demo authentication binds to loopback only. Production Entra authentication and SQL availability are required.
