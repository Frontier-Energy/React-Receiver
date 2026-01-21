# React-Receiver

## Prerequisites

- .NET 9 SDK

## Commands

Restore:
```sh
dotnet restore
```

Build:
```sh
dotnet build
```

Clean:
```sh
dotnet clean
```

Test:
```sh
dotnet test
```

Run:
```sh
dotnet run
```

## Test ReceiveInspection (POST)
The ReceiveInspection endpoint expects `multipart/form-data` with a JSON payload field
and optional file uploads.

set a body for testing in powershell
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

Invoke a remote call for testing (PowerShell 7+):
```powershell
Invoke-RestMethod "https://react-receiver.icysmoke-6c3b2e19.centralus.azurecontainerapps.io/QHVAC/ReceiveInspection/" `
  -Method Post `
  -Form @{ Payload = $payload }
```

Set the URL to local, or dev
```
$uri = "http://localhost:5108/QHVAC/ReceiveInspection"
$uri = "https://react-receiver.icysmoke-6c3b2e19.centralus.azurecontainerapps.io/QHVAC/ReceiveInspection/"
```

Add the http dll to powershell
```powershell

$netHttp = "$env:WINDIR\Microsoft.NET\Framework64\v4.0.30319\System.Net.Http.dll"
if (-not (Test-Path $netHttp)) {
  $netHttp = "$env:WINDIR\Microsoft.NET\Framework\v4.0.30319\System.Net.Http.dll"
}

Add-Type -Path $netHttp
```

execute (multipart via HttpClient):
```powershell

$multipart = New-Object System.Net.Http.MultipartFormDataContent
$jsonContent = New-Object System.Net.Http.StringContent($payload, [System.Text.Encoding]::UTF8, "application/json")
$multipart.Add($jsonContent, "Payload")

$client = New-Object System.Net.Http.HttpClient
$response = $client.PostAsync($uri, $multipart).Result
$response.Content.ReadAsStringAsync().Result
```

## Test Register (POST)
Run the PowerShell helper script:
```powershell
.\Test-Register.ps1 -BaseUrl "https://localhost:5108" -FirstName "Jane" -LastName "Doe" -Email "jane.doe@example.com"
```

## Debug in Visual Studio Code

1. Open the folder in VS Code.
2. Install the C# Dev Kit extension if prompted.
3. Press `F5` and select the `React-Receiver` launch profile.
