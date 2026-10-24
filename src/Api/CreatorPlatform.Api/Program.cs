using CreatorPlatform.Api.Health;
using CreatorPlatform.Api.Hosting;
using CreatorPlatform.Api.Middlewares;
using CreatorPlatform.Api.Networking;
using CreatorPlatform.Api.RateLimiting;
using CreatorPlatform.Api.Responses;
using CreatorPlatform.Api.Security;
using CreatorPlatform.Auth.Application.Options;
using CreatorPlatform.Auth.Infrastructure;
using CreatorPlatform.Creators.Infrastructure;
using CreatorPlatform.Email.Infrastructure;
using CreatorPlatform.Payments.Application.Options;
using CreatorPlatform.Marketing.Application.Options;
using CreatorPlatform.LandingPages.Infrastructure;
using CreatorPlatform.Orders.Application.Options;
using CreatorPlatform.Orders.Infrastructure;
using CreatorPlatform.Access.Infrastructure;
using CreatorPlatform.Payments.Infrastructure;
using CreatorPlatform.Marketing.Infrastructure;
using CreatorPlatform.Analytics.Infrastructure;
using CreatorPlatform.Products.Infrastructure;
using CreatorPlatform.Media.Application.Options;
using CreatorPlatform.Media.Infrastructure;
using CreatorPlatform.Shared.Infrastructure.Persistence;
using CreatorPlatform.Shared.Infrastructure.Redis;
using CreatorPlatform.Shared.Infrastructure.Workspaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Net;

// `--migrate`: apply the database migrations and exit (the production one-shot step); see DatabaseMigrationCommand.
var migrateOnly = DatabaseMigrationCommand.TryTake(args, out var hostArgs);

var builder = WebApplication.CreateBuilder(hostArgs);

builder.Services.AddOpenApi();
builder.Services.AddControllers();
builder.Services.AddFrontendCors(builder.Configuration);
builder.Services.AddPlatformSecurity(builder.Configuration, builder.Environment);
builder.Services.Configure<AuthOptions>(builder.Configuration.GetSection(AuthOptions.SectionName));
builder.Services.Configure<StripeOptions>(builder.Configuration.GetSection(StripeOptions.SectionName));
builder.Services.Configure<OrdersOptions>(builder.Configuration.GetSection(OrdersOptions.SectionName));
builder.Services.Configure<MarketingOptions>(builder.Configuration.GetSection(MarketingOptions.SectionName));
// The unsubscribe page lives in the same web app the other emails link to.
builder.Services.PostConfigure<MarketingOptions>(options =>
{
    if (string.IsNullOrWhiteSpace(options.FrontendBaseUrl))
        options.FrontendBaseUrl = builder.Configuration["Email:FrontendBaseUrl"] ?? string.Empty;
});
builder.Services.AddOptions<MediaOptions>()
    .Bind(builder.Configuration.GetSection(MediaOptions.SectionName))
    .ValidateOnStart();
builder.Services.Configure<ApiBehaviorOptions>(options =>
{
    options.InvalidModelStateResponseFactory = ValidationErrorResponseFactory.Create;
});
builder.Services.AddTrustedProxies(builder.Configuration);
builder.Services.AddPlatformRateLimiter();

builder.Services.AddAuthInfrastructure();
builder.Services.AddCreatorsInfrastructure();
builder.Services.AddProductsInfrastructure();
builder.Services.AddLandingPagesInfrastructure();
builder.Services.AddEmailInfrastructure(builder.Configuration, builder.Environment);
builder.Services.AddPaymentsInfrastructure();
builder.Services.AddScoped<CreatorPlatform.Api.Webhooks.StripeWebhookProcessor>();
builder.Services.AddScoped<CreatorPlatform.Api.Webhooks.WebhookFailureAdminService>();
builder.Services.AddMarketingInfrastructure();
builder.Services.AddMediaInfrastructure();
builder.Services.AddAnalyticsInfrastructure();
builder.Services.AddSingleton<CreatorPlatform.Creators.Application.Interfaces.ICreatorCacheInvalidator, CreatorPlatform.Api.Caching.CreatorCacheInvalidator>();
builder.Services.AddOrdersInfrastructure();
builder.Services.AddAccessInfrastructure();
// Deleting a workspace: every module with tables erases its own rows of it.
builder.Services.AddWorkspaceDataPurgers(ApiEntityConfigurations.Assemblies);
builder.Services.AddOptions<LoginAttemptLimiterOptions>();
builder.Services.AddSingleton<InMemoryLoginAttemptLimiter>();
builder.Services.AddSingleton<ILoginAttemptLimiter, RedisLoginAttemptLimiter>();
builder.Services.AddRedis(builder.Configuration, builder.Environment);
builder.Services.AddPlatformHealthChecks();

builder.Services.AddDbContext<CreatorPlatformDbContext>(options =>
{
    var connectionString = builder.Configuration.GetConnectionString("Database");

    options.UseNpgsql(connectionString).UseEntityConfigurationsFrom(ApiEntityConfigurations.Assemblies);
});

var app = builder.Build();

if (migrateOnly)
{
    var exitCode = await DatabaseMigrationCommand.RunAsync(app.Services, app.Logger, CancellationToken.None);
    await app.DisposeAsync();   // flushes the console logger before the process ends
    Environment.ExitCode = exitCode;
    return;
}

// First: everything below (HTTPS redirection, rate limits, the login limiter) sees the client's address and scheme.
app.UseTrustedForwardedHeaders();
app.UsePlatformSecurityHeaders(app.Environment);

if (app.Configuration.GetValue<bool>("Database:AutoMigrate"))
{
    using var scope = app.Services.CreateScope();
    var dbContext = scope.ServiceProvider.GetRequiredService<CreatorPlatformDbContext>();
    await dbContext.Database.MigrateAsync();
}

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.UseCors(FrontendCorsOptions.PolicyName);
// After CORS (a refused request still carries the CORS headers the SPA needs to read it), before anything acts.
app.UseMiddleware<AntiForgeryHeaderMiddleware>();
app.UseMiddleware<GlobalExceptionMiddleware>();
app.UseMiddleware<SessionAuthenticationMiddleware>();
app.UseRateLimiter();

app.MapControllers();
app.MapPlatformHealthChecks();

app.Run();
