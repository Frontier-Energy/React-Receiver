targetScope = 'resourceGroup'

@description('Logical environment name.')
@allowed([
  'dev'
  'uat'
  'prod'
])
param environmentName string

@description('Azure region for all resources.')
param location string = resourceGroup().location

@description('Base service name used in resource naming.')
param appName string = 'qcontrol-service'

@description('Publisher name required by API Management.')
@minLength(1)
param apimPublisherName string

@description('Publisher email required by API Management.')
@minLength(3)
param apimPublisherEmail string

@description('API Management SKU name.')
@allowed([
  'Consumption'
  'Developer'
  'Basic'
])
param apimSkuName string = 'Consumption'

@description('Whether APIM should require an Authorization header on inbound requests.')
param apimRequireAuthorizationHeader bool = true

@description('Maximum number of calls allowed per renewal period by APIM.')
@minValue(1)
param apimRateLimitCalls int = 60

@description('Renewal period, in seconds, for the APIM rate limit counter.')
@minValue(1)
param apimRateLimitRenewalPeriod int = 60

@description('Maximum inbound request body size allowed through APIM, in bytes.')
@minValue(1)
param apimMaxRequestBodyBytes int = 27262976

@description('Container image used for the initial Container App deployment before GitHub Actions rolls out the application image.')
param bootstrapContainerImage string = 'mcr.microsoft.com/k8se/quickstart:latest'

@description('Container port exposed by the bootstrap container image.')
param bootstrapContainerPort int = 80

@description('Container port exposed by the application.')
param containerPort int = 8080

@description('CPU cores allocated to the container app.')
param containerCpu string = '0.5'

@description('Memory allocated to the container app.')
param containerMemory string = '1Gi'

@description('Minimum number of replicas.')
@minValue(1)
param minReplicas int = 1

@description('Maximum number of replicas.')
@minValue(1)
param maxReplicas int = 2

@description('Blob container used for raw inspection payloads.')
param rawDataContainerName string = 'raw-data'

@description('Blob container used for inspection file uploads.')
param filesContainerName string = 'files'

@description('Blob container used for quarantined inspection file uploads.')
param filesQuarantineContainerName string = 'files-quarantine'

@description('Queue name used for inspection processing.')
param queueName string = 'processor'

@description('Primary users table name.')
param usersTableName string = 'Users'

@description('Inspection files table name.')
param inspectionFilesTableName string = 'InspectionFiles'

@description('Inspection ingest outbox table name.')
param inspectionIngestOutboxTableName string = 'InspectionIngestOutbox'

@description('Tenant configuration table name.')
param tenantConfigTableName string = 'TenantConfigs'

@description('Current user profile table name.')
param meTableName string = 'MeProfiles'

@description('Form schema catalog table name.')
param formSchemaCatalogTableName string = 'FormSchemaCatalog'

@description('Form schema content table name.')
param formSchemasTableName string = 'FormSchemas'

@description('Translations table name.')
param translationsTableName string = 'Translations'

@description('Whether the app should seed bootstrap data on startup.')
param seedOnStartup bool = false

@description('Whether bootstrap data seeding may overwrite existing data.')
param overwriteExistingBootstrapData bool = false

var normalizedAppName = toLower(replace(appName, '-', ''))
var compactServiceToken = 'qcs'
var locationToken = 'cus'
var suffix = toLower(uniqueString(subscription().subscriptionId, resourceGroup().id, environmentName, appName))
var resourceSuffix = '${locationToken}-${environmentName}'
var containerAppName = 'ca-${appName}-${resourceSuffix}'
var managedEnvironmentName = 'cae-${appName}-${resourceSuffix}'
var logAnalyticsWorkspaceName = 'log-${appName}-${resourceSuffix}'
var applicationInsightsName = 'appi-${appName}-${resourceSuffix}'
var apiManagementName = toLower('apim-${compactServiceToken}-${locationToken}-${environmentName}-${take(suffix, 6)}')
var containerRegistryName = toLower('acr${compactServiceToken}${locationToken}${environmentName}${take(suffix, 6)}')
var storageAccountName = toLower('st${compactServiceToken}${locationToken}${environmentName}${take(suffix, 6)}')
var aspNetCoreEnvironmentVariables = environmentName == 'dev'
  ? [
      {
        name: 'ASPNETCORE_ENVIRONMENT'
        value: 'Development'
      }
    ]
  : []
