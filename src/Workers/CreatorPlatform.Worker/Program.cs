using CreatorPlatform.Worker;
using CreatorPlatform.Creators.Application.Interfaces;
using CreatorPlatform.Creators.Infrastructure.Services;
using CreatorPlatform.Email.Application.Interfaces;
using CreatorPlatform.Email.Infrastructure;
using CreatorPlatform.Marketing.Application.Interfaces;
using CreatorPlatform.Marketing.Application.Options;
using CreatorPlatform.Marketing.Application.Services;
using CreatorPlatform.Marketing.Infrastructure.Persistence;
using CreatorPlatform.Marketing.Infrastructure.Repositories;
using CreatorPlatform.Marketing.Infrastructure.Services;
using CreatorPlatform.Auth.Infrastructure.Maintenance;
using CreatorPlatform.Email.Infrastructure.Maintenance;
using CreatorPlatform.Media.Infrastructure.Maintenance;
using CreatorPlatform.Shared.Infrastructure.Maintenance;
using CreatorPlatform.Shared.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddDbContext<CreatorPlatformDbContext>(options =>
{
    var connectionString = builder.Configuration.GetConnectionString("Database");

    options.UseNpgsql(connectionString).UseEntityConfigurationsFrom(WorkerEntityConfigurations.Assemblies);
});

builder.Services.AddEmailInfrastructure(builder.Configuration, builder.Environment);

builder.Services.Configure<MarketingOptions>(builder.Configuration.GetSection(MarketingOptions.SectionName));
// The unsubscribe page lives in the same web app the other emails link to.
builder.Services.PostConfigure<MarketingOptions>(options =>
{
    if (string.IsNullOrWhiteSpace(options.FrontendBaseUrl))
        options.FrontendBaseUrl = builder.Configuration["Email:FrontendBaseUrl"] ?? string.Empty;
});

// Hand-picked registrations, not the full AddMarketingInfrastructure()/AddCreatorsInfrastructure()
// bundles — this worker only ever needs the CampaignBroadcast send-failure handler (usage-counter
// refund on terminal outbox fail) and, since Task 14, the scheduled-campaign dispatch path
// (ICampaignSendService.DispatchScheduledAsync + everything its pipeline touches: audience resolution,
// recipients, rendering, unsubscribe tokens, the Marketing unit of work, and Marketing's own
// creator-context lookup). Registering each individually keeps this process's dependency graph — and
// its config surface (MarketingOptions, now required here too) — as narrow as what it actually uses;
// AddCreatorsInfrastructure() in full is still NOT called (Stripe Connect etc. remain unneeded here).
builder.Services.AddScoped<ICreatorUsageService, CreatorUsageService>();
builder.Services.AddScoped<ICampaignRepository, CampaignRepository>();
builder.Services.AddScoped<IEmailSendFailureHandler, CampaignBroadcastFailureHandler>();
builder.Services.AddScoped<ICampaignRecipientRepository, CampaignRecipientRepository>();
builder.Services.AddScoped<IEmailTemplateRepository, EmailTemplateRepository>();
builder.Services.AddScoped<IAudienceService, AudienceService>();
builder.Services.AddScoped<ICampaignProgressProvider, CampaignProgressProvider>();
builder.Services.AddScoped<IMarketingUnitOfWork, MarketingUnitOfWork>();
builder.Services.AddScoped<ICreatorContextProvider, CreatorContextProvider>();
builder.Services.AddSingleton<ICampaignEmailRenderer, CampaignEmailRenderer>();
builder.Services.AddSingleton<IUnsubscribeTokenService, UnsubscribeTokenService>();
builder.Services.AddSingleton<IOpenTrackingTokenService, OpenTrackingTokenService>();
builder.Services.AddSingleton<IClickTrackingTokenService, ClickTrackingTokenService>();
builder.Services.AddScoped<ICampaignLinkRepository, CampaignLinkRepository>();
builder.Services.AddScoped<ICampaignSendService, CampaignSendService>();

// Cleanup (retention): each module registers the tasks for its own tables.
builder.Services.AddMaintenance(builder.Configuration);
builder.Services.AddEmailMaintenanceTasks();
builder.Services.AddAuthMaintenanceTasks();
builder.Services.AddMediaMaintenanceTasks(builder.Configuration);

builder.Services.AddHostedService<EmailOutboxWorker>();
builder.Services.AddHostedService<ScheduledCampaignDispatchWorker>();
builder.Services.AddHostedService<MaintenanceWorker>();

var host = builder.Build();
host.Run();
