# SRS implementation matrix — Release 0.1.0

**Legend:** STARTED = working implementation with remaining hardening; PARTIAL = basic scaffolding/UI; FUTURE = not yet implemented. No module here is certified production-ready.

| Module | SRS feature | Status | Next acceptance gate |
|---|---|---|---|
| M01 | Identity/RBAC/platform | PARTIAL | Entra role+scope matrix, no self-approval, security tests |
| M02 | Locations/groups/zones | STARTED | Effective-dated placement, bulk import, group associations |
| M03 | Device registration/inventory | STARTED | Serial/MAC uniqueness, live discovery, 4 model certification |
| M04 | Native connectors/capabilities | STARTED | Per-model read/write/restore certification evidence |
| M05 | Instant cluster/VC topology | PARTIAL | Complete multi-AP member parsing and blast-radius test |
| M06 | SSID/security/AAA | FUTURE | Approval-bound create/edit/delete and readback |
| M07 | Wireless VLAN/service path | FUTURE | Cross-API VLAN/port/DHCP validation |
| M08 | RF/ARM config | PARTIAL | 20/40 MHz policy, approved RF write/restore |
| M09 | AP/radio monitoring | PARTIAL | Numeric RF metrics with source/units |
| M10 | Wireless client experience | FUTURE | Actual clients, retries, roaming, privacy controls |
| M11 | CX identity/VSF | PARTIAL | Stack discovery, LLDP, member topology |
| M12 | CX VLAN/ports/PoE | PARTIAL | Lab write/readback/recovery, roles, approvals |
| M13 | CX QoS/L2 security | FUTURE | Capability matrix and approved policy |
| M14 | CX telemetry/congestion | PARTIAL | Rate/counter normalization and history |
| M15 | Service-path diagnostics | FUTURE | DHCP/DNS test station integration |
| M16 | Security/anomaly signals | FUTURE | Auth events, IDS data and RBAC |
| M17 | Telemetry/historian | PARTIAL | Retention, rollups, provenance, partitions |
| M18 | Backup/templates/drift | FUTURE | Encrypted snapshots, compare/restore |
| M19 | Rules/recommendations | STARTED | Rule evidence confidence, suppression/deduplication |
| M20 | Analyze Now/hourly schedule | STARTED | Durable job locking, scopes and queuing |
| M21 | Recommendation approvals | PARTIAL | Independent approvers, signed/immutable payload binding |
| M22 | Approved execution/recovery | FUTURE — disabled | Certified safe gateway and approved lab recovery |
| M23 | Incidents/alerts/escalation | FUTURE | Correlation, routing, notifications |
| M24 | Dashboards/reports | PARTIAL | Location-scoped views, exports, kiosk mode |
| M25 | Predictive analytics/ML | FUTURE | Shadow mode first; never direct write access |
| M26 | Ops/DR/test harness | PARTIAL | SQL migrations, HA/DR, systemd, live E2E tests |

## Evidence already available from user's laboratory testing

- AP-505 InstantOS 8.10.0.21: native REST authentication, readback, SSID/VLAN write+cleanup, ARM airtime fairness, band steering and minimum power reversible writes passed on a lab unit in earlier tools.
- ClientMatch write was not confirmed; restoring the baseline was reported successful.
- No AP-535 or CX 6100/6200 live JSON report proving all desired writes was supplied for this release.
- RF channel 80 MHz policy was historically enabled as a device capability, which is inconsistent with the required 20/40 MHz-only operational baseline.

## Recommended release acceptance sequence

1. Build and run xUnit tests; run demo on localhost.
2. Configure staging SQL 2019 Express, Entra app roles and certificate trust.
3. Register one AP-505 (lab) using environment-secret reference; check TLS/auth, sanitize response, verify ARM read-only snapshot.
4. AP-535 in Instant 8 mode: read all supported APIs and check actual cluster member parsing; never assume parity.
5. Connect CX 6100 and CX 6200; verify JSON inventory and active REST ownership.
6. Verify analysis suggests 80 MHz-policy compliance correction but does not call a device write.
7. Verify scheduled hourly analysis generates recommendations but never authorizes them.
8. Test 403/role boundaries, encrypted secrets, TLS failure, timeouts, database loss and recovery.
9. Only after documented write/readback/restore tests and change approval, introduce the execution module in a separate sprint.
