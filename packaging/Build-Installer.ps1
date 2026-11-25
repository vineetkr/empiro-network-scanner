param([string]$ProductVersion = '0.2.1', [switch]$SkipPublish)
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot
Set-Location $root
$sdk = Join-Path $root '.dotnet\dotnet.exe'
$env:DOTNET_ROOT = Join-Path $root '.dotnet'
$env:DOTNET_CLI_HOME = Join-Path $root '.dotnet-home'
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$wix = Join-Path $root '.tools\wix\wix.exe'
$publish = Join-Path $root 'artifacts\release\win-x64'
$dist = Join-Path $root 'dist'
if ($ProductVersion -notmatch '^\d+\.\d+\.\d+$') { throw 'MSI version must contain three numeric parts.' }
if (-not (Test-Path -LiteralPath $wix)) { throw 'Install the pinned compiler: .\.dotnet\dotnet.exe tool install wix --version 5.0.2 --tool-path .tools\wix' }
if (-not (Test-Path -LiteralPath (Join-Path $root '.wix\extensions\WixToolset.UI.wixext\5.0.2\wixext5\WixToolset.UI.wixext.dll'))) { throw 'Install the pinned UI extension: .\.tools\wix\wix.exe extension add WixToolset.UI.wixext/5.0.2' }
if (-not $SkipPublish) {
    & $sdk publish Empiro.App\Empiro.App.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=false -p:PublishTrimmed=false -p:DebugType=None -p:DebugSymbols=false -p:UseSharedCompilation=false -m:1 -o $publish
    if ($LASTEXITCODE) { throw 'Self-contained publication failed.' }
}
if (-not (Test-Path (Join-Path $publish 'coreclr.dll'))) { throw 'Self-contained runtime is missing.' }
Copy-Item -LiteralPath (Join-Path $root 'docs\DEPLOYMENT.md') -Destination (Join-Path $publish 'DEPLOYMENT.md') -Force
Copy-Item -LiteralPath (Join-Path $root 'docs\STATUS.md') -Destination (Join-Path $publish 'FEATURE-STATUS.md') -Force
Copy-Item -LiteralPath (Join-Path $root 'Empiro.App\Assets\Empiro-logo.jpeg') -Destination (Join-Path $publish 'Empiro-logo.jpeg') -Force
$notices = Join-Path $publish 'ThirdPartyNotices'
New-Item -ItemType Directory -Force $notices | Out-Null
$runtimePackages = @(
    @{ Id = 'microsoft.netcore.app.runtime.win-x64'; Prefix = 'dotnet-runtime' },
    @{ Id = 'microsoft.windowsdesktop.app.runtime.win-x64'; Prefix = 'windows-desktop-runtime' }
)
foreach ($runtime in $runtimePackages) {
    $packageRoot = Join-Path $root ".dotnet-home\.nuget\packages\$($runtime.Id)"
    $versionDir = Get-ChildItem -LiteralPath $packageRoot -Directory | Sort-Object Name -Descending | Select-Object -First 1
    if (-not $versionDir) { throw "Runtime notices package missing: $($runtime.Id)" }
    foreach ($notice in Get-ChildItem -LiteralPath $versionDir.FullName -File | Where-Object { $_.Name -match '^(LICENSE|THIRD-PARTY-NOTICES)' }) {
        Copy-Item -LiteralPath $notice.FullName -Destination (Join-Path $notices "$($runtime.Prefix)-$($notice.Name)") -Force
    }
}
$deps = Get-Content -LiteralPath (Join-Path $publish 'Empiro.deps.json') -Raw | ConvertFrom-Json
$packageLines = @('Empiro third-party NuGet dependencies', 'Package | License expression | Project URL', '--- | --- | ---')
foreach ($package in $deps.libraries.PSObject.Properties | Where-Object { $_.Value.type -eq 'package' } | Sort-Object Name) {
    $parts = $package.Name.Split('/')
    $nuspec = Join-Path $root ".dotnet-home\.nuget\packages\$($parts[0].ToLowerInvariant())\$($parts[1])\$($parts[0].ToLowerInvariant()).nuspec"
    if (-not (Test-Path -LiteralPath $nuspec)) { throw "Dependency metadata missing: $($package.Name)" }
    [xml]$metadata = Get-Content -LiteralPath $nuspec
    $license = $metadata.package.metadata.license
    if ($license.type -ne 'expression' -or -not $license.InnerText) { throw "Review license metadata for $($package.Name)" }
    $packageLines += "$($package.Name) | $($license.InnerText) | $($metadata.package.metadata.projectUrl)"
}
$packageLines | Set-Content -LiteralPath (Join-Path $notices 'PACKAGE-LICENSES.txt') -Encoding utf8
New-Item -ItemType Directory -Force $dist | Out-Null
$installer = Join-Path $dist "Empiro-$ProductVersion-rc.1-win-x64.msi"
& $wix build packaging\Empiro.wxs packaging\EmpiroUI.wxs -ext WixToolset.UI.wixext/5.0.2 -culture en-us -arch x64 -d "ProductVersion=$ProductVersion" -d "PublishDir=$publish" -d "IconPath=$root\Empiro.App\Assets\Empiro.ico" -o $installer
if ($LASTEXITCODE) { throw 'MSI compilation or validation failed.' }
$hash = Get-FileHash -LiteralPath $installer -Algorithm SHA256
"$($hash.Hash.ToLowerInvariant())  $([IO.Path]::GetFileName($installer))" | Set-Content -LiteralPath (Join-Path $dist 'SHA256SUMS.txt') -Encoding ascii
Copy-Item docs\DEPLOYMENT.md $dist\DEPLOYMENT.md -Force
Get-Item -LiteralPath $installer | Select-Object FullName,Length
