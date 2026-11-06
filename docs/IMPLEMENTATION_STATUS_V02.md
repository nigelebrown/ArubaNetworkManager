# Implementation matrix — JCF SRS M01–M26

Baseline: `JCF_Intelligent_Network_Management_SRS_v1.0(1).docx`, JCF-ICT-NMO-SRS-001. Status means source-level functionality; none is production-certified until compiled, integrated and accepted.

| SRS module | Current status in Release 0.2 | Outstanding production acceptance |
|---|---|---|
| M01 Identity / roles / platform | Partial: Entra OIDC wiring, roles, basic audit | End-to-end Entra, scoped authorization, MFA, security audit |
| M02 Locations / groups | Functional starter: hierarchy, admin groups, membership | Effective dating, bulk import, zone/building reconciliation |
| M03 Discovery / inventory | Functional starter: IP, firmware, probe, device status | Serial-based dedup, IP range discovery, replacements |
| M04 Native connectors / capabilities | Instant/CX read adapters; field-level certification UI | Per-model/firmware live certifications and capability manifest signatures |
| M05 Instant cluster topology | Tentative members and cluster-count guards | Multi-AP identity/VC correctness and verified blast-radius writes |
| M06 SSIDs / AAA | SSID/VLAN proposal and lab-gated WPA2 create/readback | Full edit/delete, Enterprise/RADIUS/WPA3, multi-AP safe deployment |
| M07 WLAN VLAN/service | VLAN binding included in lab create | Switch trunk path, DHCP, DNS, gateway and physical clients |
| M08 ARM/RF | Basic ARM proposal, lab min-power/band-steering, 20/40 MHz and DFS guard | Broad parameters, ClientMatch, per-radio scope, regulatory approval |
| M09 AP health | Poll and labeled RF fields with source quality | Certified numeric fields and long-term rate processing |
| M10 Clients/experience | Client count/field availability, missing-data indicators | Secure session history, actual RSSI/SNR, roaming agent |
| M11 CX discovery / VSF | Switch VLAN/port inventory | Verified VSF/LLDP member topology |
| M12 CX VLAN / ports / PoE | Create-VLAN proposal and gated REST lab adapter | Port access/trunk, PoE/backup/rollback, device-specific proof |
| M13 QoS/access policies | Not implemented | Full module and model-specific testing |
| M14 Switch monitoring | API inventory/basic counts | Port counters, deltas, PoE, alert rules |
| M15 Service diagnostics | Not implemented | Synthetic Wi-Fi station/DHCP/DNS/app traces |
| M16 Security events | Not implemented | IDS/syslog/RADIUS and security posture |
| M17 Telemetry historian | Initial poll snapshots and data quality | Durable ingestion, rollups, retention, queue backpressure |
| M18 Snapshots/templates/drift | Basic policy findings | Full encrypted config snapshot, diff, templates, restore certification |
| M19 Recommendation rules | 80 MHz/DFS/drift, missing clients, sustained utilization | Multi-metric root cause and outcome scoring |
| M20 Analyze Now/hourly | Initial working functions | Multi-node scheduler lock, scope controls, dedup at scale |
| M21 Approval workbench | Independent approval and two reviewers for ARM | Location/risk scope, approval expiration UX, revocation, dual risk rules |
| M22 Apply/verify/recover | Demo simulation and restricted lab gateway, startup reconciliation | Production execution worker, durable queue, complete recovery playbooks |
| M23 Incidents/alerts | API outage and critical recovery alerts | Escalation, maintenance windows, Teams integration |
| M24 Dashboards/reports | Operator views and 10-second rotating kiosk | Report exports, performance, full RBAC scoping |
| M25 ML predictions | Not implemented | Offline models, shadow evaluations, governance |
| M26 Ops/DR/test harness | Static checks, xUnit source, Windows smoke, docs | Linux systemd installer, production SQL migrations, AG/DR, load/security testing |

**Production verdict: NOT READY.** This is an expanded development implementation, not a completed or fully end-to-end tested 26-module production system.
