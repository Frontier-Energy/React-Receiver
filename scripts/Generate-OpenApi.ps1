param(
    [string]$Configuration = 'Release',
    [string]$DocumentName = 'v1',
    [string]$ProjectPath = 'React-Receiver.csproj',
    [string]$OutputPath = 'infra/apim/openapi.v1.json'
)

$ErrorActionPreference = 'Stop'

$repoRoot = Split-Path -Parent $PSScriptRoot
$projectFile = Join-Path $repoRoot $ProjectPath
$outputFile = Join-Path $repoRoot $OutputPath
$directoryBuildProps = Join-Path $repoRoot 'Directory.Build.props'

if (-not (Test-Path $projectFile)) {
    throw "Project file not found: $projectFile"
}

if (-not (Test-Path $directoryBuildProps)) {
    throw "Directory.Build.props not found: $directoryBuildProps"
}

[xml]$props = Get-Content -Path $directoryBuildProps
$targetFramework = $props.Project.PropertyGroup.TargetFramework
if ([string]::IsNullOrWhiteSpace($targetFramework)) {
    throw 'TargetFramework was not found in Directory.Build.props.'
}

Push-Location $repoRoot
try {
    dotnet tool restore
    if ($LASTEXITCODE -ne 0) {
        throw 'dotnet tool restore failed.'
    }

    dotnet build $projectFile --configuration $Configuration
    if ($LASTEXITCODE -ne 0) {
        throw 'dotnet build failed.'
    }

    $assemblyPath = Join-Path $repoRoot "bin\$Configuration\$targetFramework\React-Receiver.dll"
    if (-not (Test-Path $assemblyPath)) {
        throw "Compiled assembly not found: $assemblyPath"
    }

    $outputDirectory = Split-Path -Parent $outputFile
    if (-not (Test-Path $outputDirectory)) {
        New-Item -ItemType Directory -Path $outputDirectory | Out-Null
    }

    dotnet swagger tofile --output $outputFile $assemblyPath $DocumentName
    if ($LASTEXITCODE -ne 0) {
        throw 'dotnet swagger tofile failed.'
    }
}
finally {
    Pop-Location
}
