# Build, test, screenshots and releases

## Prerequisites

Use Windows x64 with .NET SDK 10.0.402 (or a compatible patch permitted by global.json). NuGet dependencies are pinned in project files. Restore requires access to package sources unless the required versions are cached.

```powershell
./build.ps1
# Optional existing SDK:
./build.ps1 -Dotnet 'C:\path\to\dotnet.exe'
```

The script restores, builds Release and runs Empiro.Tests. Add -Publish to generate a self-contained Windows x64 application in artifacts/release/win-x64.

## Five reproducible images

```powershell
dotnet run --project Empiro.App -c Release --no-build -- --render docs/screenshots
```

The renderer loads synthetic data and captures the actual WPF client content without scanning. It creates dashboard.png, cctv-discovery.png, network-map.png, ipam.png and advanced-scan.png. Its preview-data folder contains temporary database/log state and must not be committed. Images show the application UI rather than an operating-system window frame.

## Validation

See VALIDATION.md for current build/test results. Automated checks do not establish switch/camera compatibility, monitoring endurance or large-network performance. No real office network was available for this publication.

## Historical MSI pipeline

packaging/Build-Installer.ps1 expects the SDK at .dotnet/dotnet.exe, WiX 5.0.2 at .tools/wix/wix.exe, and the matching UI extension in .wix/extensions. These tools are intentionally excluded from source control. The original pipeline also expects restored package metadata under .dotnet-home/.nuget/packages. Set up that layout before using this historical script; the ordinary build command supports an externally installed SDK.

```powershell
./.dotnet/dotnet.exe tool install wix --version 5.0.2 --tool-path .tools/wix
./.tools/wix/wix.exe extension add WixToolset.UI.wixext/5.0.2
./packaging/Build-Installer.ps1
```

Application metadata currently uses 0.2.0-rc.1 and the historical MSI script defaults to 0.2.1. Select coherent new application/installer version numbers before creating a new binary release. The deployment guide describes the older installer; publication of source does not mean a new installer was built or qualified.

Attach installers and checksums to GitHub Releases. Preserve GPL corresponding-source availability and dependency notices, and verify packaging and signatures before distributing binaries. Do not commit SDKs, databases, credentials, logs or existing installation artifacts.

## Contribution expectations

Keep provider implementation behind existing contracts where possible. Add targeted regression tests for parser, correlation, persistence and report changes. Use loopback or synthetic fixtures in automated checks; live tests require an explicitly authorized network. Update feature status when implementing a previously missing capability.
