$ErrorActionPreference = "Stop"
$dotnet = "dotnet"
$local = Join-Path $env:USERPROFILE ".dotnet\dotnet.exe"
if (Test-Path $local) {
    $dotnet = $local
    $env:DOTNET_ROOT = Split-Path $local
}
& $dotnet build "$PSScriptRoot\PixelArt.sln" -c Release
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
& $dotnet run --project "$PSScriptRoot\tests\PixelArt.Tests" -c Release --no-build
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
& $dotnet run --project "$PSScriptRoot\tests\PixelArt.App.Tests" -c Release --no-build
exit $LASTEXITCODE
