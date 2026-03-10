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
var tags = {
  application: appName
  environment: environmentName
  managedBy: 'bicep'
  region: location
}
var storageConnectionString = 'DefaultEndpointsProtocol=https;AccountName=${storageAccount.name};AccountKey=${storageAccount.listKeys().keys[0].value};EndpointSuffix=${environment().suffixes.storage}'
var acrPullRoleDefinitionId = subscriptionResourceId('Microsoft.Authorization/roleDefinitions', '7f951dda-4ed3-4680-a7ca-43fe172d538d')

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
    allowSharedKeyAccess: true
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
          name: 'storage-connection-string'
          value: storageConnectionString
        }
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
          env: [
            if (environmentName == 'dev') {
              name: 'ASPNETCORE_ENVIRONMENT'
              value: 'Development'
            }
            {
              name: 'APPLICATIONINSIGHTS_CONNECTION_STRING'
              secretRef: 'applicationinsights-connection-string'
            }
            {
              name: 'BlobStorage__ConnectionString'
              secretRef: 'storage-connection-string'
            }
            {
              name: 'BlobStorage__ContainerName'
              value: rawDataContainerName
            }
            {
              name: 'QueueStorage__ConnectionString'
              secretRef: 'storage-connection-string'
            }
            {
              name: 'QueueStorage__QueueName'
              value: queueName
            }
            {
              name: 'TableStorage__ConnectionString'
              secretRef: 'storage-connection-string'
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
          ]
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
    apiType: 'http'
    displayName: 'QControlService'
    path: ''
    protocols: [
      'https'
    ]
    serviceUrl: 'https://${containerApp.properties.configuration.ingress.fqdn}'
    subscriptionRequired: false
  }
}

resource apiGetAll 'Microsoft.ApiManagement/service/apis/operations@2022-08-01' = {
  name: 'get-all'
  parent: apiManagementApi
  properties: {
    displayName: 'GET all routes'
    method: 'GET'
    urlTemplate: '/{*path}'
    templateParameters: [
      {
        name: 'path'
        required: false
        type: 'string'
      }
    ]
  }
}

resource apiPostAll 'Microsoft.ApiManagement/service/apis/operations@2022-08-01' = {
  name: 'post-all'
  parent: apiManagementApi
  properties: {
    displayName: 'POST all routes'
    method: 'POST'
    urlTemplate: '/{*path}'
    templateParameters: [
      {
        name: 'path'
        required: false
        type: 'string'
      }
    ]
  }
}

resource apiPutAll 'Microsoft.ApiManagement/service/apis/operations@2022-08-01' = {
  name: 'put-all'
  parent: apiManagementApi
  properties: {
    displayName: 'PUT all routes'
    method: 'PUT'
    urlTemplate: '/{*path}'
    templateParameters: [
      {
        name: 'path'
        required: false
        type: 'string'
      }
    ]
  }
}

resource apiPatchAll 'Microsoft.ApiManagement/service/apis/operations@2022-08-01' = {
  name: 'patch-all'
  parent: apiManagementApi
  properties: {
    displayName: 'PATCH all routes'
    method: 'PATCH'
    urlTemplate: '/{*path}'
    templateParameters: [
      {
        name: 'path'
        required: false
        type: 'string'
      }
    ]
  }
}

resource apiDeleteAll 'Microsoft.ApiManagement/service/apis/operations@2022-08-01' = {
  name: 'delete-all'
  parent: apiManagementApi
  properties: {
    displayName: 'DELETE all routes'
    method: 'DELETE'
    urlTemplate: '/{*path}'
    templateParameters: [
      {
        name: 'path'
        required: false
        type: 'string'
      }
    ]
  }
}

resource apiOptionsAll 'Microsoft.ApiManagement/service/apis/operations@2022-08-01' = {
  name: 'options-all'
  parent: apiManagementApi
  properties: {
    displayName: 'OPTIONS all routes'
    method: 'OPTIONS'
    urlTemplate: '/{*path}'
    templateParameters: [
      {
        name: 'path'
        required: false
        type: 'string'
      }
    ]
  }
}

resource apiHeadAll 'Microsoft.ApiManagement/service/apis/operations@2022-08-01' = {
  name: 'head-all'
  parent: apiManagementApi
  properties: {
    displayName: 'HEAD all routes'
    method: 'HEAD'
    urlTemplate: '/{*path}'
    templateParameters: [
      {
        name: 'path'
        required: false
        type: 'string'
      }
    ]
  }
}

resource apiTraceAll 'Microsoft.ApiManagement/service/apis/operations@2022-08-01' = {
  name: 'trace-all'
  parent: apiManagementApi
  properties: {
    displayName: 'TRACE all routes'
    method: 'TRACE'
    urlTemplate: '/{*path}'
    templateParameters: [
      {
        name: 'path'
        required: false
        type: 'string'
      }
    ]
  }
}

resource apiPolicy 'Microsoft.ApiManagement/service/apis/policies@2022-08-01' = {
  name: 'policy'
  parent: apiManagementApi
  properties: {
    format: 'rawxml'
    value: '<policies><inbound><base /></inbound><backend><base /></backend><outbound><base /></outbound><on-error><base /></on-error></policies>'
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
