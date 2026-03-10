# React-Receiver

React-Receiver is an ASP.NET Core Web API for inspection ingest and supporting configuration data. It accepts inspection submissions, stores payloads and files in Azure storage, and serves tenant configuration, form schemas, translations, and user data.

## Prerequisites

- .NET 10 SDK
- Node.js with npm
- PowerShell 7 or later for the included scripts

## Quick Start

From the repository root:

```powershell
npm install
dotnet restore
.\Start-LocalSession.ps1
```

If PowerShell blocks local scripts on your machine:

```powershell
powershell -ExecutionPolicy Bypass -File .\Start-LocalSession.ps1
```

Swagger UI:

```text
http://localhost:5108/swagger
```

Health endpoints:

- `GET /health/live`
- `GET /health/ready`
- `GET /health/startup`

## Local Development

Development is configured to use Azurite for blob, queue, and table storage through `appsettings.Development.json`.

The startup script:

- starts repo-local Azurite if ports `10000`, `10001`, and `10002` are not already active
- writes Azurite data and logs to `.azurite`
- runs the API with `ASPNETCORE_ENVIRONMENT=Development`

Manual equivalent:

```powershell
& .\node_modules\.bin\azurite.cmd --location .azurite --silent
dotnet run
```

## Common Commands

Restore:

```powershell
dotnet restore
```

Build:

```powershell
dotnet build
```

Clean:

```powershell
dotnet clean
```

Test:

```powershell
dotnet test
```

Run without the startup script:

```powershell
dotnet run
```

## Verify The API

Recommended first checks after startup:

1. `GET /health/ready`
2. `GET /tenant-config?tenantId=qhvac`
3. `GET /form-schemas`
4. `GET /translations/en`
5. `GET /users/me`

Example tenant config request:

```http
GET /tenant-config
```

Example response:

```json
{
  "tenantId": "qhvac",
  "displayName": "QHVAC",
  "uiDefaults": {
    "theme": "harbor",
    "font": "Tahoma, \"Trebuchet MS\", Arial, sans-serif",
    "language": "en",
    "showLeftFlyout": true,
    "showRightFlyout": true,
    "showInspectionStatsButton": false
  },
  "enabledForms": [
    "electrical",
    "electrical-sf",
    "hvac"
  ],
  "loginRequired": true
}
```

## Submit A Test Inspection

`POST /inspections` expects `multipart/form-data` with:

- `Payload`: JSON string
- `Files`: optional uploaded files

Create a sample payload in PowerShell:

```powershell
$payload = @{
  sessionId = "test-session-001"
  userId = "test-user-001"
  name = "Test Inspection"
  queryParams = @{
    foo = "bar"
    priority = "high"
  }
} | ConvertTo-Json -Depth 5
```

Choose a target:

```powershell
$uri = "http://localhost:5108/inspections"
$uri = "https://react-receiver.icysmoke-6c3b2e19.centralus.azurecontainerapps.io/inspections"
```

Quick remote submission with `Invoke-RestMethod`:

```powershell
Invoke-RestMethod $uri `
  -Method Post `
  -Form @{ Payload = $payload }
```

Multipart submission via `HttpClient`:

```powershell
$netHttp = "$env:WINDIR\Microsoft.NET\Framework64\v4.0.30319\System.Net.Http.dll"
if (-not (Test-Path $netHttp)) {
  $netHttp = "$env:WINDIR\Microsoft.NET\Framework\v4.0.30319\System.Net.Http.dll"
}

Add-Type -Path $netHttp

$multipart = New-Object System.Net.Http.MultipartFormDataContent
$jsonContent = New-Object System.Net.Http.StringContent($payload, [System.Text.Encoding]::UTF8, "application/json")
$multipart.Add($jsonContent, "Payload")

$client = New-Object System.Net.Http.HttpClient
$response = $client.PostAsync($uri, $multipart).Result
$response.Content.ReadAsStringAsync().Result
```

The response indicates the request was accepted for eventual processing.

## Register A Test User

Use the helper script:

```powershell
.\Test-Register.ps1 -BaseUrl "http://localhost:5108" -FirstName "Jane" -LastName "Doe" -Email "jane.doe@example.com"
```

## Debug In VS Code

1. Open the folder in VS Code.
2. Install the C# Dev Kit extension if prompted.
3. Press `F5` and select the `React-Receiver` launch profile.

## Documentation

Start here:

- [Documentation index](./Documentation/README.md)
- [Getting started](./Documentation/Onboarding/getting-started.md)
- [Architecture](./Documentation/Architecture/main.md)
- [Major design patterns](./Documentation/Patterns/major-design-patterns.md)
- [Infrastructure and secrets](./Documentation/Operations/infrastructure.md)

Useful follow-up references:

- [Repository map](./Documentation/Onboarding/repository-map.md)
- [Configuration](./Documentation/Operations/configuration.md)
- [Inspection ingest](./Documentation/Features/inspection-ingest.md)
