param(
    [ValidateSet('dev', 'uat', 'prod')]
    [string]$EnvironmentName = 'dev',

    [string]$SubscriptionId = 'b7ae9f0b-20c3-4174-bb60-4ca01a867b8b',

    [string]$Location = 'centralus',

    [switch]$SkipWhatIf,

    [switch]$SkipOpenApiGeneration
)

$ErrorActionPreference = 'Stop'

$repoRoot = Split-Path -Parent $PSScriptRoot
$templateFile = Join-Path $PSScriptRoot 'main.bicep'
$parameterFile = Join-Path $PSScriptRoot "environments\$EnvironmentName.bicepparam"
$resourceGroup = "rg-qcontrol-service-cus-$EnvironmentName"

function Assert-CommandAvailable {
    param([string]$CommandName)

    if (-not (Get-Command $CommandName -ErrorAction SilentlyContinue)) {
        throw "Required command '$CommandName' was not found in PATH."
    }
}

function Invoke-AzureCli {
    param(
        [Parameter(Mandatory = $true)]
        [string[]]$Arguments
    )

    Write-Host "==> az $($Arguments -join ' ')"
    & az @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "Azure CLI command failed: az $($Arguments -join ' ')"
    }
}

Assert-CommandAvailable -CommandName 'az'
Assert-CommandAvailable -CommandName 'dotnet'

if (-not (Test-Path $templateFile)) {
    throw "Template file not found: $templateFile"
}

if (-not (Test-Path $parameterFile)) {
    throw "Parameter file not found: $parameterFile"
}

Set-Location $repoRoot

if ($SkipOpenApiGeneration) {
    Write-Host 'Skipping OpenAPI generation and using the checked-in infra/apim/openapi.v1.json contract.'
}
else {
    Write-Host "Generating OpenAPI contract..."
    & powershell -ExecutionPolicy Bypass -File (Join-Path $repoRoot 'scripts\Generate-OpenApi.ps1') -Configuration Release
    if ($LASTEXITCODE -ne 0) {
        throw 'OpenAPI generation failed.'
    }
}

Write-Host "Checking Azure login state..."
& az account show --output none 2>$null
if ($LASTEXITCODE -ne 0) {
    Invoke-AzureCli -Arguments @('login')
}

Invoke-AzureCli -Arguments @('account', 'set', '--subscription', $SubscriptionId)
Invoke-AzureCli -Arguments @('group', 'create', '--name', $resourceGroup, '--location', $Location, '--output', 'none')

Invoke-AzureCli -Arguments @(
    'deployment', 'group', 'validate',
    '--resource-group', $resourceGroup,
    '--template-file', $templateFile,
    '--parameters', $parameterFile,
    '--output', 'none'
)

if (-not $SkipWhatIf) {
    Invoke-AzureCli -Arguments @(
        'deployment', 'group', 'what-if',
        '--resource-group', $resourceGroup,
        '--template-file', $templateFile,
        '--parameters', $parameterFile
    )
}

$deploymentOutputs = & az deployment group create `
    --resource-group $resourceGroup `
    --template-file $templateFile `
    --parameters $parameterFile `
    --query properties.outputs `
    --output json

if ($LASTEXITCODE -ne 0) {
    throw 'Azure deployment failed.'
}

$outputs = $deploymentOutputs | ConvertFrom-Json

Write-Host ''
Write-Host 'Deployment outputs:'
Write-Host "  Resource group:     $resourceGroup"
Write-Host "  Container App:      $($outputs.containerAppName.value)"
Write-Host "  Container App URL:  $($outputs.containerAppUrl.value)"
Write-Host "  APIM:               $($outputs.apiManagementName.value)"
Write-Host "  APIM Gateway URL:   $($outputs.apiManagementGatewayUrl.value)"
Write-Host "  ACR:                $($outputs.containerRegistryName.value)"
Write-Host "  ACR Login Server:   $($outputs.acrLoginServer.value)"