var tags = {
  application: appName
  environment: environmentName
  managedBy: 'bicep'
  region: location
}
var acrPullRoleDefinitionId = subscriptionResourceId('Microsoft.Authorization/roleDefinitions', '7f951dda-4ed3-4680-a7ca-43fe172d538d')
var storageBlobDataContributorRoleDefinitionId = subscriptionResourceId('Microsoft.Authorization/roleDefinitions', 'ba92f5b4-2d11-453d-a403-e96b0029c9fe')
var storageQueueDataContributorRoleDefinitionId = subscriptionResourceId('Microsoft.Authorization/roleDefinitions', '974c5e8b-45b9-4653-ba55-5f855dd0fb88')
var storageTableDataContributorRoleDefinitionId = subscriptionResourceId('Microsoft.Authorization/roleDefinitions', '0a9a7e1f-b9d0-4cc4-a60d-0319b160aaa3')
var blobServiceUri = 'https://${storageAccount.name}.blob.${environment().suffixes.storage}'
var queueServiceUri = 'https://${storageAccount.name}.queue.${environment().suffixes.storage}'
var tableServiceUri = 'https://${storageAccount.name}.table.${environment().suffixes.storage}'
var apimOpenApiSpec = loadTextContent('apim/openapi.v1.json')
var apiManagementAuthorizationPolicy = apimRequireAuthorizationHeader
  ? '''
    <choose>
      <when condition="@(!context.Request.Headers.ContainsKey(&quot;Authorization&quot;) || !context.Request.Headers.GetValueOrDefault(&quot;Authorization&quot;, &quot;&quot;).StartsWith(&quot;Bearer &quot;, StringComparison.OrdinalIgnoreCase))">
        <return-response>
          <set-status code="401" reason="Unauthorized" />
          <set-header name="WWW-Authenticate" exists-action="override">
            <value>Bearer</value>
          </set-header>
        </return-response>
      </when>
    </choose>
    '''
  : ''
var apiManagementPolicyLines = concat(
  [
    '<policies>'
    '  <inbound>'
    '    <base />'
  ],
  empty(apiManagementAuthorizationPolicy) ? [] : split(trim(apiManagementAuthorizationPolicy), '\n'),
  [
    '    <rate-limit-by-key calls="${apimRateLimitCalls}" renewal-period="${apimRateLimitRenewalPeriod}" counter-key="@(context.Request.Headers.GetValueOrDefault(&quot;Authorization&quot;, context.Request.IpAddress))" />'
    '    <validate-content max-size="${apimMaxRequestBodyBytes}" size-exceeded-action="prevent" unspecified-content-type-action="ignore" />'
    '    <set-backend-service base-url="https://${containerApp.properties.configuration.ingress.fqdn}" />'
    '  </inbound>'
    '  <backend>'
    '    <base />'
    '  </backend>'
    '  <outbound>'
    '    <base />'
    '  </outbound>'
    '  <on-error>'
    '    <base />'
    '  </on-error>'
    '</policies>'
  ]
)
var apiManagementPolicy = join(apiManagementPolicyLines, '\n')

resource logAnalyticsWorkspace 'Microsoft.OperationalInsights/workspaces@2023-09-01' = {
  name: logAnalyticsWorkspaceName
  location: location
  tags: tags
  properties: {
    retentionInDays: 30
    sku: {
      name: 'PerGB2018'
    }
  }
}

resource applicationInsights 'Microsoft.Insights/components@2020-02-02' = {
  name: applicationInsightsName
  location: location
  kind: 'web'
  tags: tags
  properties: {
    Application_Type: 'web'
    Flow_Type: 'Bluefield'
    WorkspaceResourceId: logAnalyticsWorkspace.id
  }
}

resource storageAccount 'Microsoft.Storage/storageAccounts@2023-05-01' = {
  name: storageAccountName
  location: location
  tags: tags
  sku: {
    name: 'Standard_LRS'
  }
  kind: 'StorageV2'
  properties: {
    accessTier: 'Hot'
    allowBlobPublicAccess: false
    allowSharedKeyAccess: false
    minimumTlsVersion: 'TLS1_2'
    supportsHttpsTrafficOnly: true
  }
}

resource blobService 'Microsoft.Storage/storageAccounts/blobServices@2023-05-01' = {
  name: 'default'
  parent: storageAccount
}

resource rawDataContainer 'Microsoft.Storage/storageAccounts/blobServices/containers@2023-05-01' = {
  name: rawDataContainerName
  parent: blobService
  properties: {
    publicAccess: 'None'
  }
}

