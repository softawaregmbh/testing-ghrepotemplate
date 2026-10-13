using Aspire.Hosting.Azure;
using Azure.Provisioning.Primitives;

namespace TestingGhrepotemplate.AppHost;

public static class AzureProvisionOptionsExtensions
{
    public static AzureProvisioningOptions WithCustomResourceNameResolver(
        this AzureProvisioningOptions options,
        ResourceNamePropertyResolver resolver)
    {
        var resolvers = options.ProvisioningBuildOptions.InfrastructureResolvers;
        var dynamicNameResolver = resolvers
            .OfType<DynamicResourceNamePropertyResolver>()
            .FirstOrDefault();

        if (dynamicNameResolver is not null)
        {
            resolvers.Remove(dynamicNameResolver);
        }

        resolvers.Insert(0, resolver);

        return options;
    }
}
