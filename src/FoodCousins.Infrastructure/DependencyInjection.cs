using FoodCousins.Application.Admin;
using FoodCousins.Application.Auth;
using FoodCousins.Application.CookAtHome;
using FoodCousins.Application.Delivery;
using FoodCousins.Application.Foods;
using FoodCousins.Application.Messaging;
using FoodCousins.Application.Storage;
using FoodCousins.Infrastructure.Admin;
using FoodCousins.Infrastructure.Auth;
using FoodCousins.Infrastructure.CookAtHome;
using FoodCousins.Infrastructure.Delivery;
using FoodCousins.Infrastructure.Foods;
using FoodCousins.Infrastructure.Messaging;
using FoodCousins.Infrastructure.Persistence;
using FoodCousins.Infrastructure.Storage;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace FoodCousins.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("FoodCousins")
            ?? throw new InvalidOperationException("ConnectionStrings:FoodCousins is required.");

        services.AddDbContext<FoodCousinsDbContext>(options =>
            options.UseSqlServer(connectionString, sql => sql.EnableRetryOnFailure()));

        services.Configure<JwtOptions>(configuration.GetSection("Jwt"));
        services.Configure<OpenAiOptions>(configuration.GetSection("OpenAI"));

        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IFoodService, FoodService>();
        services.AddScoped<IDeliveryAreaService, DeliveryAreaService>();
        services.AddScoped<ICookAtHomeOrderService, CookAtHomeOrderService>();
        services.AddScoped<IAdminService, AdminService>();
        services.AddScoped<IImageStorage, AzureBlobImageStorage>();

        services.AddHttpClient<OpenAiFoodDiscoveryProvider>();
        services.AddScoped<IFoodDiscoveryProvider>(sp => sp.GetRequiredService<OpenAiFoodDiscoveryProvider>());

        services.AddSingleton<IEventPublisher, ServiceBusEventPublisher>();
        services.AddHostedService<OutboxDispatcher>();

        return services;
    }
}
