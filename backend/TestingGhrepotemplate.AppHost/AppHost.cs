using System.Runtime.InteropServices;
using Aspire.Hosting.Azure;
using Azure.Provisioning.AppService;
using Azure.Provisioning.Sql;
using Azure.Provisioning.Storage;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TestingGhrepotemplate.AppHost;

var builder = DistributedApplication.CreateBuilder(args);

// Integration tests pass a unique id in "TestRunId". It switches the AppHost to throwaway
// containers under names that cannot collide. Without it a test run would adopt the developer's
// persistent containers, because Aspire matches existing ones by name, and apply its EF
// migrations to the development database.
var testRunId = builder.Configuration["TestRunId"];

var efExtensionsLicenseName = builder.AddParameter("EfExtensionsLicenseName", secret: true);
var efExtensionsLicenseKey = builder.AddParameter("EfExtensionsLicenseKey", secret: true);
var fontAwesomeNpmAuthToken = builder.AddParameter("FontAwesomeNpmAuthToken", true);
var telerikLicense = builder.AddParameter("TelerikLicense", TryReadTelerikLicenseFromAppData, secret: true);

string TryReadTelerikLicenseFromAppData()
{
    var parameterValue = builder.Configuration["Parameters:TelerikLicense"];
    if (parameterValue is not null)
    {
        return parameterValue;
    }

    var path = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
        ? Environment.ExpandEnvironmentVariables("%AppData%\\Telerik\\telerik-license.txt")
        : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".telerik", "telerik-license.txt");

    if (!File.Exists(path))
    {
        return null!;
    }

    return File.ReadAllText(path);
}


var sqlServer = builder
    .AddAzureSqlServer("sql-server")
    .RunAsContainer(container =>
    {
        container.WithImageTag("2025-latest");

        if (testRunId is not null)
        {
            container.WithContainerName($"sql-server-test-{testRunId}");
            return;
        }

        container
            .WithLifetime(ContainerLifetime.Persistent)
            .WithDataVolume();

        // Only configured for local development (appsettings.Development.json), where a stable
        // port keeps the connection string usable from SSMS and avoids recreating the container.
        if (builder.Configuration.GetValue<int?>("SqlServerPort") is int sqlServerPort)
        {
            container
                .WithEndpointProxySupport(false)
                .WithHostPort(sqlServerPort);
        }
    });

var database = sqlServer.AddDatabase("database");

var storage = builder.AddAzureStorage("storage")
    .RunAsEmulator(azurite =>
    {
        if (testRunId is not null)
        {
            azurite.WithContainerName($"storage-test-{testRunId}");
            return;
        }

        azurite.WithLifetime(ContainerLifetime.Persistent);
        azurite.WithDataVolume();
        azurite.WithBlobPort(10000);
        azurite.WithQueuePort(10001);
        azurite.WithTablePort(10002);
    });

var blobs = storage.AddBlobs("blobs");

var backend = builder
    .AddProject<Projects.TestingGhrepotemplate_Cqs_Api>("backend")
    .WithReference(database)
    .WithReference(blobs)
    .WithExternalHttpEndpoints()
    .WithHttpHealthCheck("health")
    .WithEnvironment("Z__EntityFramework__Extensions__LicenseName", efExtensionsLicenseName)
    .WithEnvironment("Z__EntityFramework__Extensions__LicenseKey", efExtensionsLicenseKey)
    .WaitFor(database)
    .WaitFor(blobs)
    .WithUrls(context =>
    {
        context.Urls.Add(new ResourceUrlAnnotation()
        {
            Url = "/swagger",
            Endpoint = context.GetEndpoint("https"),
        });
    });

var migrations = backend
    .AddEFMigrations("migrations")
    .WithReference(database)
    .RunDatabaseUpdateOnStart()
    .RunDatabaseUpdateOnDeploy();

backend.WaitForCompletion(migrations);

if (builder.ExecutionContext.IsRunMode)
{
    var frontend = builder
        .AddJavaScriptApp("frontend", "../../frontend", "start")
        .WithReference(backend)
        .WithEndpoint(name: "http", scheme: "http", targetPort: 4200, isExternal: true, isProxied: false)
        .WithUrlForEndpoint("http", a => a.DisplayLocation = UrlDisplayLocation.DetailsOnly)
        .WithHttpHealthCheck()
        .WithFrontendLicenses(telerikLicense, fontAwesomeNpmAuthToken);
}
else
{
    builder.Services.Configure<AzureProvisioningOptions>(options =>
        options.WithCustomResourceNameResolver(new TestingGhrepotemplateResourceNameResolver(builder.Environment))
    );

    builder.AddAzureAppServiceEnvironment("aspire")
        .WithAzureContainerRegistry(builder.AddAzureContainerRegistry("acr"))
        .WithAzureApplicationInsights()
        .WithDeploymentSlot("staging")
        .ConfigureInfrastructure(infra =>
        {
            var appServicePlan = infra.GetProvisionableResources().OfType<AppServicePlan>().Single();
            appServicePlan.Sku = new AppServiceSkuDescription()
            {
                Name = "P0V3",
                Tier = "PremiumV3",
                Capacity = 1
            };
        });

    sqlServer.ConfigureInfrastructure(infra =>
    {
        var database = infra.GetProvisionableResources().OfType<SqlDatabase>().Single();

        database.UseFreeLimit = false;
        database.Sku = new SqlSku()
        {
            Name = "Basic",
            Tier = "Basic",
            Capacity = 5,
        };
    });

    var entraGroupId = builder.AddParameter("EntraGroupId", false);
    var entraGroupName = builder.AddParameter("EntraGroupName", false);

    database.WithEntraGroupAccess(entraGroupId, entraGroupName);

    storage.WithEntraGroupAccess(entraGroupId, StorageBuiltInRole.StorageBlobDataContributor);

    backend
        .PublishWithFrontendFiles(
            "../../frontend",
            telerikLicense,
            fontAwesomeNpmAuthToken)
        .PublishAsAzureAppServiceWebsite(
            (infra, website) =>
            {
                website.SiteConfig.NumberOfWorkers = 1;
            },
            (infra, stagingSlot) =>
            {
                stagingSlot.SiteConfig.NumberOfWorkers = 1;
            });
}

builder.Build().Run();
