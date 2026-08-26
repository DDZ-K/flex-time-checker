$ErrorActionPreference = 'Stop'
$name = 'FlexRequestExtractor'
$svc = Get-Service -Name $name -ErrorAction SilentlyContinue
if (-not $svc) {
  Write-Host '[OK] service not installed'
  exit 0
}
if ($svc.Status -ne 'Stopped') {
  Stop-Service -Name $name -Force -ErrorAction SilentlyContinue
  Start-Sleep -Seconds 2
}
sc.exe delete $name | Out-Null
Write-Host '[OK] service removed'
