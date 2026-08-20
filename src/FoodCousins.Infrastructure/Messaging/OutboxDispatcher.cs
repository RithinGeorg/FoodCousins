using FoodCousins.Application.Messaging;
using FoodCousins.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace FoodCousins.Infrastructure.Messaging;

public sealed class OutboxDispatcher(IServiceScopeFactory scopeFactory, ILogger<OutboxDispatcher> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try { await DispatchBatchAsync(stoppingToken); }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Outbox dispatch cycle failed.");
            }

            try
            {
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }
    }

    private async Task DispatchBatchAsync(CancellationToken ct)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<FoodCousinsDbContext>();
        var publisher = scope.ServiceProvider.GetRequiredService<IEventPublisher>();
        var messages = await db.OutboxMessages.Where(x => x.ProcessedAtUtc == null && x.Attempts < 10).OrderBy(x => x.CreatedAtUtc).Take(20).ToListAsync(ct);
        foreach (var message in messages)
        {
            try
            {
                await publisher.PublishAsync(message.Type, message.Payload, ct);
                message.ProcessedAtUtc = DateTimeOffset.UtcNow;
                message.LastError = null;
            }
            catch (Exception ex)
            {
                message.Attempts++;
                message.LastError = ex.Message[..Math.Min(ex.Message.Length, 2000)];

                if (message.Attempts >= 10)
                {
                    logger.LogError(
                        ex,
                        "Outbox message exhausted retry limit. MessageId={MessageId}, Type={MessageType}, Attempt={Attempt}.",
                        message.Id,
                        message.Type,
                        message.Attempts);
                }
                else
                {
                    logger.LogWarning(
                        ex,
                        "Failed publishing outbox message. MessageId={MessageId}, Type={MessageType}, Attempt={Attempt}.",
                        message.Id,
                        message.Type,
                        message.Attempts);
                }
            }
            await db.SaveChangesAsync(ct);
        }
    }
}
