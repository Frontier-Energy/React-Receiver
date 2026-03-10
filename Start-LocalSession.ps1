[CmdletBinding()]
param(
    [string]$AzuriteLocation = ".azurite",
    [string]$Configuration = "Debug"
)

$ErrorActionPreference = "Stop"

function Test-TcpPort {
    param(
        [string]$HostName,
        [int]$Port
    )

    $client = New-Object System.Net.Sockets.TcpClient
    try {
        $asyncResult = $client.BeginConnect($HostName, $Port, $null, $null)
        if (-not $asyncResult.AsyncWaitHandle.WaitOne(1000, $false)) {
            return $false
        }

        $client.EndConnect($asyncResult)
        return $true
    }
    catch {
        return $false
    }
    finally {
        $client.Dispose()
    }
}

$repoRoot = Split-Path -Parent $PSCommandPath
$azuriteCommand = Join-Path $repoRoot "node_modules\.bin\azurite.cmd"

if (-not (Test-Path $azuriteCommand)) {
    throw "Azurite is not installed. Run `npm install` in the repository root first."
}

if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    throw ".NET SDK was not found on PATH."
}

$azuriteRoot = Join-Path $repoRoot $AzuriteLocation
$azuriteLogDirectory = Join-Path $azuriteRoot "logs"
$stdoutLog = Join-Path $azuriteLogDirectory "azurite.stdout.log"
$stderrLog = Join-Path $azuriteLogDirectory "azurite.stderr.log"

New-Item -ItemType Directory -Force -Path $azuriteLogDirectory | Out-Null

$startedAzurite = $false
$azuriteProcess = $null

try {
    $blobReady = Test-TcpPort -HostName "127.0.0.1" -Port 10000
    $queueReady = Test-TcpPort -HostName "127.0.0.1" -Port 10001
    $tableReady = Test-TcpPort -HostName "127.0.0.1" -Port 10002

    if ($blobReady -and $queueReady -and $tableReady) {
        Write-Host "Azurite appears to already be running on ports 10000-10002."
    }
    else {
        Write-Host "Starting Azurite from $azuriteCommand"
        $azuriteProcess = Start-Process `
            -FilePath $azuriteCommand `
            -ArgumentList "--location", $azuriteRoot, "--silent" `
            -WorkingDirectory $repoRoot `
            -RedirectStandardOutput $stdoutLog `
            -RedirectStandardError $stderrLog `
            -PassThru
        $startedAzurite = $true

        $deadline = (Get-Date).AddSeconds(20)
        do {
            Start-Sleep -Milliseconds 500
            $blobReady = Test-TcpPort -HostName "127.0.0.1" -Port 10000
            $queueReady = Test-TcpPort -HostName "127.0.0.1" -Port 10001
            $tableReady = Test-TcpPort -HostName "127.0.0.1" -Port 10002
        } while ((-not ($blobReady -and $queueReady -and $tableReady)) -and (Get-Date) -lt $deadline)

        if (-not ($blobReady -and $queueReady -and $tableReady)) {
            throw "Azurite did not become ready within 20 seconds. Check $stdoutLog and $stderrLog."
        }
    }

    Write-Host "Starting API with ASPNETCORE_ENVIRONMENT=Development"
    $env:ASPNETCORE_ENVIRONMENT = "Development"
    Push-Location $repoRoot
    try {
        & dotnet run --configuration $Configuration
    }
    finally {
        Pop-Location
    }
}
finally {
    if ($startedAzurite -and $azuriteProcess -and -not $azuriteProcess.HasExited) {
        Write-Host "Stopping Azurite"
        Stop-Process -Id $azuriteProcess.Id
    }
}
