using CreatorPlatform.Media.Application.Interfaces;
using CreatorPlatform.Media.Application.Services;
using CreatorPlatform.Media.Infrastructure.Persistence;
using CreatorPlatform.Media.Infrastructure.Repositories;
using CreatorPlatform.Media.Infrastructure.Services;
using Microsoft.Extensions.DependencyInjection;

namespace CreatorPlatform.Media.Infrastructure;

public static class MediaInfrastructureServiceCollectionExtensions
{
    public static IServiceCollection AddMediaInfrastructure(this IServiceCollection services)
    {
        services.AddScoped<IBlobReferenceRepository, BlobReferenceRepository>();
        services.AddScoped<IMediaUnitOfWork, MediaUnitOfWork>();
        services.AddScoped<ICreatorContextProvider, CreatorContextProvider>();
        services.AddScoped<IBlobStorageClient, AzureBlobStorageClient>();
        services.AddScoped<IMediaUploadService, MediaUploadService>();

        return services;
    }
}
