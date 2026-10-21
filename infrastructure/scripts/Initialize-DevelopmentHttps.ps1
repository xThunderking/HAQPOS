param([switch]$Trust)
$ErrorActionPreference = 'Stop'
$projectRoot = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
$certificateDirectory = Join-Path $projectRoot '.tools/certs'
New-Item -ItemType Directory -Path $certificateDirectory -Force | Out-Null
$arguments = @('dev-certs', 'https', '--export-path', (Join-Path $certificateDirectory 'localhost.pem'), '--format', 'PEM', '--no-password')
if ($Trust) { $arguments += '--trust' }
& dotnet @arguments
if ($LASTEXITCODE -ne 0) { throw 'No se pudo preparar el certificado HTTPS de desarrollo.' }
Write-Host 'Certificado local exportado. Reinicia vp dev. Los archivos privados están excluidos de Git.'
