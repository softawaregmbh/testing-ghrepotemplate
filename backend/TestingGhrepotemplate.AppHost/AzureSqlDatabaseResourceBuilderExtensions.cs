using Aspire.Hosting.Azure;
using Azure.Provisioning;
using Azure.Provisioning.Authorization;
using Azure.Provisioning.Expressions;
using Azure.Provisioning.Resources;
using Azure.Provisioning.Roles;

namespace TestingGhrepotemplate.AppHost;

public static class AzureSqlDatabaseResourceBuilderExtensions
{
    public static IResourceBuilder<AzureSqlDatabaseResource> WithEntraUserAccess(
       this IResourceBuilder<AzureSqlDatabaseResource> builder,
       IResourceBuilder<ParameterResource> userIdParameter,
       IResourceBuilder<ParameterResource> userNameParameter)
        => builder.WithEntraPrincipalAccess(userIdParameter, userNameParameter, RoleManagementPrincipalType.User);

    public static IResourceBuilder<AzureSqlDatabaseResource> WithEntraGroupAccess(
        this IResourceBuilder<AzureSqlDatabaseResource> builder,
        IResourceBuilder<ParameterResource> groupIdParameter,
        IResourceBuilder<ParameterResource> groupNameParameter)
        => builder.WithEntraPrincipalAccess(groupIdParameter, groupNameParameter, RoleManagementPrincipalType.Group);

    private static IResourceBuilder<AzureSqlDatabaseResource> WithEntraPrincipalAccess(
        this IResourceBuilder<AzureSqlDatabaseResource> builder,
        IResourceBuilder<ParameterResource> principalIdParameter,
        IResourceBuilder<ParameterResource> principalNameParameter,
        RoleManagementPrincipalType principalType)
    {
        var serverBuilder = builder.ApplicationBuilder.CreateResourceBuilder(builder.Resource.Parent);
        serverBuilder.ConfigureInfrastructure(infra =>
        {
            var principalId = principalIdParameter.AsProvisioningParameter(infra);
            var principalName = principalNameParameter.AsProvisioningParameter(infra);

            // Taken almost 1:1 from AzureSqlServerResource.AddRoleAssignments (https://github.com/dotnet/aspire/blob/6613fb7032295e7ea88cb86f9690fa2aad00fad0/src/Aspire.Hosting.Azure.Sql/AzureSqlServerResource.cs#L145)
            var azureSqlServer = (AzureSqlServerResource)infra.AspireResource;

            var sqlServer = (Azure.Provisioning.Sql.SqlServer)azureSqlServer.AddAsExistingResource(infra);
            var sqlServerAdmin = infra.GetProvisionableResources().OfType<UserAssignedIdentity>().Single(
                i => i.BicepIdentifier == "sqlServerAdminManagedIdentity");

            var uniqueScriptIdentifier = Infrastructure.NormalizeBicepIdentifier(
                $"{azureSqlServer.GetBicepIdentifier()}_{builder.Resource.Name}_{principalId}");
            var scriptResource = new AzurePowerShellScript($"script_{uniqueScriptIdentifier}")
            {
                Name = BicepFunction.Take(
                    BicepFunction.Interpolate(
                        $"script-{BicepFunction.GetUniqueString(azureSqlServer.GetBicepIdentifier(), principalName, new StringLiteralExpression(builder.Resource.Name), BicepFunction.GetResourceGroup().Id)}"), 24),
                RetentionInterval = TimeSpan.FromHours(1),
                // List of supported versions: https://mcr.microsoft.com/v2/azuredeploymentscripts-powershell/tags/list
                AzPowerShellVersion = "10.0"
            };

            // Run the script as the administrator
            var id = BicepFunction.Interpolate($"{sqlServerAdmin.Id}").Compile().ToString();
            scriptResource.Identity.IdentityType = ArmDeploymentScriptManagedIdentityType.UserAssigned;
            scriptResource.Identity.UserAssignedIdentities[id] = new UserAssignedIdentityDetails();

            // Script don't support Bicep expression, they need to be passed as ENVs
            scriptResource.EnvironmentVariables.Add(new ScriptEnvironmentVariable() { Name = "DBNAME", Value = azureSqlServer.Databases[builder.Resource.Name] });
            scriptResource.EnvironmentVariables.Add(new ScriptEnvironmentVariable() { Name = "DBSERVER", Value = sqlServer.FullyQualifiedDomainName });
            scriptResource.EnvironmentVariables.Add(new ScriptEnvironmentVariable() { Name = "PRINCIPALTYPE", Value = new BicepValue<RoleManagementPrincipalType>(principalType) });
            scriptResource.EnvironmentVariables.Add(new ScriptEnvironmentVariable() { Name = "PRINCIPALNAME", Value = principalName });
            scriptResource.EnvironmentVariables.Add(new ScriptEnvironmentVariable() { Name = "ID", Value = principalId });

            scriptResource.ScriptContent = $$"""
                $sqlServerFqdn = "$env:DBSERVER"
                $sqlDatabaseName = "$env:DBNAME"
                $principalName = "$env:PRINCIPALNAME"
                $id = "$env:ID"

                # Install SqlServer module - using specific version to avoid breaking changes in 22.4.5.1 (see https://github.com/dotnet/aspire/issues/9926)
                Install-Module -Name SqlServer -RequiredVersion 22.3.0 -Force -AllowClobber -Scope CurrentUser
                Import-Module SqlServer

                $sqlCmd = @"
                DECLARE @name SYSNAME = '$principalName';
                DECLARE @id UNIQUEIDENTIFIER = '$id';

                -- Convert the guid to the right type
                DECLARE @castId NVARCHAR(MAX) = CONVERT(VARCHAR(MAX), CONVERT (VARBINARY(16), @id), 1);

                -- Construct command: CREATE USER [@name] WITH SID = @castId, TYPE = E;
                DECLARE @cmd NVARCHAR(MAX) = N'CREATE USER [' + @name + '] WITH SID = ' + @castId + ', TYPE = E;'
                EXEC (@cmd);

                -- Assign roles to the new user
                DECLARE @role1 NVARCHAR(MAX) = N'ALTER ROLE db_owner ADD MEMBER [' + @name + ']';
                EXEC (@role1);

                "@
                # Note: the string terminator must not have whitespace before it, therefore it is not indented.

                Write-Host $sqlCmd

                $connectionString = "Server=tcp:${sqlServerFqdn},1433;Initial Catalog=${sqlDatabaseName};Authentication=Active Directory Default;"

                Invoke-Sqlcmd -ConnectionString $connectionString -Query $sqlCmd
                """;

            infra.Add(scriptResource);
        });

        return builder;
    }
}
