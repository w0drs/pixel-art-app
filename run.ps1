# Prefer the local SDK if .NET 10 is not installed system-wide.
$ErrorActionPreference = "Stop"
$dotnet = "dotnet"
$local = Join-Path $env:USERPROFILE ".dotnet\dotnet.exe"
if (Test-Path $local) {
    $dotnet = $local
    $env:DOTNET_ROOT = Split-Path $local
}
& $dotnet run --project "$PSScriptRoot\src\PixelArt.App" -c Release -- @args
exit $LASTEXITCODE
