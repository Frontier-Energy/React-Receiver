# Deployment Versioning

## Goal

Each deployment should have:

- an immutable container image tag
- a matching Container Apps revision suffix
- runtime environment values that identify the deployed version in logs and telemetry

## GitHub Actions behavior

The workflow uses an image tag in this format:

- `main-<run_number>-<short_sha>`

It also keeps:

- `${{ github.sha }}` as a full commit tag
- `latest` for convenience

The deployed revision receives:

- image: `react-receiver:<image_tag>`
- revision suffix: `r<run_number>-<short_sha>`
- `APP_VERSION=<image_tag>`
- `GIT_SHA=<full_sha>`

## Required GitHub secrets

Set these repository secrets:

- `AZURE_CREDENTIALS`
- `AZURE_SUBSCRIPTION_ID`
- `ACR_NAME`
- `ACR_LOGIN_SERVER`
- `APPLICATIONINSIGHTS_CONNECTION_STRING`

## Notes

- `APPLICATIONINSIGHTS_CONNECTION_STRING` is copied into an Azure Container Apps secret named `applicationinsights-connection-string`.
- The container app references that secret through the environment variable `APPLICATIONINSIGHTS_CONNECTION_STRING`.
- `APP_VERSION` is used by OpenTelemetry resource configuration so Application Insights can group telemetry by deployed version.
