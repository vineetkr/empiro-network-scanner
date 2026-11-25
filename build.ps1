param([switch]$Publish, [string]$Dotnet = "")
$ErrorActionPreference = 'Stop'
Set-Location $PSScriptRoot
$sdk = if ($Dotnet) { $Dotnet } elseif (Test-Path '.dotnet\dotnet.exe') { Join-Path $PSScriptRoot '.dotnet\dotnet.exe' } else { 'dotnet' }
$env:DOTNET_CLI_HOME = Join-Path $PSScriptRoot '.dotnet-home'
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
& $sdk restore Empiro.sln --disable-parallel -m:1
if ($LASTEXITCODE) { throw 'Package restore failed.' }
& $sdk build Empiro.sln -c Release --no-restore -m:1 -p:UseSharedCompilation=false
if ($LASTEXITCODE) { throw 'Build failed.' }
& $sdk test Empiro.Tests\Empiro.Tests.csproj -c Release --no-build --no-restore
if ($LASTEXITCODE) { throw 'Tests failed.' }
if ($Publish) {
    & $sdk publish Empiro.App\Empiro.App.csproj -c Release -r win-x64 --self-contained true -o artifacts\release\win-x64 -m:1 -p:UseSharedCompilation=false
    if ($LASTEXITCODE) { throw 'Publish failed.' }
}
