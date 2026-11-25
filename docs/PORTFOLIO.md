# Empiro Network Scanner — portfolio case study

## Overview

Empiro Network Scanner is a modular Windows desktop project for network/CCTV discovery, inventory organization, evidence inspection and reporting. It offers a broader management workflow than the separate Empiro IP Scanner project.

**Primary language:** C#. **Framework:** .NET 10/WPF. **Interface:** XAML. **Supporting tools:** PowerShell and WiX. The solution uses MVVM-style view models, dependency injection, SQLite and structured logging.

## Engineering evidence

| Area | Where to look |
|---|---|
| Modular responsibilities | Separate Domain, Core, Discovery, CCTV, Inventory and Infrastructure projects |
| Responsive discovery | NativeScanner, bounded result channel, UI batching and cancellation |
| Identification quality | Service detection, evidence/confidence and correlation logic |
| Data integrity | SqliteInventory transactions, schema checks and interrupted-session recovery |
| Operator workflow | ShellViewModel commands, filters, inspector, notes/tags and favorites |
| Reporting safety | HTML encoding and CSV formula-prefix handling |
| Reproducibility | SDK/package versions, automated tests and synthetic screenshot renderer |

## Decisions worth explaining

Multiple probe types improve coverage, but lack of response cannot establish that an address is free. Explicit evidence and confidence labels make this uncertainty visible. Observed relationships are kept separate from assumptions about physical cabling. A bounded pipeline controls workload while progressive UI updates keep the application usable. Synthetic assets are marked and rejected by live persistence to avoid contaminating real inventory.

## Demonstration

Follow USER_GUIDE.md to present the dashboard, CCTV view, map, IPAM and scan settings without an office network. All sample device identities and link evidence are fictional. The screenshots demonstrate UI behavior rather than throughput, deployment scale or real-device compatibility.

## Interview introduction

“Empiro Network Scanner is my C#/.NET desktop network-inventory project. It uses WPF and XAML for the interface, a modular service architecture, SQLite persistence and asynchronous discovery. I can demonstrate evidence-aware inventory, a network map, address observations and reports, and explain the limits of unauthenticated discovery. The offline demo lets me present the workflow without exposing office-network data.”

Discuss only contributions and decisions you can explain. Do not describe roadmap items as shipped functionality or use synthetic latency/counts as performance measurements.

## Resume project entry

**Empiro Network Scanner | C#, .NET 10, WPF, MVVM, SQLite, xUnit**

- Modular Windows network/CCTV inventory project with bounded discovery, evidence inspection, monitoring, topology visualization and multiple export formats.
- Includes an offline synthetic demonstration, five application screenshots, automated regression checks and documented feature boundaries.

## Questions to prepare for

1. How are concurrency, cancellation and UI result batching handled?
2. Why separate domain models, provider contracts and persistence?
3. How does correlation handle a changed MAC at an existing IP?
4. Why are ONVIF advertisements insufficient to prove device compatibility?
5. What prevents synthetic data from entering live inventory?
6. What evidence is needed to draw a network-map link?
7. Which tests are automated and which need actual switches/cameras?
8. What would change before supporting much larger inventories?

## Current limitations

This is an evaluation release candidate. Hardware interoperability, large-network benchmarks, publisher signing and production acceptance testing remain outstanding. See STATUS.md and VALIDATION.md for precise scope and current evidence.
