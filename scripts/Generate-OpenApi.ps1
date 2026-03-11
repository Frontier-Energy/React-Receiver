param(
    [string]$Configuration = 'Release',
    [string]$DocumentName = 'v1',
    [string]$ProjectPath = 'React-Receiver.csproj',
    [string]$OutputPath = 'infra/apim/openapi.v1.json'
)

$ErrorActionPreference = 'Stop'

function Copy-JsonNode {
    param(
        [Parameter(Mandatory = $true)]
        [object]$Value
    )

    return $Value | ConvertTo-Json -Depth 100 | ConvertFrom-Json
}

function Resolve-OpenApiPathParameterReferences {
    param(
        [Parameter(Mandatory = $true)]
        [string]$OpenApiPath
    )

    $document = Get-Content -Path $OpenApiPath -Raw | ConvertFrom-Json
    if ($null -eq $document.components -or $null -eq $document.components.parameters) {
        return
    }

    function Get-DeclaredPathParameterNames {
        param(
            [Parameter(Mandatory = $true)]
            [string]$PathTemplate
        )

        $matches = [regex]::Matches($PathTemplate, '\{([^}]+)\}')
        return @($matches | ForEach-Object { $_.Groups[1].Value })
    }

    function Normalize-PathParameter {
        param(
            [Parameter(Mandatory = $true)]
            [object]$Parameter,
            [Parameter(Mandatory = $true)]
            [string[]]$DeclaredNames
        )

        if ($null -eq $Parameter -or $Parameter.in -ne 'path' -or [string]::IsNullOrWhiteSpace($Parameter.name)) {
            return $Parameter
        }

        $matchingName = $DeclaredNames | Where-Object { $_ -ieq $Parameter.name } | Select-Object -First 1
        if (-not [string]::IsNullOrWhiteSpace($matchingName)) {
            $Parameter.name = $matchingName
        }

        return $Parameter
    }

    foreach ($pathProperty in $document.paths.PSObject.Properties) {
        $pathItem = $pathProperty.Value
        $declaredPathParameters = Get-DeclaredPathParameterNames -PathTemplate $pathProperty.Name

        if ($pathItem.PSObject.Properties.Name -contains 'parameters') {
            $pathLevelParameters = @()
            foreach ($parameter in @($pathItem.parameters)) {
                if ($parameter.PSObject.Properties.Name -contains '$ref') {
                    $reference = $parameter.'$ref'
                    if ($reference -like '#/components/parameters/*') {
                        $parameterName = $reference.Substring('#/components/parameters/'.Length)
                        $componentParameter = $document.components.parameters.PSObject.Properties[$parameterName].Value
                        if ($null -ne $componentParameter -and $componentParameter.in -eq 'path') {
                            $pathLevelParameters += Normalize-PathParameter -Parameter (Copy-JsonNode -Value $componentParameter) -DeclaredNames $declaredPathParameters
                            continue
                        }
                    }
                }

                $pathLevelParameters += Normalize-PathParameter -Parameter $parameter -DeclaredNames $declaredPathParameters
            }

            $pathItem.parameters = $pathLevelParameters
        }

        foreach ($member in $pathItem.PSObject.Properties) {
            if ($member.Name -eq 'parameters') {
                continue
            }

            $operation = $member.Value
            if ($null -eq $operation -or -not ($operation.PSObject.Properties.Name -contains 'parameters')) {
                continue
            }

            $resolvedParameters = @()
            foreach ($parameter in @($operation.parameters)) {
                if ($parameter.PSObject.Properties.Name -contains '$ref') {
                    $reference = $parameter.'$ref'
                    if ($reference -like '#/components/parameters/*') {
                        $parameterName = $reference.Substring('#/components/parameters/'.Length)
                        $componentParameter = $document.components.parameters.PSObject.Properties[$parameterName].Value
                        if ($null -ne $componentParameter -and $componentParameter.in -eq 'path') {
                            $resolvedParameters += Normalize-PathParameter -Parameter (Copy-JsonNode -Value $componentParameter) -DeclaredNames $declaredPathParameters
                            continue
                        }
                    }
                }

                $resolvedParameters += Normalize-PathParameter -Parameter $parameter -DeclaredNames $declaredPathParameters
            }

            $operation.parameters = $resolvedParameters
        }
    }

    $document | ConvertTo-Json -Depth 100 | Set-Content -Path $OpenApiPath
}

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

    $previousAspNetCoreEnvironment = $env:ASPNETCORE_ENVIRONMENT
    $previousGenerateOpenApi = $env:GenerateOpenApi

    try {
        $env:ASPNETCORE_ENVIRONMENT = 'Development'
        $env:GenerateOpenApi = 'true'

        dotnet swagger tofile --output $outputFile $assemblyPath $DocumentName
        if ($LASTEXITCODE -ne 0) {
            throw 'dotnet swagger tofile failed.'
        }

        Resolve-OpenApiPathParameterReferences -OpenApiPath $outputFile
    }
    finally {
        $env:ASPNETCORE_ENVIRONMENT = $previousAspNetCoreEnvironment
        $env:GenerateOpenApi = $previousGenerateOpenApi
    }
}
finally {
    Pop-Location
}
