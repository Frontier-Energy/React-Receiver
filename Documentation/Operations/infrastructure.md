# Infrastructure Deployment

## Overview

Infrastructure is defined in `infra/main.bicep` and deployed per environment through GitHub Actions.

The repository is currently pinned to Azure subscription `b7ae9f0b-20c3-4174-bb60-4ca01a867b8b` for both local validation and GitHub Actions deployments.

Each environment gets its own QControlService resource group in Central US:

- `rg-qcontrol-service-cus-dev`
- `rg-qcontrol-service-cus-uat`
- `rg-qcontrol-service-cus-prod`

The deployment provisions a full stack:

- Azure Container Registry
- Azure Container Apps managed environment
- Azure Container App
- Azure Storage account and required containers, queue, and tables
- Azure Log Analytics workspace
- Azure Application Insights
- Azure API Management

## GitHub Environments

Create three GitHub Environments:

- `dev`
- `uat`
- `prod`

Set these secrets in each environment:

- `AZURE_CREDENTIALS`

## Secret handling

Application secrets are not stored in `appsettings.json`.

Bicep resolves and injects these values into the Container App as secrets:

- storage account connection string
- Application Insights connection string

The initial Container App revision is created from a bootstrap image so the infrastructure deployment can complete before the application image is pushed. That bootstrap revision can use a different ingress port than the ASP.NET application. For system-assigned identity pulls from ACR, registry access is configured after the Container App exists, and then the GitHub Actions rollout step switches the Container App ingress target port back to `8080` when it deploys the real image.

The application reads them through environment variables such as:

- `BlobStorage__ConnectionString`
- `QueueStorage__ConnectionString`
- `TableStorage__ConnectionString`
- `APPLICATIONINSIGHTS_CONNECTION_STRING`

For local development, keep secrets out of source control and use one of:

- `appsettings.Development.json`
- local environment variables
- `dotnet user-secrets`

## Deployment flow

- `pull_request` to `main`: runs tests only
- `push` to `main`: runs tests, then deploys `dev`, then `uat`, then `prod`
- `workflow_dispatch`: deploys only the selected target environment

GitHub Environment protections still apply. If `uat` or `prod` has required reviewers or wait timers configured in GitHub, those gates will pause the automatic promotion until they are satisfied.

## First deployment checklist

1. Update `apimPublisherName` and `apimPublisherEmail` in each `.bicepparam` file.
2. Create the GitHub Environments and add the required Azure credentials.
3. Run the workflow for `dev`.
4. Validate the APIM gateway URL output and Container App URL output from the infrastructure deployment.
5. Push to `main` when you want the full `dev -> uat -> prod` promotion chain to run.
