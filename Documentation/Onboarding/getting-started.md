# Getting Started

This document is the shortest path from clone to a running API.

## What This Service Does

React-Receiver is an ASP.NET Core Web API that:

- accepts inspection submissions
- stores inspection payloads and files in Azure storage
- serves tenant configuration, form schemas, translations, and user data

## Prerequisites

- .NET 10 SDK
- PowerShell 7 or later for the example scripts in this repo
- Access to the environment-specific storage configuration you intend to use

The repository currently targets `net10.0` in both the main app and the test project.

## First Run

From the repository root:

```powershell
dotnet restore
dotnet build
dotnet test
dotnet run
```

Swagger UI in development:

```text
http://localhost:5108/swagger
```

Health endpoints:

- `GET /health/live`
- `GET /health/ready`
- `GET /health/startup`

## Startup Shape

Application startup is assembled in `Program.cs` and `Services/ServiceCollectionExtensions.cs`.

The app registers:

- controllers and request validation
- Azure storage clients and health checks
- MediatR handlers and pipeline behaviors
- feature services for auth, users, inspections, form schemas, translations, and tenant config
- hosted services for storage bootstrap, startup validation, seed import, and inspection retry processing

## Local Configuration

The app binds configuration from these sections:

- `BlobStorage`
- `QueueStorage`
- `TableStorage`
- `BootstrapData`

See [../Operations/configuration.md](../Operations/configuration.md) for the complete field list and behavior.

## First Things To Verify

Once the API is running, verify these in Swagger or a REST client:

1. `GET /health/ready`
2. `GET /tenant-config?tenantId=qhvac`
3. `GET /form-schemas`
4. `GET /translations/en`
5. `GET /users/me`

## First Inspection Submission

`POST /inspections` expects `multipart/form-data` with:

- `Payload`: JSON string
- `Files`: optional uploaded files

Example PowerShell payload:

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

Example local target:

```powershell
$uri = "http://localhost:5108/inspections"
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

The response indicates the request was accepted for eventual processing. Inspection finalization may complete inline or through the retry hosted service.

## Pitfalls To Know Immediately

- The app depends on valid storage configuration at startup.
- Inspection ingest is eventually consistent by design.
- Updates to tenant config, form schemas, and translations rely on `If-Match` and ETag semantics.
- Seed data can satisfy some lower-environment scenarios, but storage-backed behavior remains the primary runtime path.

## Suggested Next Reading

1. [../Architecture/main.md](../Architecture/main.md)
2. [./repository-map.md](./repository-map.md)
3. [../Operations/configuration.md](../Operations/configuration.md)
4. [../Features/inspection-ingest.md](../Features/inspection-ingest.md)
