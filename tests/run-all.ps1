# Build all test projects, apply the .mm.dll patch, and regenerate verify.doc.
# Usage: pwsh ./run-all.ps1
$ErrorActionPreference = 'Stop'
$here = Split-Path -Parent $MyInvocation.MyCommand.Path
$sln = Join-Path $here 'MonoModPatchTests.slnx'
Write-Host '[run-all] Building solution...'
dotnet build $sln | Out-Null
Write-Host '[run-all] Running harness (apply patch + verify all scenarios)...'
dotnet run --project (Join-Path $here 'TestHarness/TestHarness.csproj') --no-build
$exit = $LASTEXITCODE
Write-Host "[run-all] Done. exit=$exit"
exit $exit
