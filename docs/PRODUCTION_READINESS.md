# Production-readiness disposition — Release 0.6

**Status: NOT CERTIFIED FOR PRODUCTION.** This is real application source code with operational modules, not a screen-only mockup. However, source completeness is not equivalent to completed SRS acceptance or Aruba Central feature parity. Do not place it in charge of live JCF APs or switches until the following gates are passed.

## Non-negotiable acceptance gates

1. **Compiled build and integration tests** — .NET 10 SDK and NuGet packages are absent from the generation runtime. Build with approved .NET 10 SDK, run all xUnit tests, generate TRX and correct any compilation/runtime defects. Static lexical checks are not compiled tests.
2. **Database lifecycle** — create and peer-review an EF Core initial migration, apply to SQL Server 2019 Express pilot and SQL Server Enterprise AG test environment, verify backups, restore, migrations, workload, isolation and retention. Never auto-apply DDL on live AG hosts.
3. **Authentication/security** — verify local Identity, strong-password lockout, account recovery, session protection, CSRF, privilege boundaries, site permissions, application/database ACLs, certificate pinning, credential rotation, systemd confinement and external penetration test. Location-scoped RBAC is not fully enforced across all views/routes; this alone blocks production certification.
4. **AP model certification** — live AP-505 and AP-535 tests on actual firmware. Validate cluster/VC owner, session management, all vendor API response variants, radios/clients, actual configuration changes and rollback on isolated equipment.
5. **AOS-CX model certification** — CX6100/CX6200 REST version/CSRF modes, VSF, actual port/VLAN/PoE/QoS read and write, protecting uplinks/management, timeout and recovery tests.
6. **Client end-to-end proof** — actual Wi-Fi client agent for association, 802.1X/RADIUS, correct VLAN, DHCP, gateway, DNS, app, roaming, RSSI/retries; no device API can prove these with zero associated clients.
7. **Optimization governance** — trigger Analyze Now/hourly over real telemetry, persistent detection, no duplicate recommendations, separate independent human approvals, maintenance windows, exact target/payload and safe, verified compensation after failures.
8. **Scale and DR** — >=500 AP, >=500 switch and >=10k client sample profiles; SQL and API response budget; 99.9% availability target, RTO<=1h/RPO<=15m; backup/restore and DR test evidence.
9. **Operational deployment** — TLS proxy, network ACLs, service account, protected secrets, installation/upgrade verification, monitoring, security review, SIEM and incident procedures.
10. **Regulatory/client compatibility** — 20/40 MHz only, 2.4 GHz channels 1/6/11; authorized non-DFS 5 GHz pool with 165 only for 20 MHz; final domain/channel list must be confirmed against local AP regulatory domain.

## Explicitly not yet delivered or certified

- Whole-fleet production write gateway and cluster-aware scope deployment. The only device-write implementations are restricted lab methods with write/readback and attempted recovery. New ClientMatch, channel pool, port VLAN and PoE actions are *proposals only* until model-specific certification.
- Full 802.1X/RADIUS/guest/AAA feature parity with Aruba Central, WPA3 per firmware certification, client roaming steering guarantees, central firmware lifecycle, captive portal, analytics telemetry completeness or licensed Central-only APIs.
- Multi-node durable job locks, cross-site role-grant enforcement in all Blazor views, full system security hardening, complete client diagnostics and approved on-site test evidence.
- True ML training/inference. Included shadow scoring is explicitly heuristic and non-authoritative.

## Documentation/evidence discipline

Maintain a signed acceptance record for each SRS module and each model/firmware/API combination, with baseline/readback/restore, operator approval, execution logs and RF/client health outcomes. No simulated test should appear as a real device certification.

## Additional release 0.6 source audit notes

- API mutation endpoints protected by same-origin `X-CSRF-TOKEN` validation on production cookie sessions. Anti-forgery tests on a real HTTPS deployment remain outstanding.
- AOS-CX REST login updated to the HPE documented 10.09+ CSRF session pattern; the credential-bearing URI must not be logged at any HTTP proxy. No physical CX login verification is claimed.
- Polling groups known VC members serially and limits concurrent clusters. Unclassified Instant APs use per-device polling keys. Multi-node coordination and performance benchmarks remain outstanding.
- Release 0.8.2 adds site-scoped authorization checks throughout core API, browser read queries and write services; child-location inheritance is implemented. Detailed cluster member identities/global audit remain admin-only because those records lack complete site ownership. **Production validation against compiled .NET, live SQL and real role/site integration is still required before issuing scoped operator accounts.**
