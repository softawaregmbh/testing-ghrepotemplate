using Azure.Provisioning;
using Azure.Provisioning.AppService;
using Azure.Provisioning.Expressions;
using Azure.Provisioning.Primitives;
using Microsoft.Extensions.Hosting;

namespace TestingGhrepotemplate.AppHost;

/// <summary>
/// Provides consistent Azure resource names when the <c>AppHost</c> is deployed using <c>aspire deploy</c>
/// in the form <c>{resourceName}-{projectName}-{environmentName}</c> (or similar based on resource name requirements).
/// </summary>
/// <param name="environment">The environment.</param>
internal sealed class TestingGhrepotemplateResourceNameResolver(IHostEnvironment environment)
    : ResourceNamePropertyResolver
{
    private const string ProjectName = "testingghrepotemplate";

    /// <summary>
    /// Returns the desired resource name for the given provisionable resource, or null if no override is needed.
    /// </summary>
    /// <param name="resource">The provisionable resource.</param>
    /// <returns>The desired resource name, or null if no override is needed.</returns>
    private static string? GetAzureResourceNameOverride(ProvisionableResource resource) =>
        resource.BicepIdentifier switch
        {
            "aspire_mi" => "acr-pull-id",
            "aspire_contributor_mi" => "aspire-dashboard-id",
            "aspire_asplan" => "plan",
            "backend_identity" => "backend-id",
            "sql_server" => "sql-server",
            "sqlServerAdminManagedIdentity" => "sql-admin-id",
            "aspire_law" => "law",
            "aspire_ai" => "insights",
            "webapp" => "backend",
            "dashboard" => "dashboard",
            _ => null
        };

    public override void ResolveProperties(ProvisionableConstruct construct, ProvisioningBuildOptions options)
    {
        // Try to resolve the "Name" property for provisionable resources based on our overrides.
        if (construct is ProvisionableResource resource &&
            resource.ProvisionableProperties.TryGetValue("Name", out var nameProperty))
        {
            var resolvedName = this.ResolveName(options, resource, resource.GetResourceNameRequirements());
            if (resolvedName is not null)
            {
                construct.SetProvisioningProperty(nameProperty, resolvedName);
            }
        }

        // Override the hardcoded "AZURE_APP_SERVICE_DASHBOARD_URI" output for the dashboard web app to use the actual host name
        if (construct is ProvisioningOutput output &&
            output.BicepIdentifier == "AZURE_APP_SERVICE_DASHBOARD_URI")
        {
            var dashboard = output.ParentInfrastructure!.GetProvisionableResources().OfType<WebSite>().Single(r => r.BicepIdentifier == "dashboard");
            output.Value = BicepFunction.Interpolate($"https://{dashboard.DefaultHostName}");
        }
    }

    public override BicepValue<string>? ResolveName(
        ProvisioningBuildOptions options,
        ProvisionableResource resource,
        ResourceNameRequirements requirements)
    {
        var resourceName = GetAzureResourceNameOverride(resource);

        if (resourceName is null)
        {
            var nameIsRequired = resource.ProvisionableProperties.TryGetValue("Name", out var name)
                && name.Kind == BicepValueKind.Unset
                && !name.IsOutput;

            if (!nameIsRequired)
            {
                return null;
            }

            // If the name is required but we don't have an override, use the Bicep identifier
            // as a fallback. This will be the case for secondary resources like role assignments
            // that are not directly visible in the Azure portal.
            resourceName = resource.BicepIdentifier;
        }

        resourceName = SanitizeText(resourceName, requirements.ValidCharacters);

        var separator =
            requirements.ValidCharacters.HasFlag(ResourceNameCharacters.Hyphen) ? "-" :
            requirements.ValidCharacters.HasFlag(ResourceNameCharacters.Underscore) ? "_" :
            requirements.ValidCharacters.HasFlag(ResourceNameCharacters.Period) ? "." :
            string.Empty;

        var environmentName = environment switch
        {
            var e when e.IsProduction() => "prod",
            var e when e.IsDevelopment() => "dev",
            _ => environment.EnvironmentName
        };

        return BicepFunction.Take(
            BicepFunction.Interpolate(
            $"{resourceName}{separator}{ProjectName}{separator}{environmentName}"),
            requirements.MaxLength);
    }
}
