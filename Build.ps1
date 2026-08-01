$ErrorActionPreference = 'Stop'
$projectRoot = $PSScriptRoot
$localDotnet = Join-Path $projectRoot '.dotnet\dotnet.exe'
$dotnetCommand = if (Test-Path -LiteralPath $localDotnet) { $localDotnet } else { 'dotnet' }
$env:DOTNET_CLI_HOME = Join-Path $projectRoot '.dotnet-home'
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE = '1'

& $dotnetCommand restore (Join-Path $projectRoot 'TerrainBuilder.sln')
& $dotnetCommand build (Join-Path $projectRoot 'TerrainBuilder.sln') --configuration Release --no-restore
& $dotnetCommand test (Join-Path $projectRoot 'tests\TerrainBuilder.Tests\TerrainBuilder.Tests.csproj') --configuration Release --no-build
