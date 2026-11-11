param([string]$Output = (Join-Path $PSScriptRoot 'run'))
$ErrorActionPreference='Stop'
$checkerOutput=Join-Path $Output 'Checker'
$extractorOutput=Join-Path $Output 'Extractor'
New-Item -ItemType Directory -Path $checkerOutput,$extractorOutput -Force | Out-Null
& dotnet publish (Join-Path $PSScriptRoot 'checker/FlexTimeChecker/FlexTimeChecker.csproj') -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true -p:RuntimeFrameworkVersion=8.0.22 -o $checkerOutput
if($LASTEXITCODE -ne 0){throw 'Checker publish failed'}
foreach($project in @('UI/UI.csproj','Service/Service.csproj','Worker/Worker.csproj','Tests/Tests.csproj')) {
    & dotnet publish (Join-Path $PSScriptRoot ('extractor/'+$project)) -c Release -r win-x64 --self-contained true -p:PublishSingleFile=false -p:RuntimeFrameworkVersion=8.0.22 -p:DebugType=None -p:DebugSymbols=false -o $extractorOutput
    if($LASTEXITCODE -ne 0){throw ('Extractor publish failed: '+$project)}
}
Copy-Item (Join-Path $PSScriptRoot 'extractor/config.example.json') (Join-Path $extractorOutput 'config.example.json') -Force
Write-Output 'BUILD_OK. No service or autostart installed. Runtime pinned to the tested 8.0.22; see security-patch caveat in README.'