resource filesContainer 'Microsoft.Storage/storageAccounts/blobServices/containers@2023-05-01' = {
  name: filesContainerName
  parent: blobService
  properties: {
    publicAccess: 'None'
  }
}

resource filesQuarantineContainer 'Microsoft.Storage/storageAccounts/blobServices/containers@2023-05-01' = {
  name: filesQuarantineContainerName
  parent: blobService
  properties: {
    publicAccess: 'None'
  }
}

resource queueService 'Microsoft.Storage/storageAccounts/queueServices@2023-05-01' = {
  name: 'default'
  parent: storageAccount
}

resource processorQueue 'Microsoft.Storage/storageAccounts/queueServices/queues@2023-05-01' = {
  name: queueName
  parent: queueService
}

resource tableService 'Microsoft.Storage/storageAccounts/tableServices@2023-05-01' = {
  name: 'default'
  parent: storageAccount
}

resource usersTable 'Microsoft.Storage/storageAccounts/tableServices/tables@2023-05-01' = {
  name: usersTableName
  parent: tableService
}

resource inspectionFilesTable 'Microsoft.Storage/storageAccounts/tableServices/tables@2023-05-01' = {
  name: inspectionFilesTableName
  parent: tableService
}

resource inspectionIngestOutboxTable 'Microsoft.Storage/storageAccounts/tableServices/tables@2023-05-01' = {
  name: inspectionIngestOutboxTableName
  parent: tableService
}

resource tenantConfigTable 'Microsoft.Storage/storageAccounts/tableServices/tables@2023-05-01' = {
  name: tenantConfigTableName
  parent: tableService
}

resource meTable 'Microsoft.Storage/storageAccounts/tableServices/tables@2023-05-01' = {
  name: meTableName
  parent: tableService
}

resource formSchemaCatalogTable 'Microsoft.Storage/storageAccounts/tableServices/tables@2023-05-01' = {
  name: formSchemaCatalogTableName
  parent: tableService
}

resource formSchemasTable 'Microsoft.Storage/storageAccounts/tableServices/tables@2023-05-01' = {
  name: formSchemasTableName
  parent: tableService
}

resource translationsTable 'Microsoft.Storage/storageAccounts/tableServices/tables@2023-05-01' = {
  name: translationsTableName
  parent: tableService
}

resource containerRegistry 'Microsoft.ContainerRegistry/registries@2023-07-01' = {
  name: containerRegistryName
  location: location
  tags: tags
  sku: {
    name: 'Basic'
  }
  properties: {
    adminUserEnabled: false
    publicNetworkAccess: 'Enabled'
  }
}

resource managedEnvironment 'Microsoft.App/managedEnvironments@2024-03-01' = {
  name: managedEnvironmentName
  location: location
  tags: tags
  properties: {
    appLogsConfiguration: {
      destination: 'log-analytics'
      logAnalyticsConfiguration: {
        customerId: logAnalyticsWorkspace.properties.customerId
        sharedKey: logAnalyticsWorkspace.listKeys().primarySharedKey
      }
    }
  }
}

