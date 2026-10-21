# Dot-source in the current terminal: . ./infrastructure/scripts/Use-LocalTools.ps1
# This script downloads nothing and makes no persistent machine changes.
$projectRoot = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
$portableDotnet = Join-Path $projectRoot '.tools/dotnet'
if (Test-Path -LiteralPath (Join-Path $portableDotnet 'dotnet.exe')) {
    $env:DOTNET_ROOT = $portableDotnet
    $env:DOTNET_CLI_HOME = Join-Path $projectRoot '.tools/dotnet-home'
    $env:NUGET_PACKAGES = Join-Path $projectRoot '.tools/nuget'
    $env:PATH = $portableDotnet + [IO.Path]::PathSeparator + $env:PATH
}
# Use the existing real Node binary when NVM's shim has no active version.
$existingNode = Join-Path $env:LOCALAPPDATA 'Author Software/nvm/installs/v22.23.2'
if (Test-Path -LiteralPath (Join-Path $existingNode 'node.exe')) {
    $env:PATH = $existingNode + [IO.Path]::PathSeparator + $env:PATH
}
$localCli = Join-Path $projectRoot 'frontend/haqpos-web/node_modules/.bin'
$env:PATH = $localCli + [IO.Path]::PathSeparator + $env:PATH
$env:VP_HOME = Join-Path $projectRoot '.tools/vite-plus'
$env:NPM_CONFIG_CACHE_DIR = Join-Path $projectRoot '.tools/pnpm-cache'
$env:NPM_CONFIG_STORE_DIR = Join-Path $projectRoot '.tools/pnpm-store'
