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
using CreatorPlatform.Shared.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

var builder = Host.CreateApplicationBuilder(args);

// CreatorPlatformDbContext.OnModelCreating only applies IEntityTypeConfiguration<T> from
// *.Infrastructure assemblies that are already loaded into this process's AppDomain (see the comment
// there) — the API loads every module because it directly registers a concrete service from each of
// them, which forces the CLR to load the assembly; this worker registers far fewer services (by design,
// see below), so several entities the Task 14 dispatch pipeline touches only via LINQ (never through a
// registered service) would otherwise have no mapping at all. A `typeof(...)` reference is NOT enough to
// force this — the CLR can resolve a type token without loading the assembly's other contents — so each
// needs an explicit Assembly.Load by name:
//   - Auth.Infrastructure: User, read by Marketing's ICreatorContextProvider.GetByCreatorIdAsync to
//     resolve the owner's fallback reply-to email.
//   - Analytics.Infrastructure: EmailCapture, queried by AudienceService when resolving the real
//     audience at dispatch time (never referenced by this project otherwise, so also added as a new
//     ProjectReference in the .csproj).
//   - LandingPages.Infrastructure: LandingPage, joined by AudienceService for the Product-audience case
//     (already project-referenced for other reasons, but never touched by any registered service here).
System.Reflection.Assembly.Load("CreatorPlatform.Auth.Infrastructure");
System.Reflection.Assembly.Load("CreatorPlatform.Analytics.Infrastructure");
System.Reflection.Assembly.Load("CreatorPlatform.LandingPages.Infrastructure");

builder.Services.AddDbContext<CreatorPlatformDbContext>(options =>
{
    var connectionString = builder.Configuration.GetConnectionString("Database");

    options.UseNpgsql(connectionString);
});

builder.Services.AddEmailInfrastructure(builder.Configuration);

builder.Services.Configure<MarketingOptions>(builder.Configuration.GetSection(MarketingOptions.SectionName));

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
builder.Services.AddScoped<ICampaignSendService, CampaignSendService>();

builder.Services.AddHostedService<EmailOutboxWorker>();
builder.Services.AddHostedService<ScheduledCampaignDispatchWorker>();

var host = builder.Build();
host.Run();