resource containerApp 'Microsoft.App/containerApps@2024-03-01' = {
  name: containerAppName
  location: location
  tags: tags
  identity: {
    type: 'SystemAssigned'
  }
  properties: {
    managedEnvironmentId: managedEnvironment.id
    configuration: {
      activeRevisionsMode: 'Single'
      ingress: {
        allowInsecure: false
        external: true
        targetPort: bootstrapContainerPort
        traffic: [
          {
            latestRevision: true
            weight: 100
          }
        ]
      }
      secrets: [
        {
          name: 'applicationinsights-connection-string'
          value: applicationInsights.properties.ConnectionString
        }
      ]
    }
    template: {
      containers: [
        {
          name: normalizedAppName
          image: bootstrapContainerImage
          env: concat(aspNetCoreEnvironmentVariables, [
            {
              name: 'APPLICATIONINSIGHTS_CONNECTION_STRING'
              secretRef: 'applicationinsights-connection-string'
            }
            {
              name: 'BlobStorage__ServiceUri'
              value: blobServiceUri
            }
            {
              name: 'BlobStorage__ContainerName'
              value: rawDataContainerName
            }
            {
              name: 'QueueStorage__ServiceUri'
              value: queueServiceUri
            }
            {
              name: 'QueueStorage__QueueName'
              value: queueName
            }
            {
              name: 'TableStorage__ServiceUri'
              value: tableServiceUri
            }
            {
              name: 'TableStorage__TableName'
              value: usersTableName
            }
            {
              name: 'TableStorage__InspectionFilesTableName'
              value: inspectionFilesTableName
            }
            {
              name: 'TableStorage__InspectionIngestOutboxTableName'
              value: inspectionIngestOutboxTableName
            }
            {
              name: 'TableStorage__TenantConfigTableName'
              value: tenantConfigTableName
            }
            {
              name: 'TableStorage__MeTableName'
              value: meTableName
            }
            {
              name: 'TableStorage__FormSchemaCatalogTableName'
              value: formSchemaCatalogTableName
            }
            {
              name: 'TableStorage__FormSchemasTableName'
              value: formSchemasTableName
            }
            {
              name: 'TableStorage__TranslationsTableName'
              value: translationsTableName
            }
            {
              name: 'BootstrapData__SeedOnStartup'
              value: string(seedOnStartup)
            }
            {
              name: 'BootstrapData__OverwriteExisting'
              value: string(overwriteExistingBootstrapData)
            }
          ])
          resources: {
            cpu: json(containerCpu)
            memory: containerMemory
          }
        }
      ]
      scale: {
        minReplicas: minReplicas
        maxReplicas: maxReplicas
      }
    }
  }
}

resource acrPullRoleAssignment 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(containerRegistry.id, containerApp.id, 'acrpull')
  scope: containerRegistry
  properties: {
    roleDefinitionId: acrPullRoleDefinitionId
    principalId: containerApp.identity.principalId
    principalType: 'ServicePrincipal'
  }
}

resource storageBlobDataContributorRoleAssignment 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(storageAccount.id, containerApp.id, 'storage-blob-data-contributor')
  scope: storageAccount
  properties: {
    roleDefinitionId: storageBlobDataContributorRoleDefinitionId
    principalId: containerApp.identity.principalId
    principalType: 'ServicePrincipal'
  }
}

resource storageQueueDataContributorRoleAssignment 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(storageAccount.id, containerApp.id, 'storage-queue-data-contributor')
  scope: storageAccount
  properties: {
    roleDefinitionId: storageQueueDataContributorRoleDefinitionId
    principalId: containerApp.identity.principalId
    principalType: 'ServicePrincipal'
  }
}

resource storageTableDataContributorRoleAssignment 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(storageAccount.id, containerApp.id, 'storage-table-data-contributor')
  scope: storageAccount
  properties: {
    roleDefinitionId: storageTableDataContributorRoleDefinitionId
    principalId: containerApp.identity.principalId
    principalType: 'ServicePrincipal'
  }
}

resource apiManagement 'Microsoft.ApiManagement/service@2022-08-01' = {
  name: apiManagementName
  location: location
  tags: tags
  sku: {
    name: apimSkuName
    capacity: apimSkuName == 'Consumption' ? 0 : 1
  }
  properties: {
    publisherEmail: apimPublisherEmail
    publisherName: apimPublisherName
    publicNetworkAccess: 'Enabled'
    virtualNetworkType: 'None'
  }
}

resource apiManagementApi 'Microsoft.ApiManagement/service/apis@2022-08-01' = {
  name: 'qcontrol-service'
  parent: apiManagement
  properties: {
    displayName: 'QControlService'
    format: 'openapi+json'
    path: ''
    protocols: [
      'https'
    ]
    serviceUrl: 'https://${containerApp.properties.configuration.ingress.fqdn}'
    subscriptionRequired: false
    value: apimOpenApiSpec
  }
}

resource apiPolicy 'Microsoft.ApiManagement/service/apis/policies@2022-08-01' = {
  name: 'policy'
  parent: apiManagementApi
  properties: {
    format: 'rawxml'
    value: apiManagementPolicy
  }
}

output resourceGroupName string = resourceGroup().name
output locationName string = location
output storageAccountName string = storageAccount.name
output containerRegistryName string = containerRegistry.name
output acrLoginServer string = containerRegistry.properties.loginServer
output containerAppName string = containerApp.name
output containerAppUrl string = 'https://${containerApp.properties.configuration.ingress.fqdn}'
output applicationContainerPort int = containerPort
output apiManagementName string = apiManagement.name
output apiManagementGatewayUrl string = apiManagement.properties.gatewayUrl
output applicationInsightsName string = applicationInsights.name
