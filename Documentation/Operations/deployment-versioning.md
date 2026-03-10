# Deployment Versioning

## Goal

Each deployment should have:

- an immutable container image tag
- a matching Container Apps revision suffix
- runtime environment values that identify the deployed version in logs and telemetry

## GitHub Actions behavior

The workflow uses an image tag in this format:

- `<environment>-<run_number>-<short_sha>`

It also keeps:

- `${{ github.sha }}` as a full commit tag
- `latest` for convenience

The deployed revision receives:

- image: `qcontrol-service:<image_tag>`
- revision suffix: `r<run_number>-<short_sha>`
- `APP_VERSION=<image_tag>`
- `GIT_SHA=<full_sha>`

For `push` events on `main`, the workflow promotes sequentially through `dev`, `uat`, and `prod`. For `workflow_dispatch`, it deploys only the selected environment.

## Required GitHub environment secrets

Set these secrets in each GitHub Environment (`dev`, `uat`, `prod`):

- `AZURE_CREDENTIALS`

## Notes

- GitHub Actions uses Azure subscription `b7ae9f0b-20c3-4174-bb60-4ca01a867b8b`, which is committed in the workflow.
- Azure Container Registry, Storage, Application Insights, Container Apps, and API Management are provisioned by Bicep.
- The Bicep deployment injects the storage connection string and Application Insights connection string into Container Apps secrets.
- `APP_VERSION` is used by OpenTelemetry resource configuration so Application Insights can group telemetry by deployed version.
