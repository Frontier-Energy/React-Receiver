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

The application reads them through environment variables such as:

- `BlobStorage__ConnectionString`
- `QueueStorage__ConnectionString`
- `TableStorage__ConnectionString`
- `APPLICATIONINSIGHTS_CONNECTION_STRING`

For local development, keep secrets out of source control and use one of:

- `appsettings.Development.json`
- local environment variables
- `dotnet user-secrets`

## First deployment checklist

1. Update `apimPublisherName` and `apimPublisherEmail` in each `.bicepparam` file.
2. Create the GitHub Environments and add the required Azure credentials.
3. Run the workflow for `dev`.
4. Validate the APIM gateway URL output and Container App URL output from the infrastructure deployment.
