using System.Diagnostics;
using System.Runtime.InteropServices;
using Aspire.Hosting.JavaScript;
using Aspire.Hosting.Pipelines;
using Microsoft.Extensions.Logging;

namespace TestingGhrepotemplate.AppHost;

public static class FrontendResourceBuilderExtensions
{
    /// <summary>
    /// Configures the local dev environment (Telerik license activation and FontAwesome npm token)
    /// for the frontend dev server shown in the Aspire dashboard.
    /// </summary>
    public static IResourceBuilder<JavaScriptAppResource> WithFrontendLicenses(
        this IResourceBuilder<JavaScriptAppResource> javaScriptApp,
        IResourceBuilder<ParameterResource> telerikLicense,
        IResourceBuilder<ParameterResource> fontAwesomeNpmToken)
    {
        var activator = javaScriptApp.ApplicationBuilder
            .AddExecutable("telerik-license-activator", "npx", javaScriptApp.Resource.WorkingDirectory, "kendo-ui-license", "activate")
            .WithEnvironment("TELERIK_LICENSE", telerikLicense);

        javaScriptApp.WithChildRelationship(activator);
        javaScriptApp.WaitForCompletion(activator);

        if (javaScriptApp.Resource.TryGetLastAnnotation<JavaScriptPackageInstallerAnnotation>(out var installer))
        {
            var installerBuilder = javaScriptApp.ApplicationBuilder.CreateResourceBuilder(installer.Resource);
            installerBuilder.WithEnvironment("FONTAWESOME_NPM_AUTH_TOKEN", fontAwesomeNpmToken);
            activator.WaitForCompletion(installerBuilder);
        }

        return javaScriptApp;
    }

    /// <summary>
    /// Builds the frontend on the host during publish and copies the build output into the
    /// backend's wwwroot, so it is served by the backend instead of being built and deployed
    /// in a separate container.
    /// </summary>
    public static IResourceBuilder<ProjectResource> PublishWithFrontendFiles(
        this IResourceBuilder<ProjectResource> backend,
        string frontendDirectory,
        IResourceBuilder<ParameterResource> telerikLicense,
        IResourceBuilder<ParameterResource> fontAwesomeNpmToken)
    {
        frontendDirectory = Path.GetFullPath(frontendDirectory);

#pragma warning disable ASPIREPIPELINES001 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
        backend.ApplicationBuilder.Pipeline.AddStep(new PipelineStep()
        {
            Name = "build-frontend",
            Resource = backend.Resource,
            // The backend container image is packaged during the Build step. Declaring this step
            // as a prerequisite of Build guarantees the frontend is built and copied into wwwroot
            // before the backend is published into its image.
            RequiredBySteps = [$"build-{backend.Resource.Name}"],
            Action = async stepContext =>
            {
                var step = stepContext.ReportingStep;
                var cancellationToken = stepContext.CancellationToken;

                try
                {
                    var backendProject = backend.Resource.GetProjectMetadata().ProjectPath;
                    var backendDirectory = Path.GetDirectoryName(backendProject)!;

                    var telerik = await telerikLicense.Resource.GetValueAsync(cancellationToken);
                    var fontAwesome = await fontAwesomeNpmToken.Resource.GetValueAsync(cancellationToken);

                    // Regenerating the API proxy (a backend build) and installing the npm
                    // dependencies are independent, so run them concurrently.
                    await Task.WhenAll(
                        RunTaskAsync(step, "Regenerating the TypeScript API proxy",
                            ct => RunAsync(stepContext, "dotnet", $"build \"{backendProject}\" --configuration Release", cancellationToken: ct), cancellationToken),
                        RunTaskAsync(step, "Installing frontend dependencies",
                            ct => RunAsync(stepContext, "npm", "ci", frontendDirectory,
                                new Dictionary<string, string> { ["FONTAWESOME_NPM_AUTH_TOKEN"] = fontAwesome ?? string.Empty }, ct), cancellationToken));

                    // The license activation needs the installed dependencies (kendo-ui-license CLI).
                    await RunTaskAsync(step, "Activating the Telerik license",
                        ct => RunAsync(stepContext, "npx", "kendo-ui-license activate", frontendDirectory,
                            new Dictionary<string, string> { ["TELERIK_LICENSE"] = telerik ?? string.Empty }, ct), cancellationToken);

                    // The frontend build needs the API proxy, the dependencies and an active license.
                    await RunTaskAsync(step, "Building the frontend",
                        ct => RunAsync(stepContext, "npm", "run build", frontendDirectory, cancellationToken: ct), cancellationToken);

                    await RunTaskAsync(step, "Copying the build output into wwwroot",
                        _ =>
                        {
                            CopyDirectory(
                                Path.Combine(frontendDirectory, "dist/testingghrepotemplate/browser"),
                                Path.Combine(backendDirectory, "wwwroot"));
                            return Task.CompletedTask;
                        }, cancellationToken);

                    await step.CompleteAsync("frontend build successful");
                }
                catch (Exception ex)
                {
                    await step.FailAsync($"frontend build failed: {ex.Message}");
                    throw;
                }
            }
        });
#pragma warning restore ASPIREPIPELINES001 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.

        return backend;
    }

#pragma warning disable ASPIREPIPELINES001 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
    private static async Task RunTaskAsync(
        IReportingStep step,
        string title,
        Func<CancellationToken, Task> action,
        CancellationToken cancellationToken)
    {
        var task = await step.CreateTaskAsync(title, cancellationToken);
        try
        {
            await action(cancellationToken);
            await task.SucceedAsync($"{title} succeeded", cancellationToken);
        }
        catch (Exception ex)
        {
            await task.FailAsync($"{title} failed: {ex.Message}", cancellationToken);
            throw;
        }
    }
#pragma warning restore ASPIREPIPELINES001 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.

#pragma warning disable ASPIREPIPELINES001 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
    // @codescene(disable:"Excess Number of Function Arguments")
    private static async Task RunAsync(
        PipelineStepContext stepContext,
        string command,
        string arguments,
        string? workingDirectory = null,
        IDictionary<string, string>? environmentVariables = null,
        CancellationToken cancellationToken = default)
    {
        // The full output is only logged at debug level. Keeping the tail around means a
        // failure still reports why it failed, even when the pipeline runs at its default
        // log level (for example on a CI agent).
        const int capturedLineLimit = 40;
        var capturedOutput = new Queue<string>(capturedLineLimit);

        void Capture(string line)
        {
            // Both redirected streams raise their events on separate threads.
            lock (capturedOutput)
            {
                if (capturedOutput.Count == capturedLineLimit)
                {
                    capturedOutput.Dequeue();
                }

                capturedOutput.Enqueue(line);
            }
        }

        var startInfo = new ProcessStartInfo
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            WindowStyle = ProcessWindowStyle.Hidden,
        };

