using Microsoft.Extensions.DependencyInjection;

namespace ForgeSharp.Mapper.DependencyInjection;

/// <summary>
/// Provides extension methods for registering the mapper service in the DI container.
/// </summary>
public static class MapperServiceExtension
{
    /// <summary>
    /// Adds the mapper service and related services to the service collection.
    /// </summary>
    /// <typeparam name="TBuilder">The type of the mapper builder.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <returns>The updated service collection.</returns>
    public static IServiceCollection AddMapper<TBuilder>(this IServiceCollection services) where TBuilder : MapperBuilder
    {
        /*
         * I genuinely dislike that I have to make this into its own project but its best to
         * remove the Microsoft.Extensions.DependencyInjection dependency from the core library.
         * And this certainly does not warrent its own repository. So for now it'll just be its
         * own NuGet package.
         */

        services.AddTransient<MapperBuilder, TBuilder>();
        services.AddSingleton<IMapperService, MapperService>();
        services.AddTransient<IMapperTester, MapperTester>();

        return services;
    }
}
