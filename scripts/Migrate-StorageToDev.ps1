[CmdletBinding()]
param(
    [string]$SourceAccountName = "receiverstorage",
    [string]$DestinationEnvironment = "dev",
    [string]$DestinationResourceGroup,
    [string]$DestinationAccountName,
    [switch]$SkipBlobs,
    [switch]$SkipTables,
    [switch]$SkipQueues,
    [int]$QueueVisibilityTimeoutSeconds = 30,
    [int]$QueueIdleRounds = 3,
    [int]$SasLifetimeHours = 8
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$script:BlobContainers = @(
    "raw-data",
    "files",
    "files-quarantine"
)

$script:TableNames = @(
    "Users",
    "InspectionFiles",
    "InspectionIngestOutbox",
    "TenantConfigs",
    "MeProfiles",
    "FormSchemaCatalog",
    "FormSchemas",
    "Translations"
)

$script:QueueNames = @(
    "processor"
)

$script:StorageTokenCache = $null

function Write-Step {
    param([string]$Message)

    Write-Host ""
    Write-Host "==> $Message" -ForegroundColor Cyan
}

function Test-CommandExists {
    param([string]$Name)

    return $null -ne (Get-Command $Name -ErrorAction SilentlyContinue)
}

function Invoke-AzCli {
    param(
        [Parameter(Mandatory = $true)]
        [string[]]$Arguments
    )

    $output = & az @Arguments 2>&1
    if ($LASTEXITCODE -ne 0) {
        throw "Azure CLI command failed: az $($Arguments -join ' ')`n$output"
    }

    return $output
}

function Get-StorageToken {
    $refreshWindowUtc = (Get-Date).ToUniversalTime().AddMinutes(5)

    if ($null -eq $script:StorageTokenCache -or $script:StorageTokenCache.ExpiresOnUtc -le $refreshWindowUtc) {
        $tokenJson = Invoke-AzCli -Arguments @(
            "account", "get-access-token",
            "--resource", "https://storage.azure.com/",
            "--output", "json"
        ) | Out-String

        $token = $tokenJson | ConvertFrom-Json
        $script:StorageTokenCache = [pscustomobject]@{
            AccessToken  = $token.accessToken
            ExpiresOnUtc = ([DateTimeOffset]::Parse($token.expiresOn)).UtcDateTime
        }
    }

    return $script:StorageTokenCache.AccessToken
}

function Get-BearerHeaders {
    param(
        [string]$Accept = "application/json;odata=fullmetadata",
        [hashtable]$AdditionalHeaders
    )

    $headers = @{
        Authorization         = "Bearer $(Get-StorageToken)"
        "x-ms-date"           = [DateTime]::UtcNow.ToString("R")
        "x-ms-version"        = "2023-11-03"
        "x-ms-client-request-id" = [guid]::NewGuid().ToString()
        Accept                = $Accept
        DataServiceVersion    = "3.0;NetFx"
        MaxDataServiceVersion = "3.0;NetFx"
    }

    if ($AdditionalHeaders) {
        foreach ($key in $AdditionalHeaders.Keys) {
            $headers[$key] = $AdditionalHeaders[$key]
        }
    }

    return $headers
}

function Get-StorageAccountResourceGroup {
    param([string]$StorageAccountName)

    $resourceGroup = (Invoke-AzCli -Arguments @(
        "storage", "account", "show",
        "--name", $StorageAccountName,
        "--query", "resourceGroup",
        "--output", "tsv"
    ) | Out-String).Trim()

    if ([string]::IsNullOrWhiteSpace($resourceGroup)) {
        throw "Could not resolve the resource group for storage account '$StorageAccountName'."
    }

    return $resourceGroup
}

function Get-StorageAccountKey {
    param(
        [string]$StorageAccountName,
        [string]$ResourceGroupName
    )

    $key = (Invoke-AzCli -Arguments @(
        "storage", "account", "keys", "list",
        "--account-name", $StorageAccountName,
        "--resource-group", $ResourceGroupName,
        "--query", "[0].value",
        "--output", "tsv"
    ) | Out-String).Trim()

    if ([string]::IsNullOrWhiteSpace($key)) {
        throw "Could not retrieve an access key for storage account '$StorageAccountName'."
    }

    return $key
}

function Resolve-DestinationAccountName {
    param(
        [string]$ResourceGroupName,
        [string]$ExplicitAccountName
    )

    if (-not [string]::IsNullOrWhiteSpace($ExplicitAccountName)) {
        return $ExplicitAccountName
    }

    $name = (Invoke-AzCli -Arguments @(
        "storage", "account", "list",
        "--resource-group", $ResourceGroupName,
        "--query", "[0].name",
        "--output", "tsv"
    ) | Out-String).Trim()

    if ([string]::IsNullOrWhiteSpace($name)) {
        throw "Could not find a storage account in resource group '$ResourceGroupName'."
    }

    return $name
}

function New-AccountSas {
    param(
        [string]$StorageAccountName,
        [string]$ResourceGroupName,
        [string]$Permissions
    )

    $expiry = (Get-Date).ToUniversalTime().AddHours($SasLifetimeHours).ToString("yyyy-MM-ddTHH:mm:ssZ")

    $sas = (Invoke-AzCli -Arguments @(
        "storage", "account", "generate-sas",
        "--account-name", $StorageAccountName,
        "--account-key", (Get-StorageAccountKey -StorageAccountName $StorageAccountName -ResourceGroupName $ResourceGroupName),
        "--services", "bqtf",
        "--resource-types", "sco",
        "--permissions", $Permissions,
        "--expiry", $expiry,
        "--https-only",
        "--output", "tsv"
    ) | Out-String).Trim()

    if ([string]::IsNullOrWhiteSpace($sas)) {
        throw "Could not generate an account SAS for '$StorageAccountName'."
    }

    if ($sas.StartsWith("?")) {
        return $sas.Substring(1)
    }

    return $sas
}

function Ensure-BlobContainer {
    param(
        [string]$AccountName,
        [string]$ContainerName
    )

    Invoke-AzCli -Arguments @(
        "storage", "container", "create",
        "--name", $ContainerName,
        "--account-name", $AccountName,
        "--auth-mode", "login",
        "--output", "none"
    ) | Out-Null
}

function Ensure-Queue {
    param(
        [string]$AccountName,
        [string]$QueueName
    )

    Invoke-AzCli -Arguments @(
        "storage", "queue", "create",
        "--name", $QueueName,
        "--account-name", $AccountName,
        "--auth-mode", "login",
        "--output", "none"
    ) | Out-Null
}

function Ensure-Table {
    param(
        [string]$AccountName,
        [string]$TableName
    )

    $uri = "https://$AccountName.table.core.windows.net/Tables"
    $body = @{ TableName = $TableName } | ConvertTo-Json -Compress

    try {
        Invoke-WebRequest -Method Post -Uri $uri -Headers (Get-BearerHeaders -AdditionalHeaders @{
            "Content-Type" = "application/json"
        }) -Body $body | Out-Null
    }
    catch {
        if ($_.Exception.Response.StatusCode.value__ -ne 409) {
            throw
        }
    }
}

function Get-TableEntitySetUri {
    param(
        [string]$AccountName,
        [string]$TableName
    )

    return "https://$AccountName.table.core.windows.net/$TableName()"
}

function ConvertTo-TableEntityBody {
    param([psobject]$Entity)

    $body = [ordered]@{}

    foreach ($property in $Entity.PSObject.Properties) {
        if (
            $property.Name -eq "Timestamp" -or
            $property.Name.StartsWith("odata.") -or
            $property.Name.Contains("@odata.")
        ) {
            continue
        }

        if ($null -eq $property.Value) {
            continue
        }

        $body[$property.Name] = $property.Value
    }

    return $body
}

function Get-HttpErrorDetail {
    param([System.Management.Automation.ErrorRecord]$ErrorRecord)

    $response = $ErrorRecord.Exception.Response
    if ($null -eq $response) {
        return $ErrorRecord.ToString()
    }

    try {
        $reader = New-Object System.IO.StreamReader($response.GetResponseStream())
        $content = $reader.ReadToEnd()
        if ([string]::IsNullOrWhiteSpace($content)) {
            return "$($response.StatusCode.value__) $($response.StatusDescription)"
        }

        return $content
    }
    catch {
        return "$($response.StatusCode.value__) $($response.StatusDescription)"
    }
}

function Get-EntityIdentityUri {
    param(
        [string]$AccountName,
        [string]$TableName,
        [psobject]$Entity
    )

    $partitionKey = [Uri]::EscapeDataString(([string]$Entity.PartitionKey).Replace("'", "''"))
    $rowKey = [Uri]::EscapeDataString(([string]$Entity.RowKey).Replace("'", "''"))
    return "https://$AccountName.table.core.windows.net/$TableName(PartitionKey='$partitionKey',RowKey='$rowKey')"
}

function Upsert-TableEntity {
    param(
        [string]$AccountName,
        [string]$TableName,
        [psobject]$Entity
    )

    $bodyObject = ConvertTo-TableEntityBody -Entity $Entity
    $body = $bodyObject | ConvertTo-Json -Depth 20 -Compress
    $entityUri = Get-EntityIdentityUri -AccountName $AccountName -TableName $TableName -Entity $Entity

    try {
        Invoke-WebRequest -Method Post -Uri "https://$AccountName.table.core.windows.net/$TableName" -Headers (Get-BearerHeaders -AdditionalHeaders @{
            "Content-Type" = "application/json"
        }) -Body $body | Out-Null
        return
    }
    catch {
        if ($_.Exception.Response.StatusCode.value__ -ne 409) {
            $detail = Get-HttpErrorDetail -ErrorRecord $_
            throw "Insert failed for table '$TableName' entity (PartitionKey='$($Entity.PartitionKey)', RowKey='$($Entity.RowKey)').`nBody: $body`nResponse: $detail"
        }
    }

    try {
        Invoke-WebRequest -Method Put -Uri $entityUri -Headers (Get-BearerHeaders -AdditionalHeaders @{
            "Content-Type" = "application/json"
            "If-Match"     = "*"
        }) -Body $body | Out-Null
    }
    catch {
        $detail = Get-HttpErrorDetail -ErrorRecord $_
        throw "Update failed for table '$TableName' entity (PartitionKey='$($Entity.PartitionKey)', RowKey='$($Entity.RowKey)').`nBody: $body`nResponse: $detail"
    }
}

function Get-AllTableEntities {
    param(
        [string]$AccountName,
        [string]$TableName
    )

    $entities = New-Object System.Collections.Generic.List[object]
    $nextPartitionKey = $null
    $nextRowKey = $null

    do {
        $uri = Get-TableEntitySetUri -AccountName $AccountName -TableName $TableName
        $queryParts = @()

        if (-not [string]::IsNullOrWhiteSpace($nextPartitionKey)) {
            $queryParts += "NextPartitionKey=$([Uri]::EscapeDataString($nextPartitionKey))"
        }

        if (-not [string]::IsNullOrWhiteSpace($nextRowKey)) {
            $queryParts += "NextRowKey=$([Uri]::EscapeDataString($nextRowKey))"
        }

        if ($queryParts.Count -gt 0) {
            $uri = "$uri?$(($queryParts -join '&'))"
        }

        $response = Invoke-WebRequest -Method Get -Uri $uri -Headers (Get-BearerHeaders)
        $payload = $response.Content | ConvertFrom-Json

        $pageEntities = @($payload.value)
        if ($pageEntities.Count -gt 0) {
            foreach ($entity in $pageEntities) {
                $entities.Add($entity)
            }
        }

        $nextPartitionKey = $response.Headers["x-ms-continuation-NextPartitionKey"]
        $nextRowKey = $response.Headers["x-ms-continuation-NextRowKey"]
    }
    while (-not [string]::IsNullOrWhiteSpace($nextPartitionKey) -or -not [string]::IsNullOrWhiteSpace($nextRowKey))

    return $entities
}

function Copy-Tables {
    param(
        [string]$SourceAccount,
        [string]$DestinationAccount
    )

    foreach ($tableName in $script:TableNames) {
        Write-Step "Migrating table '$tableName'"
        Ensure-Table -AccountName $DestinationAccount -TableName $tableName

        $entities = @(Get-AllTableEntities -AccountName $SourceAccount -TableName $tableName)
        Write-Host "Found $($entities.Count) entities in $tableName"

        $copied = 0
        foreach ($entity in $entities) {
            Upsert-TableEntity -AccountName $DestinationAccount -TableName $tableName -Entity $entity
            $copied++

            if ($copied -eq 1 -or $copied % 250 -eq 0) {
                Write-Host "Upserted $copied / $($entities.Count)"
            }
        }
    }
}

function Copy-Blobs {
    param(
        [string]$SourceAccount,
        [string]$SourceResourceGroup,
        [string]$DestinationAccount,
        [string]$DestinationResourceGroup
    )

    if (-not (Test-CommandExists -Name "azcopy")) {
        throw "AzCopy is required for blob migration. Install it and ensure 'azcopy' is on PATH."
    }

    $sourceSas = New-AccountSas -StorageAccountName $SourceAccount -ResourceGroupName $SourceResourceGroup -Permissions "rl"
    $destinationSas = New-AccountSas -StorageAccountName $DestinationAccount -ResourceGroupName $DestinationResourceGroup -Permissions "racwdl"

    foreach ($containerName in $script:BlobContainers) {
        Write-Step "Migrating blob container '$containerName'"
        Ensure-BlobContainer -AccountName $DestinationAccount -ContainerName $containerName

        $sourceUri = "https://$SourceAccount.blob.core.windows.net/${containerName}?$sourceSas"
        $destinationUri = "https://$DestinationAccount.blob.core.windows.net/${containerName}?$destinationSas"

        & azcopy copy $sourceUri $destinationUri --recursive=true --overwrite=true
        if ($LASTEXITCODE -ne 0) {
            throw "AzCopy failed for container '$containerName'."
        }
    }
}

function Get-QueueMessageBatch {
    param(
        [string]$AccountName,
        [string]$QueueName
    )

    $uri = "https://$AccountName.queue.core.windows.net/$QueueName/messages?numofmessages=32&visibilitytimeout=$QueueVisibilityTimeoutSeconds"
    $response = Invoke-WebRequest -Method Get -Uri $uri -Headers (Get-BearerHeaders -Accept "application/xml")

    if ([string]::IsNullOrWhiteSpace($response.Content)) {
        return @()
    }

    [xml]$xml = $response.Content
    $messageNodes = @($xml.QueueMessagesList.QueueMessage)

    if ($messageNodes.Count -eq 1 -and $null -eq $messageNodes[0]) {
        return @()
    }

    return $messageNodes
}

function Add-QueueMessage {
    param(
        [string]$AccountName,
        [string]$QueueName,
        [string]$MessageText
    )

    $escapedMessage = [System.Security.SecurityElement]::Escape($MessageText)
    $body = "<QueueMessage><MessageText>$escapedMessage</MessageText></QueueMessage>"
    $uri = "https://$AccountName.queue.core.windows.net/$QueueName/messages"

    Invoke-WebRequest -Method Post -Uri $uri -Headers (Get-BearerHeaders -Accept "application/xml" -AdditionalHeaders @{
        "Content-Type" = "application/xml"
    }) -Body $body | Out-Null
}

function Copy-Queues {
    param(
        [string]$SourceAccount,
        [string]$DestinationAccount
    )

    Write-Warning "Queue migration assumes the source queue is static while the script runs. Stop writers and consumers first if you need a complete point-in-time copy."

    foreach ($queueName in $script:QueueNames) {
        Write-Step "Migrating queue '$queueName'"
        Ensure-Queue -AccountName $DestinationAccount -QueueName $queueName

        $seen = New-Object System.Collections.Generic.HashSet[string]
        $copied = 0
        $idleRounds = 0

        while ($idleRounds -lt $QueueIdleRounds) {
            $batch = Get-QueueMessageBatch -AccountName $SourceAccount -QueueName $queueName

            if ($batch.Count -eq 0) {
                $idleRounds++
                Start-Sleep -Seconds ([Math]::Min([Math]::Max($QueueVisibilityTimeoutSeconds, 1), 5))
                continue
            }

            $newMessagesThisRound = 0

            foreach ($message in $batch) {
                $messageId = [string]$message.MessageId
                if ($seen.Add($messageId)) {
                    Add-QueueMessage -AccountName $DestinationAccount -QueueName $queueName -MessageText ([string]$message.MessageText)
                    $copied++
                    $newMessagesThisRound++
                }
            }

            Write-Host "Seen $($seen.Count) source messages, copied $copied to destination"

            if ($newMessagesThisRound -eq 0) {
                $idleRounds++
                Start-Sleep -Seconds ([Math]::Min([Math]::Max($QueueVisibilityTimeoutSeconds, 1), 5))
            }
            else {
                $idleRounds = 0
            }
        }
    }
}

if (-not (Test-CommandExists -Name "az")) {
    throw "Azure CLI is required. Install it and run 'az login' before using this script."
}

Write-Step "Validating Azure login"
Invoke-AzCli -Arguments @("account", "show", "--output", "none") | Out-Null

if ([string]::IsNullOrWhiteSpace($DestinationResourceGroup)) {
    $DestinationResourceGroup = "rg-qcontrol-service-cus-$DestinationEnvironment"
}

$sourceResourceGroup = Get-StorageAccountResourceGroup -StorageAccountName $SourceAccountName
$DestinationAccountName = Resolve-DestinationAccountName -ResourceGroupName $DestinationResourceGroup -ExplicitAccountName $DestinationAccountName

Write-Step "Resolved migration endpoints"
Write-Host "Source account:      $SourceAccountName"
Write-Host "Source RG:           $sourceResourceGroup"
Write-Host "Destination account: $DestinationAccountName"
Write-Host "Destination RG:      $DestinationResourceGroup"

if (-not $SkipBlobs) {
    Copy-Blobs -SourceAccount $SourceAccountName -SourceResourceGroup $sourceResourceGroup -DestinationAccount $DestinationAccountName -DestinationResourceGroup $DestinationResourceGroup
}

if (-not $SkipTables) {
    Copy-Tables -SourceAccount $SourceAccountName -DestinationAccount $DestinationAccountName
}

if (-not $SkipQueues) {
    Copy-Queues -SourceAccount $SourceAccountName -DestinationAccount $DestinationAccountName
}

Write-Step "Migration complete"
