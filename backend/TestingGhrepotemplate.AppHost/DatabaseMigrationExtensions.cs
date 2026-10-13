using Aspire.Hosting.Azure;
using Aspire.Hosting.EntityFrameworkCore;
using Aspire.Hosting.Pipelines;

namespace TestingGhrepotemplate.AppHost;

public static class DatabaseMigrationExtensions
{
    /// <summary>
    /// Wires up database migrations for <c>aspire deploy</c>.
    ///
    /// During deploy Aspire generates an idempotent EF Core migration SQL script. That script is
    /// then executed against the Azure SQL database by an Azure deployment script that runs as the
    /// SQL server's admin managed identity (the only identity with DDL access), so the application
    /// identity does not need schema-modifying rights.
    ///
    /// The migration is ordered before the app is (re)deployed. Combined with backward-compatible
    /// "expand/contract" migrations this enables zero-downtime deployments: the old app keeps
    /// running against the already-migrated schema while the new version is rolled out.
    /// </summary>
    public static IResourceBuilder<EFMigrationResource> RunDatabaseUpdateOnDeploy(
        this IResourceBuilder<EFMigrationResource> migrations)
    {
        if (migrations.ApplicationBuilder.ExecutionContext.IsRunMode)
        {
            return migrations;
        }

        migrations.PublishAsMigrationScript();

        var builder = migrations.ApplicationBuilder;
        var backend = migrations.Resource.StartupProjectResource;

        // The migration references its database via WithReference(database), which records a
        // relationship annotation. Resolve the database from it, and the SQL server from the database.
        var database = migrations.Resource.Annotations
            .OfType<ResourceRelationshipAnnotation>()
            .Select(a => a.Resource)
            .OfType<AzureSqlDatabaseResource>()
            .Single();
        var sqlServer = database.Parent;

        // Carries the generated SQL from the pipeline step (which reads the generated file) to the
        // bicep parameter callback (which is evaluated later, while the migrate module provisions).
        // Both closures capture the same local, so the value read below is visible to the callback.
        string? migrationScript = null;

        var migrate = builder
            .AddBicepTemplate("migrate-database", "migrate-database.bicep")
            .WithParameter("sqlServerFqdn", new BicepOutputReference("sqlServerFqdn", sqlServer))
            .WithParameter("sqlServerAdminName", new BicepOutputReference("sqlServerAdminName", sqlServer))
            .WithParameter("databaseName", database.DatabaseName)
            .WithParameter("migrationScript", () => migrationScript);

        var generateStep = $"{migrations.Resource.Name}-generate-migration-script";
        var provisionMigrateStep = $"provision-{migrate.Resource.Name}";

#pragma warning disable ASPIREPIPELINES001 // Type is for evaluation purposes only and is subject to change or removal in future updates.
        // Aspire wires the generation step as a prerequisite of the publish-only 'publish' step, so
        // it is orphaned (never runs) during 'aspire deploy'. Depending on it here pulls it into the
        // deploy graph; requiring this step before the migrate module provisions guarantees the SQL
        // file exists before the 'migrationScript' parameter callback reads it.
        builder.Pipeline.AddStep(new PipelineStep()
        {
            Name = "read-migration-script",
            Resource = migrations.Resource,
            DependsOnSteps = [generateStep],
            RequiredBySteps = [provisionMigrateStep, "publish"],
            Action = async stepContext =>
            {
                var step = stepContext.ReportingStep;
                try
                {
                    var path = Path.Combine(builder.AppHostDirectory, "aspire-output", "efmigrations", $"{migrations.Resource.Name}.sql");
                    migrationScript = await File.ReadAllTextAsync(path, stepContext.CancellationToken);
                    await step.CompleteAsync($"loaded migration script ({migrationScript.Length} characters)");
                }
                catch (Exception ex)
                {
                    await step.FailAsync($"failed to load migration script: {ex.Message}");
                    throw;
                }
            }
        });

        // Gate the website deployment on the migration having been applied. The compute website
        // provisioning step (provision-<backend>-website) is synthesized so late it is not yet present
        // even in this callback, so we cannot target it directly. Instead we gate the image *push*
        // step, which is registered here and which the website deployment depends on -- transitively
        // ordering the deployment after the migration, while leaving the image build free to run in
        // parallel with the migration.
        var pushStep = $"push-{backend.Name}";
        migrations.WithPipelineConfiguration(ctx =>
        {
            var migrateSteps = ctx.GetSteps(migrate.Resource).ToList();
            var push = ctx.GetSteps(backend).SingleOrDefault(s => s.Name == pushStep);
            migrateSteps.RequiredBy(push);
        });
#pragma warning restore ASPIREPIPELINES001

        return migrations;
    }
}
