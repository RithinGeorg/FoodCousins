using FoodCousins.Application.Auth;
using FoodCousins.Application.Foods;
using FoodCousins.Application.CookAtHome;
using FoodCousins.Application.Messaging;
using FoodCousins.Application.Orders;
using FoodCousins.Application.Storage;
using FoodCousins.Infrastructure.Auth;
using FoodCousins.Infrastructure.Foods;
using FoodCousins.Infrastructure.CookAtHome;
using FoodCousins.Infrastructure.Messaging;
using FoodCousins.Infrastructure.Orders;
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
        var cs = configuration.GetConnectionString("FoodCousins") ?? throw new InvalidOperationException("ConnectionStrings:FoodCousins is required.");
        services.AddDbContext<FoodCousinsDbContext>(o => o.UseSqlServer(cs, sql => sql.EnableRetryOnFailure()));
        services.Configure<JwtOptions>(configuration.GetSection("Jwt"));
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IFoodService, FoodService>();
        services.AddScoped<IOrderService, OrderService>();
        services.AddScoped<ICookAtHomeOrderService, CookAtHomeOrderService>();
        services.AddSingleton<IImageStorage, AzureBlobImageStorage>();
        services.AddSingleton<IEventPublisher, ServiceBusEventPublisher>();
        services.AddHostedService<OutboxDispatcher>();
        return services;
    }
}
