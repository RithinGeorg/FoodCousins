using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Configuration;
using FoodCousins.Application.Email;
using FoodCousins.Infrastructure.Email;
using FoodCousins.Infrastructure.Persistence;
using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Events;
using Serilog.Sinks.ApplicationInsights.TelemetryConverters;

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    var builder = FunctionsApplication.CreateBuilder(args);

    builder.Services.AddApplicationInsightsTelemetryWorkerService();
    builder.Services.ConfigureFunctionsApplicationInsights();

    var connectionString = builder.Configuration.GetConnectionString("FoodCousins")
        ?? throw new InvalidOperationException("ConnectionStrings:FoodCousins is required for Functions.");

    builder.Services.AddDbContext<FoodCousinsDbContext>(options =>
        options.UseSqlServer(connectionString, sql => sql.EnableRetryOnFailure()));

    builder.Services.Configure<EmailOptions>(builder.Configuration.GetSection("Email"));
    builder.Services.AddTransient<LoggingEmailSender>();
    builder.Services.AddHttpClient<SendGridEmailSender>();
    builder.Services.AddTransient<IEmailSender>(sp =>
        string.Equals(builder.Configuration["Email:Provider"], "SendGrid", StringComparison.OrdinalIgnoreCase)
            ? sp.GetRequiredService<SendGridEmailSender>()
            : sp.GetRequiredService<LoggingEmailSender>());

    builder.Logging.ClearProviders();
    builder.Services.AddSerilog((services, loggerConfiguration) => loggerConfiguration
        .MinimumLevel.Information()
        .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
        .MinimumLevel.Override("System", LogEventLevel.Warning)
        .MinimumLevel.Override("Azure", LogEventLevel.Warning)
        .Enrich.FromLogContext()
        .Enrich.WithProperty("Application", "FoodCousins.Functions")
        .WriteTo.Console()
        .WriteTo.ApplicationInsights(
            services.GetRequiredService<TelemetryConfiguration>(),
            TelemetryConverter.Traces));

    Log.Information("FoodCousins Functions worker starting");
    await builder.Build().RunAsync();
}
catch (Exception ex)
{
    Log.Fatal(ex, "FoodCousins Functions worker terminated unexpectedly");
    throw;
}
finally
{
    await Log.CloseAndFlushAsync();
}
