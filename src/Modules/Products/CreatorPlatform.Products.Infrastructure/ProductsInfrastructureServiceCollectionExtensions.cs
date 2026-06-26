using CreatorPlatform.Products.Application.Interfaces;
using CreatorPlatform.Products.Application.Services;
using CreatorPlatform.Products.Infrastructure.Persistence;
using CreatorPlatform.Products.Infrastructure.Repositories;
using CreatorPlatform.Products.Infrastructure.Services;
using Microsoft.Extensions.DependencyInjection;

namespace CreatorPlatform.Products.Infrastructure;

public static class ProductsInfrastructureServiceCollectionExtensions
{
    public static IServiceCollection AddProductsInfrastructure(this IServiceCollection services)
    {
        services.AddScoped<IProductService, ProductService>();
        services.AddScoped<IProductRepository, ProductRepository>();
        services.AddScoped<ICreatorContextProvider, CreatorContextProvider>();
        services.AddScoped<IProductsUnitOfWork, ProductsUnitOfWork>();

        return services;
    }
}
