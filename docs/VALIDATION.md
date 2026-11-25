# Validation and publication record

Validated on 2026-11-25 from the prepared Empiro Network Scanner source repository.

- .NET SDK: 10.0.402, Release configuration.
- Restore completed successfully.
- Full solution build: zero warnings and zero errors.
- Existing test suite: 29 passed, zero failed, zero skipped.
- Five actual WPF views rendered with 14 synthetic devices: dashboard, CCTV discovery, network map, IPAM and advanced scan. Images were visually inspected.
- Preview launch now uses a separate demo data directory. Rendering stores its temporary state under the screenshot output directory; preview-data is excluded from Git.
- Launcher paths were corrected for .NET 10 and Release builds; build.ps1 accepts an explicit SDK executable.
- Source/documentation reviewed for common credential patterns and machine-specific paths. No credentials were found. This limited check is not a security audit.

## What these checks cover

The regression suite includes IPv4 validation, loopback probing, discovery parsing, observation correlation, synthetic-inventory rejection, reporting, persistence and release-state checks. It does not establish real-camera or switch interoperability, large-network capacity, long-running monitoring reliability or production suitability.

## What remains unverified

No office network was scanned. No new installer was produced or signed for this publication, and the earlier installed-app launch issue has not been diagnosed here. The historical deployment guide applies to an older installer; use the source build and documented demo for this portfolio review.

## Repository

https://github.com/vineetkr/empiro-network-scanner

This is a separate public GPL-3.0-only source repository from Empiro IP Scanner. It includes the source projects, tests, packaging scripts, documentation, branding and five synthetic screenshots. SDKs, caches, local databases, logs, binaries and unrelated projects are excluded.
