# Implementation status

Feature baseline: 2026-10-14 for 0.2.0-rc.1. Rebuilt and tested for source publication on 2026-11-25; see [current validation](VALIDATION.md). This checklist describes actual functionality rather than the intended final product.

## Available

- WPF dark/light theme, grouped navigation, responsive scrolling, virtualized device grid, global text filtering and inspector.
- Live and explicitly synthetic demo workspaces, with persistence rejection of synthetic devices.
- IPv4 adapter/subnet detection, CIDR validation, `/31` and `/32`, lazy bounded host enumeration, ICMP discovery and TCP fallback.
- Bounded concurrency, a bounded results channel, UI result batching, throttled progress and cancellation.
- Local Windows ARP resolver, reverse DNS, configurable TCP port scanning, HTTP/RTSP/SSH response identification and confidence labels.
- WS-Discovery multicast on a selected IPv4 adapter, bounded XML parsing with DTDs disabled, advertised ONVIF name/model and endpoint provenance.
- SQLite observations, transactional device/event updates, session history, persisted notes/tags/favorites, settings and rotating logs.
- Address reuse detection without retaining the previous MAC's identity, scoped port disappearance events.
- Explicit LLDP/CDP/FDB evidence correlation, interactive map pan/zoom/node dragging/reset, PNG export. Live collection of these neighbor protocols is still missing.
- IPAM observation view that does not label unanswered addresses free.
- Reachability monitoring and availability events, requiring three missed polling rounds before offline status.
- Basic exposure review for cleartext and remote management ports. These are configuration findings, not vulnerability claims.
- CSV, JSON, text and printable HTML inventory exports; CLI scanning.
- Unit and loopback integration regression tests. No user LAN scan is needed to validate these tests.

## Deployment additions

- Complete supplied Empiro logo, cat Windows icon, and release metadata.
- .NET 10 LTS self-contained Windows x64 distribution and WiX MSI packaging.
- Start menu shortcuts and uninstall registration; user data remains outside the installation folder.
- Versioned inventory schema, integrity check, interrupted-scan journal recovery and single-instance workspace lock.

## Not yet implemented

- SNMP v1/v2c/v3 transport, authenticated provider UI, MIB walks, vendor OIDs, LLDP/CDP/FDB collection and interface-level topology.
- Windows IPv4/IPv6 route table parsing, reachable subnet suggestions, multi-target scanning, IPv6 discovery, VRF and VLAN collection.
- OUI database, broader manufacturer/OS fingerprint rules, mDNS/SSDP/NetBIOS/DHCP collection, passive capture.
- Authenticated ONVIF device information/profiles, NVR relationships, safe camera configuration, vendor discovery protocols and live RTSP playback.
- Pause/resume scheduling, adaptive packet rate, retry controls and large-network performance validation.
- Persistent map layouts, all map groupings, SVG/PDF/GraphML exports, accessible map node keyboard interaction.
- DHCP leases, DNS record type queries, reservations and multi-address asset identity reconciliation.
- Certificate checks, vulnerability intelligence/CPE, desktop notifications, full scan-to-scan comparison.
- Nmap integration, AD, cloud, virtualization and plugin loading/trust UI.
- Full first-run wizard, complete context menu, column chooser, export report types beyond inventory.
- Publisher signing, upgrade qualification, hardware-in-the-loop camera/switch testing and production deployment validation.

## Known constraints

- Inventory currently keys assets by IP; it does not yet unify a multi-homed device across addresses. A changed MAC is a possible conflict or address reuse, not conclusive simultaneous duplication.
- Service probes read a bounded initial response; partial TCP reads may reduce identification confidence. Unknown results are preserved.
- Local `SendARP` is synchronous Windows API work on background workers; in-flight calls can delay cancellation.
- Reachability monitoring is sequential and intended for small inventories in this build.
- ONVIF multicast availability depends on the local firewall, interfaces and devices. An isolated camera network remains unreachable without appropriate connectivity.
- Startup and theme/layout have been rendered locally. Real switch/camera interoperability, monitor longevity, very large inventories, and an installer have not been qualified.

The remaining phases must be completed and validated before describing Empiro as production-ready.
