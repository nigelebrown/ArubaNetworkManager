# Secure pilot and lab-device operation

**Default:** `Jcf:LiveLabWrites:Enabled=false` and `Jcf:EnableDeviceWrites=false`. Only the first key, which is deliberately `false` in `appsettings.json`, controls the new restricted **lab** execution route. The older `EnableDeviceWrites` key must remain false; setting it true fails application startup. Neither is an authorization substitute.

## Demo
Run `RUN_AND_TEST_WINDOWS.bat` (requires .NET 10 SDK). Demo binds `127.0.0.1:5180`, has a simulated identity, uses an EF in-memory database, uses fake Aruba observations, and the execution adapter never contacts hardware. Approvals are performed by **clearly simulated** independent reviewers in demo only. All demo changes disappear at process restart. Do not treat demo as proof of Aruba functionality.

## Real read-only pilot
Use SQL Server with an appropriate application account; Entra/OIDC configuration is mandatory. Do not commit secret material. Device inventory stores only a credential **reference**. Configure for each `SecretRef`:

```text
JCF_DEVICE_<REF>_USER=<service user>
JCF_DEVICE_<REF>_PASS=<secret from protected machine vault>
JCF_DEVICE_<REF>_CERT_SHA256=<optional pinned device TLS certificate SHA-256>
```

A trusted certificate chain is preferred; if pinned, validate the fingerprint independently. HTTPS is used on AP Instant 8 **4343** and CX **443**. All device management destinations must belong to approved management subnets. For production, replace pilot `EnsureCreated` with EF migrations and reviewed SQL changes.

## Laboratory write path (do not enable on production)
This source includes a tightly restricted lab-only execution adapter to validate the methodology after compiling and peer review. It is **not** a production change engine. It enforces: independent reviewers, two reviewers for cluster-wide ARM; exact SHA-256 approved payload; 24-hour expiry; matching firmware; recent successful device probe; unique certified write capability with evidence; allowlisted device IP; and **single-member** AP cluster (unknown or multi-AP cluster fails closed). Every write has preflight, device readback and a best-effort compensation path. If outcome is uncertain it becomes a critical recovery alert; it is not automatically retried. Switch port and PoE changes are not enabled.

Lab WLAN passphrase references use `JCF_WLAN_<PSK_SECRET_REF>_PASS`. The actual passphrase is never saved in SQL. Rotate lab credentials after testing.

**Approval role:** An actual Entra ChangeApprover must make first approval; a different ChangeApprover is required for ARM actions. A person cannot approve their own proposal. Demo simulated reviewers are never available outside demo.

## Known safety gaps before production
1. Real AP-535 and CX6100/CX6200 write certification, lab field-by-field readback, client DHCP/DNS and actual multi-AP blast radius.
2. Entra **location-scoped** RBAC and enhanced dual-approval for wide-scope switch changes.
3. Reviewed production SQL migrations, secret-vault integration, session hardening and security tests.
4. Durable multi-node execution locks, configuration diff/backup/restore for all supported operations and robust AG/DR validation.
5. No 80/160 MHz profile writes; DFS channel exclusions must be validated against the configured regulatory domain and official firmware behavior.
6. Native Aruba ARM/ClientMatch should remain responsible for fast RF decisions. New automation only recommends changes before approval.

No unattended write activation is recommended with this release.
