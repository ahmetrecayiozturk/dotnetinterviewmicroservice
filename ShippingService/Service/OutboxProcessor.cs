using common.Events;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using ShippingService.Infrastructure;
using System.Text.Json;

namespace ShippingService.Service;

public class OutboxProcessor : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<OutboxProcessor> _logger;

    public OutboxProcessor(IServiceProvider serviceProvider, ILogger<OutboxProcessor> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("📤 [SHIPPING OUTBOX] Processor başlatıldı");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<ShippingDbContext>();
                var publisher = scope.ServiceProvider.GetRequiredService<IPublishEndpoint>();

                var messages = await db.OutboxMessages
                    .Where(m => !m.IsProcessed && m.RetryCount < 5)
                    .OrderBy(m => m.CreatedAt)
                    .Take(10)
                    .ToListAsync(stoppingToken);

                foreach (var message in messages)
                {
                    try
                    {
                        object? eventObj = null;

                        if (message.EventType == nameof(ShippingPreparedEvent))
                        {
                            eventObj = JsonSerializer.Deserialize<ShippingPreparedEvent>(message.Payload);
                        }
                        else if (message.EventType == nameof(ShippingFailedEvent))
                        {
                            eventObj = JsonSerializer.Deserialize<ShippingFailedEvent>(message.Payload);
                        }
                        else
                        {
                            eventObj = null;
                        }

                        if (eventObj != null)
                        {
                            await publisher.Publish(eventObj, stoppingToken);

                            message.IsProcessed = true;
                            message.ProcessedAt = DateTime.UtcNow;
                            await db.SaveChangesAsync(stoppingToken);

                            _logger.LogInformation(
                                "📤 [SHIPPING OUTBOX] [{CorrelationId}] Event gönderildi: {EventType}",
                                message.CorrelationId, message.EventType);
                        }
                    }
                    catch (Exception ex)
                    {
                        message.RetryCount++;
                        message.LastError = ex.Message;
                        await db.SaveChangesAsync(stoppingToken);

                        _logger.LogError(
                            ex,
                            "❌ [SHIPPING OUTBOX] [{CorrelationId}] Event gönderilemedi (Deneme {RetryCount}/5): {MessageId}",
                            message.CorrelationId, message.RetryCount, message.Id);
                    }
                }

                await Task.Delay(3000, stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ [SHIPPING OUTBOX] Processor hatası");
                await Task.Delay(10000, stoppingToken);
            }
        }
    }
}