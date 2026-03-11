# Infrastructure

This directory contains the Bicep-based infrastructure definition for QControlService.

## Scope

The deployment is resource-group scoped. GitHub Actions creates the target resource group, then runs `infra/main.bicep` with an environment-specific `.bicepparam` file.

The stack provisions:

- Azure Container Registry
- Azure Container Apps managed environment
- Azure Container App
- Azure Storage account with the required blob containers, queue, and tables
- Azure Log Analytics workspace
- Azure Application Insights
- Azure API Management in front of the Container App

## Environments

- `infra/environments/dev.bicepparam`
- `infra/environments/uat.bicepparam`
- `infra/environments/prod.bicepparam`

## Naming

Resource names follow these patterns:

- Resource group: `rg-qcontrol-service-cus-<environment>`
- Container App: `ca-qcontrol-service-cus-<environment>`
- Managed environment: `cae-qcontrol-service-cus-<environment>`
- Log Analytics: `log-qcontrol-service-cus-<environment>`
- Application Insights: `appi-qcontrol-service-cus-<environment>`

Resources that must be globally unique, such as Storage, ACR, and APIM, add a deterministic suffix derived from the subscription and resource group.

## APIM

API Management is provisioned from the application OpenAPI contract:

- Azure-managed default hostname only
- explicit imported operations from `infra/apim/openapi.v1.json`
- dev-only Swagger UI passthrough at `/swagger/` and `/swagger/index.html`
- dev-only Swagger UI static assets including `/swagger/index.css` and `/swagger/index.js`
- API-level policy enforcement for bearer auth header presence
- rate limiting
- request body size enforcement
- no subscription key requirement by default

Swagger is intentionally exposed through APIM only in `dev`. `uat` and `prod` do not publish the Swagger UI routes.

Storage access for the Container App uses a user-assigned managed identity. The app receives that identity's client ID through `AZURE_CLIENT_ID`, while the Container App keeps its system-assigned identity for other platform integrations.

Refresh the contract with:

```powershell
pwsh ./scripts/Generate-OpenApi.ps1 -Configuration Release
```

## Required updates before first deployment

Replace the placeholder APIM publisher identity values in the environment parameter files:

- `apimPublisherName`
- `apimPublisherEmail`

## Local testing workflow

Use this process when changing `infra/main.bicep` or any environment parameter file and you want to validate the infrastructure locally before pushing to GitHub Actions.

The repository is pinned to Azure subscription `b7ae9f0b-20c3-4174-bb60-4ca01a867b8b`.

Single-command local entrypoint:

```powershell
.\infra\Deploy-Infrastructure.ps1 -EnvironmentName dev
```

If your machine does not have the ASP.NET Core 8.0 runtime required by the local `dotnet swagger` tool, you can use the checked-in OpenAPI contract instead:

```powershell
.\infra\Deploy-Infrastructure.ps1 -EnvironmentName dev -SkipOpenApiGeneration
```

### Prerequisites

- Azure CLI installed
- Bicep support available through Azure CLI
- Azure access to subscription `b7ae9f0b-20c3-4174-bb60-4ca01a867b8b`

### 1. Authenticate and select the subscription

```powershell
cd c:\dev\GitHub\React-Receiver

az login
az account set --subscription "b7ae9f0b-20c3-4174-bb60-4ca01a867b8b"
az bicep version
```

### 2. Choose an environment and ensure the resource group exists

```powershell
$envName = "dev"
$resourceGroup = "rg-qcontrol-service-cus-$envName"
$location = "centralus"

az group create --name $resourceGroup --location $location
```

### 3. Update required parameter values

Before the first deployment, replace placeholder values in:

- `infra/environments/dev.bicepparam`
- `infra/environments/uat.bicepparam`
- `infra/environments/prod.bicepparam`

At minimum, set:

- `apimPublisherName`
- `apimPublisherEmail`

### 4. Validate the template

```powershell
az deployment group validate `
  --resource-group $resourceGroup `
  --template-file infra/main.bicep `
  --parameters infra/environments/$envName.bicepparam
```

### 5. Preview the change set

```powershell
az deployment group what-if `
  --resource-group $resourceGroup `
  --template-file infra/main.bicep `
  --parameters infra/environments/$envName.bicepparam
```

### 6. Provision the infrastructure

```powershell
az deployment group create `
  --resource-group $resourceGroup `
  --template-file infra/main.bicep `
  --parameters infra/environments/$envName.bicepparam `
  --output json
```

### 6a. Local smoke test after deployment

After a successful `dev` deployment, verify the app and APIM directly:

```powershell
$containerAppUrl = az containerapp show `
  --resource-group rg-qcontrol-service-cus-dev `
  --name ca-qcontrol-service-cus-dev `
  --query properties.configuration.ingress.fqdn `
  -o tsv

curl.exe -sS -D - "https://$containerAppUrl/health/live" -o NUL
curl.exe -sS -D - "https://$containerAppUrl/swagger/v1/swagger.json" -o NUL
curl.exe -sS -D - "https://apim-qcs-cus-dev-7dbwgp.azure-api.net/swagger/" -o NUL
curl.exe -sS -D - "https://apim-qcs-cus-dev-7dbwgp.azure-api.net/swagger/index.html" -o NUL
```

Expected results in `dev`:

- `GET /health/live` returns `200`
- `GET /swagger/v1/swagger.json` returns `200`
- `GET /swagger/` through APIM returns `200`
- `GET /swagger/index.html` through APIM returns `301` to `./` or `200`, and the browser should load the Swagger UI

If the app still fails to start, inspect the current container logs:

```powershell
az containerapp logs show `
  --resource-group rg-qcontrol-service-cus-dev `
  --name ca-qcontrol-service-cus-dev `
  --tail 200
```

### 7. Verify the deployed resources

```powershell
az containerapp list --resource-group $resourceGroup -o table
az apim list --resource-group $resourceGroup -o table
az storage account list --resource-group $resourceGroup -o table
az acr list --resource-group $resourceGroup -o table
```

### 8. Complete the app rollout

The Bicep deployment creates the infrastructure and a bootstrap Container App definition. The bootstrap revision uses its own image port so ARM can finish provisioning cleanly before the real application image exists.

To test the real application, push a container image through the GitHub Actions deploy workflow or manually update the Container App image after provisioning. For system-assigned identity pulls from ACR, configure registry access after the Container App exists, then update the image. When updating manually, also set the ingress target port back to `8080` for the ASP.NET app.
