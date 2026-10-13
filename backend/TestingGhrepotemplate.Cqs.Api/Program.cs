using System.Text.Json;
using Azure.Core;
using Azure.Identity;
using FluentValidation;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Infrastructure;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Net.Http.Headers;
using NJsonSchema.Generation;
using softaware.Cqs.Decorators.FluentValidation;
using softaware.Cqs.Decorators.OpenTelemetry;
using TestingGhrepotemplate.Cqs;
using TestingGhrepotemplate.Cqs.Api;
using TestingGhrepotemplate.Cqs.Exceptions;
using TestingGhrepotemplate.Cqs.Logging;
using TestingGhrepotemplate.EntityFramework;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

builder.AddAzureBlobServiceClient(connectionName: "blobs");

// Use the signed in user in Visual Studio during development (VS options: Azure Service Authentication)
// and managed identity in production.
var azureCredential = new DefaultAzureCredential();

// TODO: Add "KeyVault" setting in appsettings.json
var keyVault = builder.Configuration["KeyVault"];
var keyVaultUri = string.IsNullOrEmpty(keyVault) ? null : new Uri($"https://{keyVault}.vault.azure.net/");
builder.AddKeyVaultConfigurationProvider(keyVaultUri, azureCredential);

builder.AddSqlServerDbContext<TestingGhrepotemplateContext>("database");

builder.Services.AddHealthChecks();

builder.Services.AddHsts(options =>
{
    options.Preload = true;
    options.IncludeSubDomains = true;
    options.MaxAge = TimeSpan.FromDays(365);
});

// Use the Problem Details format
builder.Services.AddProblemDetails();

// Add auth
// TODO: Configure properly
builder.Services.AddAuthorizationBuilder()
    .AddDefaultPolicy("default", new AuthorizationPolicy(
        [new AssertionRequirement(c => true)],
        [CookieAuthenticationDefaults.AuthenticationScheme]));

builder.Services.AddAuthentication(options =>
{
    options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
}).AddCookie();

// Add NSwag Open API document
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddOpenApiDocument(document =>
{
    document.Title = "TestingGhrepotemplate API";
    document.SchemaSettings.GenerateEnumMappingDescription = true;
    ((SystemTextJsonSchemaGeneratorSettings)document.SchemaSettings).SerializerOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web);

    // TODO: configure Open API authentication
    // document.AddSecurity("JWT", Enumerable.Empty<string>(), new OpenApiSecurityScheme
    // {
    //     Type = OpenApiSecuritySchemeType.ApiKey,
    //     Name = "Authorization",
    //     In = OpenApiSecurityApiKeyLocation.Header,
    //     Description = "Type into the textbox: Bearer {your JWT token}."
    // });

    // document.OperationProcessors.Add(
    //     new AspNetCoreOperationSecurityScopeProcessor("JWT"));
});

builder.Services.AddValidatorsFromAssemblyContaining<TestingGhrepotemplate.Cqs.Items.ItemDtoValidator>();

// Add softaware CQS handlers and decorators
builder.Services.AddSoftawareCqs(a => a.IncludeTypesFrom(typeof(CqsAssemblyMarker)))
    .AddDecorators(b => b
        .AddRequestHandlerDecorator(typeof(ActivityRequestHandlerDecorator<,>))
        .AddRequestHandlerDecorator(typeof(FluentValidationRequestHandlerDecorator<,>))
        .AddRequestHandlerDecorator(typeof(RequestLoggingDecorator<,>))
        .AddRequestHandlerDecorator(typeof(CommandLoggingDecorator<,>)));


// Add YARP to forward requests to the SPA dev server during development
if (builder.Environment.IsDevelopment())
{
    builder.Services.AddHttpForwarder();
}

// Add additional services
builder.Services.AddHttpContextAccessor();


// Configure middleware pipeline
var app = builder.Build();

app.UseCustomExceptionHandling();

app.UseSecurityHeaders();

app.UseHttpsRedirection();

app.UseStaticFiles();

if (app.Environment.IsDevelopment())
{
    app.UseOpenApi();
    app.UseSwaggerUi();
}

