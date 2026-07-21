using CreatorPlatform.Worker;
using CreatorPlatform.Creators.Application.Interfaces;
using CreatorPlatform.Creators.Infrastructure.Services;
using CreatorPlatform.Email.Application.Interfaces;
using CreatorPlatform.Email.Infrastructure;
using CreatorPlatform.Marketing.Application.Interfaces;
using CreatorPlatform.Marketing.Application.Services;
using CreatorPlatform.Marketing.Infrastructure.Repositories;
using CreatorPlatform.Shared.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddDbContext<CreatorPlatformDbContext>(options =>
{
    var connectionString = builder.Configuration.GetConnectionString("Database");

    options.UseNpgsql(connectionString);
});

builder.Services.AddEmailInfrastructure(builder.Configuration);

// Minimal, hand-picked registrations for the CampaignBroadcast send-failure handler (usage-counter
// refund on terminal fail) — deliberately not the full AddMarketingInfrastructure()/
// AddCreatorsInfrastructure() bundles, since those also wire up services this worker never uses (e.g.
// send pipeline, Stripe Connect) and would require config (MarketingOptions) this process doesn't set.
builder.Services.AddScoped<ICreatorUsageService, CreatorUsageService>();
builder.Services.AddScoped<ICampaignRepository, CampaignRepository>();
builder.Services.AddScoped<IEmailSendFailureHandler, CampaignBroadcastFailureHandler>();

builder.Services.AddHostedService<EmailOutboxWorker>();

var host = builder.Build();
host.Run();
