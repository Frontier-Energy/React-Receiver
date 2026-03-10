using '../main.bicep'

param environmentName = 'prod'
param location = 'Central US'
param apimPublisherName = 'QControl Production Team'
param apimPublisherEmail = 'devnull@example.com'
param apimSkuName = 'Consumption'
param minReplicas = 2
param maxReplicas = 4
