using Aspire.Hosting.Azure;
using Azure.Provisioning.Authorization;
using Azure.Provisioning.Storage;

namespace TestingGhrepotemplate.AppHost;

public static class AzureStorageResourceBuilderExtensions
{
    public static IResourceBuilder<AzureStorageResource> WithEntraGroupAccess(
        this IResourceBuilder<AzureStorageResource> builder,
        IResourceBuilder<ParameterResource> groupIdParameter,
        params IEnumerable<StorageBuiltInRole> roles)
    {
        builder.ConfigureInfrastructure(infra =>
        {
            var principalId = groupIdParameter.AsProvisioningParameter(infra);
            var storageAccount = infra.GetProvisionableResources().OfType<StorageAccount>().Single();

            foreach (var role in roles)
            {
                var roleAssignment = storageAccount.CreateRoleAssignment(
                    role,
                    RoleManagementPrincipalType.Group,
                    principalId);

                infra.Add(roleAssignment);
            }
        });

        return builder;
    }
}
