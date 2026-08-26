$ErrorActionPreference = 'Stop'
$name = 'FlexRequestExtractor'
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$exe = Join-Path $root 'FlexRequestExtractor.Service.exe'
if (-not (Test-Path -LiteralPath $exe)) { throw "Missing $exe" }

$svc = Get-Service -Name $name -ErrorAction SilentlyContinue
if ($svc) {
  if ($svc.Status -ne 'Stopped') {
    Stop-Service -Name $name -Force -ErrorAction SilentlyContinue
    Start-Sleep -Seconds 2
  }
  sc.exe delete $name | Out-Null
  Start-Sleep -Seconds 1
}

New-Service -Name $name -BinaryPathName ('"{0}"' -f $exe) -DisplayName 'Flex Request Extractor' -StartupType Automatic | Out-Null
sc.exe failure $name reset= 86400 actions= restart/5000/restart/5000/restart/5000 | Out-Null
Write-Host '[OK] service installed (Automatic)'
Write-Host ('exe=' + $exe)
Write-Host 'start: sc start FlexRequestExtractor'
Write-Host 'Use a Log On account that can read the UNC share. LocalSystem cannot see mapped drives.'
