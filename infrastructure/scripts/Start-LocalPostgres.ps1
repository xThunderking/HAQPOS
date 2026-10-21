param([switch]$Stop)
$ErrorActionPreference = 'Stop'
$projectRoot = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
$control = Join-Path $projectRoot '.tools/postgresql/pgsql/bin/pg_ctl.exe'
$dataDirectory = Join-Path $projectRoot '.tools/postgresql/data'
if (!(Test-Path -LiteralPath $control) -or !(Test-Path -LiteralPath (Join-Path $dataDirectory 'PG_VERSION'))) {
    throw 'No existe el PostgreSQL portable local. Consulta docs/DEVELOPMENT.md para usar PostgreSQL 18 nativo o Docker.'
}
if ($Stop) {
    & $control -D $dataDirectory -m fast -w stop
} else {
    & $control -D $dataDirectory status *> $null
    if ($LASTEXITCODE -eq 0) { Write-Host 'PostgreSQL portable ya está activo.'; return }
    & $control -D $dataDirectory -l (Join-Path $projectRoot '.tools/postgresql/server.log') -o '-h 127.0.0.1 -p 55432' -w start
}
if ($LASTEXITCODE -ne 0) { throw 'No se pudo cambiar el estado del PostgreSQL portable.' }