        // npm/npx are batch scripts on Windows and cannot be started directly, so run
        // everything through the shell to resolve them via PATH consistently.
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            startInfo.FileName = "cmd.exe";
            startInfo.Arguments = $"/c {command} {arguments}";
        }
        else
        {
            startInfo.FileName = command;
            startInfo.Arguments = arguments;
        }

        if (workingDirectory is not null)
        {
            startInfo.WorkingDirectory = workingDirectory;
        }

        if (environmentVariables is not null)
        {
            foreach (var variable in environmentVariables)
            {
                startInfo.Environment[variable.Key] = variable.Value;
            }
        }

        using var process = new Process()
        {
            StartInfo = startInfo,
            EnableRaisingEvents = true,
        };

        process.OutputDataReceived += (_, e) =>
        {
            if (!string.IsNullOrEmpty(e.Data))
            {
                stepContext.Logger.LogDebug("{Command} (stdout): {Output}", command, e.Data);
                Capture(e.Data);
            }
        };

        process.ErrorDataReceived += (_, e) =>
        {
            if (!string.IsNullOrEmpty(e.Data))
            {
                stepContext.Logger.LogDebug("{Command} (stderr): {Error}", command, e.Data);
                Capture(e.Data);
            }
        };

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        await process.WaitForExitAsync(cancellationToken);

        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"'{command} {arguments}' failed with exit code {process.ExitCode}.{DescribeOutput(capturedOutput)}");
        }
    }
#pragma warning restore ASPIREPIPELINES001 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.

    private static string DescribeOutput(Queue<string> capturedOutput)
    {
        lock (capturedOutput)
        {
            return capturedOutput.Count == 0
                ? string.Empty
                : $"{Environment.NewLine}Last {capturedOutput.Count} line(s) of output:{Environment.NewLine}{string.Join(Environment.NewLine, capturedOutput)}";
        }
    }

    private static void CopyDirectory(string source, string destination)
    {
        if (!Directory.Exists(source))
        {
            throw new DirectoryNotFoundException($"Source directory not found: '{source}'");
        }

        if (Directory.Exists(destination))
        {
            Directory.Delete(destination, recursive: true);
        }

        Directory.CreateDirectory(destination);

        foreach (var file in Directory.GetFiles(source, "*", SearchOption.AllDirectories))
        {
            var target = Path.Combine(destination, Path.GetRelativePath(source, file));
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(file, target, overwrite: true);
        }
    }
}