if (app.Environment.IsProduction())
{
    app.UseHsts();
}

app.UseRouting();
app.UseAuthorization();

app.MapGroup("api")
   .MapCqsEndpoints()
   .RequireAuthorization();

app.MapHealthChecks("/health",
    new HealthCheckOptions
    {
        ResponseWriter = HealthCheckResponseWriter.WriteResponse,
    });

app.UseSpa();

app.Run();

namespace TestingGhrepotemplate.Cqs.Api
{
    public static class Extensions
    {
        public static void AddKeyVaultConfigurationProvider(this WebApplicationBuilder builder, Uri? keyVaultUri, TokenCredential credential)
        {
            if (keyVaultUri != null)
            {
                // Add KeyVault first so that values can be overridden by other providers
                var sources = builder.Configuration.Sources.ToList();
                builder.Configuration.Sources.Clear();
                builder.Configuration.AddAzureKeyVault(keyVaultUri, credential);
                foreach (var source in sources)
                {
                    builder.Configuration.Sources.Add(source);
                }
            }
        }

        public static void UseSecurityHeaders(this WebApplication app)
        {
            app.UseXContentTypeOptions();
            app.UseXfo(options => options.SameOrigin());
            app.UseReferrerPolicy(options => options.NoReferrerWhenDowngrade());

            app.Use(async (context, next) =>
            {
                context.Response.Headers.Append("Permissions-Policy", "accelerometer=(), camera=(), geolocation=(), gyroscope=(), magnetometer=(), microphone=(), payment=(), usb=()");
                await next();
            });

            app.UseCsp(options =>
            {
                options = options
                    .DefaultSources(s => s.Self()
                        .CustomSources("data:"))
                    .ImageSources(s => s.Self()
                        .CustomSources("data:"))
                    .FontSources(s => s.Self()
                        .CustomSources("data:"))
                    .StyleSources(s => s.Self()
                        .UnsafeInline()) // necessary for Angular applications
                    .ScriptSources(s => s.Self()
                        .UnsafeInline()) // necessary for Angular applications
                    .ConnectSources(s => s.Self()
                        .CustomSources("https://*.applicationinsights.azure.com/")
                    );

                if (app.Environment.IsDevelopment())
                {
                    // necessary for browser link
                    options.ConnectSources(s => s.Self()
                        .CustomSources(
                            "wss:",
                            "ws://localhost:*", // BrowserLink SignalR
                            "http://localhost:*", // BrowserLink SignalR
                            "https://*.applicationinsights.azure.com/"));
                }
            });
        }

        public static void UseSpa(this WebApplication app)
        {
            app.UseWhen(context => HttpMethods.IsGet(context.Request.Method), builder =>
            {
                if (app.Environment.IsDevelopment())
                {
                    app.MapForwarder("/{**catch-all}", "http://localhost:4200");
                }
                else
                {
                    app.MapFallbackToFile("index.html", new StaticFileOptions()
                    {
                        OnPrepareResponse = ctx =>
                        {
                            var headers = ctx.Context.Response.GetTypedHeaders();
                            headers.CacheControl = new CacheControlHeaderValue
                            {
                                Public = true,
                                NoCache = true,
                                NoStore = true,
                                MustRevalidate = true,
                                MaxAge = TimeSpan.FromDays(0)
                            };
                        }
                    });
                }
            });
        }

        public static void UseCustomExceptionHandling(this WebApplication app)
        {
            app.UseStatusCodePages();

            app.UseExceptionHandler(new ExceptionHandlerOptions
            {
                StatusCodeSelector = ex => ex switch
                {
                    NotFoundException => StatusCodes.Status404NotFound,
                    ValidationException => StatusCodes.Status400BadRequest,
                    ForbiddenException => StatusCodes.Status403Forbidden,
                    UnauthorizedException => StatusCodes.Status401Unauthorized,
                    UnauthorizedAccessException => StatusCodes.Status401Unauthorized,
                    NotImplementedException => StatusCodes.Status501NotImplemented,
                    _ => StatusCodes.Status500InternalServerError
                }
            });
        }
    }
}
