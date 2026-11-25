# Empiro 0.2.1 Release Candidate — Windows installer

Installer 0.2.1 packages application 0.2.0 with a visible setup wizard, completion screen, and corrected Windows Start-menu shortcuts. It upgrades the original 0.2.0 installer.

This is an unsigned internal-evaluation release candidate. The application includes native IPv4 discovery, ONVIF advertisements, inventory, monitoring, map rendering and reports. It does not yet implement all of the original enterprise requirements. See FEATURE-STATUS.md in the installation folder for the exact scope.

## Install

1. Use a Windows 11 x64 computer (build 22000 or newer). ARM64 and Windows Server are not qualified by this package.
2. Close Empiro, double-click `Empiro-0.2.1-rc.1-win-x64.msi`, choose **Next**, then **Install**, and approve the administrator prompt. Choose **Finish** when setup completes.
3. Open **Empiro Network Scanner** from Start. **Empiro Demo** opens synthetic sample data without scanning.

The installer bundles the .NET 10 desktop runtime; no separate runtime download is required. Files install under `C:\Program Files\Empiro`. Nothing starts automatically with Windows. No firewall exceptions or services are installed. Local firewall policy may prevent multicast discovery.

The installer is unsigned because no publisher signing certificate was supplied. Windows or organization policy can show an unknown-publisher warning or block it. Follow your organization's approval process; do not disable endpoint protection. Public distribution and production rollout require publisher signing and real-device acceptance testing.

## Managed installation

Run these commands in an elevated terminal. Adjust the MSI path as appropriate.

```powershell
msiexec.exe /i "Empiro-0.2.1-rc.1-win-x64.msi" /qn /norestart /L*v "Empiro-install.log"
msiexec.exe /x "Empiro-0.2.1-rc.1-win-x64.msi" /qn /norestart /L*v "Empiro-uninstall.log"
```

Exit codes 0 and 3010 indicate success (3010 requests a restart). Check the log for any other exit code. Uninstall is also available from Windows Settings → Apps → Installed apps.

## Data and upgrades

User inventory, notes, settings and logs stay in `%LOCALAPPDATA%\Empiro`. Installation and uninstallation do not remove that directory. Close Empiro before maintenance. Back up the entire data directory while the application is closed, including any SQLite `-wal` and `-shm` files. Restore that directory only while Empiro is closed.

The MSI has a stable upgrade identity and blocks downgrades. Future upgrades must increment the three-part installer version. Signed/public releases have not been produced yet. Do not deploy this release candidate as a replacement for an existing network-management system.

## Verification

Compare the installer with the adjacent `SHA256SUMS.txt` using `Get-FileHash -Algorithm SHA256`. Checksums verify a matching artifact, not publisher identity.

Before production rollout, test camera/switch compatibility, firewall behavior, representative network sizes, scan cancellation, and monitoring on the target network. No network-wide scan is run by the installer or by application startup.
