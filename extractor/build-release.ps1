param([string]$Output = (Join-Path $PSScriptRoot 'publish'))
$ErrorActionPreference = 'Stop'
$artifacts = Join-Path $PSScriptRoot 'artifacts'
New-Item -ItemType Directory -Force $Output, $artifacts | Out-Null
$projects = @('UI/UI.csproj', 'Service/Service.csproj', 'Worker/Worker.csproj', 'Tests/Tests.csproj')
$summary = @()
foreach ($project in $projects) {
    $name = ($project -split '/')[0]
    $log = Join-Path $artifacts ("publish-" + $name + '.log')
    & dotnet publish (Join-Path $PSScriptRoot $project) -c Release -r win-x64 --self-contained true -p:PublishSingleFile=false -p:DebugType=None -p:DebugSymbols=false -o $Output 2>&1 | Tee-Object -FilePath $log
    $code = $LASTEXITCODE
    $summary += "$name exit=$code"
    if ($code -ne 0) { throw "Publish failed: $project exit=$code" }
}
$summary | Set-Content -Encoding UTF8 (Join-Path $artifacts 'publish-exit-codes.txt')
Copy-Item (Join-Path $PSScriptRoot 'config.example.json') (Join-Path $Output 'config.example.json') -Force
Copy-Item (Join-Path $PSScriptRoot 'README.md') (Join-Path $Output 'README.md') -Force
Get-ChildItem $Output -File | ForEach-Object {
    [PSCustomObject]@{Name=$_.Name; Bytes=$_.Length; SHA256=(Get-FileHash -Algorithm SHA256 $_.FullName).Hash}
} | ConvertTo-Json -Depth 3 | Set-Content -Encoding UTF8 (Join-Path $artifacts 'published-files.json')
Write-Host 'Published all four executables. No service installed or startup registered.'
