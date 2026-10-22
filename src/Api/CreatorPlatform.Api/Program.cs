using CreatorPlatform.Api.Health;
using CreatorPlatform.Api.Middlewares;
using CreatorPlatform.Api.RateLimiting;
using CreatorPlatform.Api.Responses;
using CreatorPlatform.Auth.Application.Options;
using CreatorPlatform.Auth.Infrastructure;
using CreatorPlatform.Creators.Infrastructure;
using CreatorPlatform.Email.Infrastructure;
using CreatorPlatform.Payments.Application.Options;
using CreatorPlatform.Payouts.Application.Options;
using CreatorPlatform.Marketing.Application.Options;
using CreatorPlatform.LandingPages.Infrastructure;
using CreatorPlatform.Orders.Application.Options;
using CreatorPlatform.Orders.Infrastructure;
using CreatorPlatform.Access.Infrastructure;
using CreatorPlatform.Payments.Infrastructure;
using CreatorPlatform.Payouts.Infrastructure;
using CreatorPlatform.Marketing.Infrastructure;
using CreatorPlatform.Analytics.Infrastructure;
using CreatorPlatform.Products.Infrastructure;
using CreatorPlatform.Media.Application.Options;
using CreatorPlatform.Media.Infrastructure;
using CreatorPlatform.Shared.Infrastructure.Persistence;
using CreatorPlatform.Shared.Infrastructure.Redis;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Net;
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.AddControllers();
builder.Services.AddCors(options =>
{
    options.AddPolicy("Frontend", policy =>
    {
        policy
            .WithOrigins(
                "http://localhost:3000",
                "http://localhost:3001",
                "http://127.0.0.1:3000",
                "http://127.0.0.1:3001")
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials();
    });
});
builder.Services.Configure<AuthOptions>(builder.Configuration.GetSection(AuthOptions.SectionName));
builder.Services.Configure<StripeOptions>(builder.Configuration.GetSection(StripeOptions.SectionName));
builder.Services.Configure<OrdersOptions>(builder.Configuration.GetSection(OrdersOptions.SectionName));
builder.Services.Configure<PayoutsOptions>(builder.Configuration.GetSection(PayoutsOptions.SectionName));
builder.Services.Configure<MarketingOptions>(builder.Configuration.GetSection(MarketingOptions.SectionName));
// The unsubscribe page lives in the same web app the other emails link to.
builder.Services.PostConfigure<MarketingOptions>(options =>
{
    if (string.IsNullOrWhiteSpace(options.FrontendBaseUrl))
        options.FrontendBaseUrl = builder.Configuration["Email:FrontendBaseUrl"] ?? string.Empty;
});
builder.Services.Configure<MediaOptions>(builder.Configuration.GetSection(MediaOptions.SectionName));
builder.Services.Configure<ApiBehaviorOptions>(options =>
{
    options.InvalidModelStateResponseFactory = ValidationErrorResponseFactory.Create;
});
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(RateLimitPolicies.GlobalPartition);
    options.OnRejected = async (context, ct) =>
    {
        context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
        context.HttpContext.Response.ContentType = "application/json";

        await context.HttpContext.Response.WriteAsJsonAsync(new ApiErrorResponse
        {
            StatusCode = StatusCodes.Status429TooManyRequests,
            Message = "Too many requests. Please try again later.",
            Code = "RATE_LIMITED"
        }, ct);
    };

    foreach (var policy in RateLimitPolicies.Catalog)
        options.AddPolicy(policy.Name, httpContext => RateLimitPolicies.Partition(httpContext, policy));
});

builder.Services.AddAuthInfrastructure();
builder.Services.AddCreatorsInfrastructure();
builder.Services.AddProductsInfrastructure();
builder.Services.AddLandingPagesInfrastructure();
builder.Services.AddEmailInfrastructure(builder.Configuration);
builder.Services.AddPaymentsInfrastructure();
builder.Services.AddPayoutsInfrastructure();
builder.Services.AddMarketingInfrastructure();
builder.Services.AddMediaInfrastructure();
builder.Services.AddAnalyticsInfrastructure();
builder.Services.AddSingleton<CreatorPlatform.Creators.Application.Interfaces.ICreatorCacheInvalidator, CreatorPlatform.Api.Caching.CreatorCacheInvalidator>();
builder.Services.AddOrdersInfrastructure();
builder.Services.AddAccessInfrastructure();
builder.Services.AddOptions<LoginAttemptLimiterOptions>();
builder.Services.AddSingleton<InMemoryLoginAttemptLimiter>();
builder.Services.AddSingleton<ILoginAttemptLimiter, RedisLoginAttemptLimiter>();
builder.Services.AddRedis(builder.Configuration, builder.Environment);
builder.Services.AddPlatformHealthChecks();

builder.Services.AddDbContext<CreatorPlatformDbContext>(options =>
{
    var connectionString = builder.Configuration.GetConnectionString("Database");

    options.UseNpgsql(connectionString);
});

var app = builder.Build();

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

app.UseCors("Frontend");
app.UseMiddleware<GlobalExceptionMiddleware>();
app.UseMiddleware<SessionAuthenticationMiddleware>();
app.UseRateLimiter();

app.MapControllers();
app.MapPlatformHealthChecks();

app.Run();
