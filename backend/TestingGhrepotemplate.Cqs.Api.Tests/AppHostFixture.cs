namespace TestingGhrepotemplate.Cqs.Api.Tests;

/// <summary>
/// Runs the AppHost once per test class, so the tests exercise the same resources as a local
/// debug session: the SQL Server container, the Azurite emulator and the backend, including the
/// EF Core migrations that are applied on startup.
/// </summary>
public sealed class AppHostFixture : IAsyncLifetime
{
    private static readonly TimeSpan StartupTimeout = TimeSpan.FromMinutes(5);

    private DistributedApplication? app;

    public HttpClient Client { get; private set; } = null!;

    public async ValueTask InitializeAsync()
    {
        using var startup = new CancellationTokenSource(StartupTimeout);

        var appHost = await DistributedApplicationTestingBuilder.CreateAsync<Projects.TestingGhrepotemplate_AppHost>(
            [],
            (options, settings) =>
            {
                options.TrustDeveloperCertificate = true;

                // The AppHost reads its parameters from user secrets, which are only loaded in development.
                settings.EnvironmentName = Environments.Development;

                // Supplied as configuration rather than as command line arguments, because the
                // AppHost does not receive the latter when it is started by the testing builder.
                settings.Configuration ??= new ConfigurationManager();
                settings.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    // Isolates this run from the persistent containers of the local development
                    // environment, whose database the EF migrations would otherwise be applied to.
                    ["TestRunId"] = DateTime.Now.ToString("yyyyMMdd-HHmmssfff", CultureInfo.InvariantCulture),
                });
            },
            startup.Token);

        this.app = await appHost.BuildAsync(startup.Token);
        await this.app.StartAsync(startup.Token);

        // Starting the SQL Server container and applying the migrations takes a while on a cold agent.
        await this.app.ResourceNotifications
            .WaitForResourceHealthyAsync("backend", startup.Token);

        this.Client = this.app.CreateHttpClient("backend", "https");
        this.Client.BaseAddress = new Uri(this.Client.BaseAddress!, "api/");
    }

    public async ValueTask DisposeAsync()
    {
        this.Client?.Dispose();

        if (this.app is not null)
        {
            await this.app.DisposeAsync();
        }
    }
}
