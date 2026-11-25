# User guide and interview demo

## Offline presentation

Build the solution and run:

```powershell
dotnet run --project Empiro.App -c Release --no-build -- --preview
```

The demo contains 14 fictional devices, including four cameras, one recorder, a switch, gateway, access point, workstations and other examples. One sample camera is shown offline. The visible demo label applies to all identity, service and link evidence. No office network is needed.

## Five-minute walkthrough

1. **Dashboard:** explain overview cards, inventory and the explicit synthetic-data label.
2. **CCTV Discovery:** show camera/recorder filtering and distinguish an advertisement from authenticated camera information.
3. **Network Map:** demonstrate relationships and explain that the demo links are synthetic evidence, not measurements of an office network.
4. **IPAM:** explain observed versus unobserved addresses. An unanswered address is not automatically available for assignment.
5. **Advanced Scan:** explain target limits, selected ports, concurrency and timeout. Scanning is disabled while the demo is active.

Select a device to inspect evidence and edit demo notes/tags/favorites. Exported demo reports are explicitly named as demo inventory. The demo does not show real camera footage or authenticate to cameras.

## Live workflow

Launch without --preview to use the normal inventory. Choose an active IPv4 adapter; use Scan My Network, Find Cameras or Advanced Scan. Start only on authorized targets. Cancel when required, review identification confidence and inspect observations before acting on results. Use notes, tags and favorites to organize devices. Export from the application when a report is needed.

Reachability monitoring evaluates known devices and reports availability changes; three failed rounds are required before an offline observation. Firewall changes can still affect results. Web actions are available only for appropriate live assets.

## Data locations

- Normal launch: `%LOCALAPPDATA%\Empiro`.
- Preview launch: `%LOCALAPPDATA%\Empiro-NetworkScanner-Demo`.
- Screenshot render: `<output-folder>/preview-data` (ignored by Git).

Exit demo changes the active mode explicitly; a preview-launched process keeps its separate workspace directory. Relaunch normally to return to your normal saved inventory. Close the application before backing up SQLite data, including its WAL/SHM side files.

## Limits and troubleshooting

Check adapter selection, routing, local firewall and multicast reachability when discovery is incomplete. Camera discovery across VLANs is not guaranteed. No SNMP transport, live LLDP collection, authenticated ONVIF profiles, live RTSP playback, IPv6 scanning or proprietary vendor discovery is implemented. Consult STATUS.md before demonstrating a feature implied by a navigation label.

If startup reports another instance, close the application using that workspace. If the runtime is missing, use `dotnet run` with the required SDK or build a self-contained distribution. No network scan occurs automatically on startup.
