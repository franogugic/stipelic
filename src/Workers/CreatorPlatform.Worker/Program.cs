using CreatorPlatform.Worker;
using CreatorPlatform.Email.Infrastructure;
using CreatorPlatform.Shared.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddDbContext<CreatorPlatformDbContext>(options =>
{
    var connectionString = builder.Configuration.GetConnectionString("Database");

    options.UseNpgsql(connectionString);
});

builder.Services.AddEmailInfrastructure(builder.Configuration);
builder.Services.AddHostedService<EmailOutboxWorker>();

var host = builder.Build();
host.Run();
