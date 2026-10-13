@description('The location for the resource(s) to be deployed.')
param location string = resourceGroup().location

@description('The fully qualified domain name of the Azure SQL server.')
param sqlServerFqdn string

@description('The name of the database to migrate.')
param databaseName string

@description('The name of the user-assigned managed identity that is the Entra admin of the SQL server. The migration runs as this identity, which has DDL access.')
param sqlServerAdminName string

@description('The idempotent EF Core migration SQL script to apply to the database.')
@secure()
param migrationScript string

@description('Forces the deployment script to run on every deployment so new migrations are always applied.')
param forceUpdateTag string = utcNow()

resource sqlServerAdmin 'Microsoft.ManagedIdentity/userAssignedIdentities@2024-11-30' existing = {
  name: sqlServerAdminName
}

resource migrate 'Microsoft.Resources/deploymentScripts@2023-08-01' = {
  name: take('migrate-${uniqueString(databaseName, resourceGroup().id)}', 24)
  location: location
  kind: 'AzurePowerShell'
  identity: {
    type: 'UserAssigned'
    userAssignedIdentities: {
      '${sqlServerAdmin.id}': { }
    }
  }
  properties: {
    azPowerShellVersion: '14.0'
    retentionInterval: 'PT1H'
    forceUpdateTag: forceUpdateTag
    environmentVariables: [
      {
        name: 'DBSERVER'
        value: sqlServerFqdn
      }
      {
        name: 'DBNAME'
        value: databaseName
      }
      {
        name: 'MIGRATIONSCRIPT'
        secureValue: migrationScript
      }
    ]
    scriptContent: '''
$ErrorActionPreference = 'Stop'
$sqlServerFqdn = "$env:DBSERVER"
$sqlDatabaseName = "$env:DBNAME"
$migrationScript = "$env:MIGRATIONSCRIPT"

# This script deliberately avoids the SqlServer PowerShell module (Invoke-Sqlcmd). The Azure
# deployment script host imports the Az modules before running user scripts, and Az ships an
# older Microsoft.Extensions.Caching.Memory. Importing SqlServer afterwards makes its Always
# Encrypted Azure Key Vault provider - registered unconditionally on the first Invoke-Sqlcmd
# call, even though nothing here uses Always Encrypted - bind against that older assembly and
# fail with:
#   System.MissingMethodException: Method not found: 'Void Microsoft.Extensions.Caching.Memory.MemoryCache..ctor(
#     Microsoft.Extensions.Options.IOptions`1<Microsoft.Extensions.Caching.Memory.MemoryCacheOptions>)'.
# Pinning a module version only works against one combination of Az module and .NET runtime
# versions in the image, so use System.Data.SqlClient - which ships in-box with PowerShell in
# the azuredeploymentscripts-powershell images - together with a managed identity access token.
# This mirrors what Aspire's own generated SQL role scripts do, see
# https://github.com/microsoft/aspire/issues/18892.

# The token audience is cloud specific - US Gov uses database.usgovcloudapi.net and China uses
# database.chinacloudapi.cn - so derive it from the deployment script's Az context rather than
# assuming public cloud. The previous Invoke-Sqlcmd implementation used
# 'Authentication=Active Directory Default', which let the driver resolve this automatically.
$sqlDnsSuffix = (Get-AzContext).Environment.SqlDatabaseDnsSuffix
if ([string]::IsNullOrWhiteSpace($sqlDnsSuffix)) {
    $sqlDnsSuffix = ".database.windows.net"
}
$sqlAudience = "https://" + $sqlDnsSuffix.TrimStart('.') + "/"

$connectionString = "Server=tcp:${sqlServerFqdn},1433;Initial Catalog=${sqlDatabaseName};Encrypt=True;TrustServerCertificate=False;"

# 'dotnet ef migrations script --idempotent' emits GO batch separators. Invoke-Sqlcmd understood
# those, but SqlCommand does not - GO is a client-side construct, not T-SQL - so split the script
# into batches and execute them in order. The script is idempotent and safe to run repeatedly.
$batches = [System.Text.RegularExpressions.Regex]::Split($migrationScript, '(?im)^[ \t]*GO[ \t]*\d*[ \t]*\r?$') |
    Where-Object { -not [string]::IsNullOrWhiteSpace($_) }

Write-Host "Migration script contains $($batches.Count) batch(es)."

$maxRetries = 5
$retryDelay = 60
$attempt = 0
$success = $false

while (-not $success -and $attempt -lt $maxRetries) {
    $attempt++
    Write-Host "Applying database migrations, attempt $attempt of $maxRetries..."
    $connection = $null
    try {
        # Acquired inside the loop so a transient token failure is retried like any other
        # failure, rather than aborting the script before the first attempt.
        $tokenResponse = Get-AzAccessToken -ResourceUrl $sqlAudience

        # Az.Accounts 5.x returns the token as a SecureString, earlier majors return a plain string.
        $accessToken = if ($tokenResponse.Token -is [System.Security.SecureString]) {
            [System.Net.NetworkCredential]::new("", $tokenResponse.Token).Password
        } else {
            $tokenResponse.Token
        }

        $connection = New-Object System.Data.SqlClient.SqlConnection
        $connection.ConnectionString = $connectionString
        $connection.AccessToken = $accessToken
        $connection.Open()

        $batchNumber = 0
        foreach ($batch in $batches) {
            $batchNumber++
            $command = $connection.CreateCommand()
            $command.CommandText = $batch
            $command.CommandTimeout = 0
            [void]$command.ExecuteNonQuery()
            Write-Host "Applied batch $batchNumber of $($batches.Count)."
        }

        $success = $true
        Write-Host "Database migrations applied successfully on attempt $attempt."
    } catch {
        Write-Host "Attempt $attempt failed: $_"
        if ($attempt -lt $maxRetries) {
            Write-Host "Retrying in $retryDelay seconds..."
            Start-Sleep -Seconds $retryDelay
        } else {
            throw
        }
    } finally {
        if ($null -ne $connection) {
            $connection.Dispose()
        }
    }
}
'''
  }
}
