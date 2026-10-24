using CreatorPlatform.Media.Application.Interfaces;
using CreatorPlatform.Media.Application.Options;
using CreatorPlatform.Media.Application.Services;
using CreatorPlatform.Media.Infrastructure.Persistence;
using CreatorPlatform.Media.Infrastructure.Repositories;
using CreatorPlatform.Media.Infrastructure.Services;
using CreatorPlatform.Media.Infrastructure.Workspaces;
using CreatorPlatform.Shared.Application.Workspaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace CreatorPlatform.Media.Infrastructure;

public static class MediaInfrastructureServiceCollectionExtensions
{
    public static IServiceCollection AddMediaInfrastructure(this IServiceCollection services)
    {
        services.AddScoped<IBlobReferenceRepository, BlobReferenceRepository>();
        services.AddScoped<IMediaUnitOfWork, MediaUnitOfWork>();
        services.AddScoped<ICreatorContextProvider, CreatorContextProvider>();
        services.AddScoped<IBlobStorageClient, AzureBlobStorageClient>();
        services.AddScoped<IProductFileStorage, AzureProductFileStorage>();
        services.AddSingleton<IValidateOptions<MediaOptions>, MediaOptionsValidator>();
        services.AddScoped<IMediaUploadService, MediaUploadService>();
        services.AddScoped<CreatorPlatform.Shared.Application.Media.IOwnedImageCheck, OwnedImageCheck>();

        services.AddScoped<IWorkspaceDataPurger, MediaWorkspaceDataPurger>();
        services.AddScoped<IWorkspaceFiles, AzureWorkspaceFiles>();

        return services;
    }
}
