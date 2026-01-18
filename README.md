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

PowerShell (Invoke-RestMethod):
```powershell
$body = @{
  sessionId = "abc123"
  name = "Test"
  queryParams = @{ foo = "bar"; priority = "high" }
} | ConvertTo-Json

Invoke-RestMethod "http://localhost:5108/QHVAC/ReceiveInspection" `
  -Method Post `
  -ContentType "application/json" `
  -Body $body
```

PowerShell (Invoke-WebRequest):
```powershell
Invoke-WebRequest "http://localhost:5108/QHVAC/ReceiveInspection" `
  -Method Post `
  -ContentType "application/json" `
  -Body $body
```

Windows cmd:
```sh
curl -X POST "http://localhost:5108/QHVAC/ReceiveInspection" ^
  -H "Content-Type: application/json" ^
  -d "{\"sessionId\":\"abc123\",\"name\":\"Test\",\"queryParams\":{\"foo\":\"bar\",\"priority\":\"high\"}}"
```

## Debug in Visual Studio Code

1. Open the folder in VS Code.
2. Install the C# Dev Kit extension if prompted.
3. Press `F5` and select the `React-Receiver` launch profile.
