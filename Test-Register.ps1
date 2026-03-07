param(
    [string]$BaseUrl = "https://localhost:5001",
    [string]$FirstName = "Ada",
    [string]$LastName = "Lovelace",
    [string]$Email = "ada@example.com"
)

$payload = @{
    firstName = $FirstName
    lastName = $LastName
    email = $Email
} | ConvertTo-Json

$uri = "$BaseUrl/auth/register"

try {
    $response = Invoke-RestMethod -Method Post -Uri $uri -ContentType "application/json" -Body $payload
    $response | ConvertTo-Json -Depth 5
}
catch {
    Write-Error $_
    throw
}
