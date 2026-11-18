# Build a standalone Windows x64 executable; .NET is not needed on the target PC.
$ErrorActionPreference = "Stop"
$dotnet = "dotnet"
$local = Join-Path $env:USERPROFILE ".dotnet\dotnet.exe"
if (Test-Path $local) {
    $dotnet = $local
    $env:DOTNET_ROOT = Split-Path $local
}
& $dotnet publish "$PSScriptRoot\src\PixelArt.App" -c Release -r win-x64 --self-contained true `
    -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:DebugType=None -p:DebugSymbols=false -o "$PSScriptRoot\artifacts\PixelArt"
exit $LASTEXITCODE
