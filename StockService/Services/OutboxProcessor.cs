using common.Events;
using MassTransit;
using StockService.Infrastructure;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace StockService.Services
{
    public class OutboxProcessor : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<OutboxProcessor> _logger;

        // Maksimum retry sayısı
        private const int MaxRetryCount = 5;
        // Bir seferde işlenecek maksimum mesaj sayısı
        private const int BatchSize = 10;
        // İşlemler arasındaki bekleme süresi (ms)
        private const int ProcessIntervalMs = 3000;

        public OutboxProcessor(IServiceProvider serviceProvider, ILogger<OutboxProcessor> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("📤 [STOCK OUTBOX] Processor başlatıldı");

            //5 saniye bekliyoruz
            await Task.Delay(5000, stoppingToken);

            //Eğer token tüm işleri bitiren bir token değilse
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await ProcessOutboxMessageAsync(stoppingToken);
                    await Task.Delay(ProcessIntervalMs, stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "❌ [STOCK OUTBOX] Processor'de hata oldu.");
                    await Task.Delay(10000, stoppingToken);
                }
            }
        }

        private async Task ProcessOutboxMessageAsync(CancellationToken stoppingToken)
        {
            //bir scope varmış gibi simüle ediyoruz
            using var scope = _serviceProvider.CreateScope();
            //bunu Program.cs'de tanımlıyoruz zaten
            var db = scope.ServiceProvider.GetRequiredService<StockDbContext>();
            //dağıtıcıyı tanımlayalım
            var publisher = scope.ServiceProvider.GetRequiredService<IPublishEndpoint>();

            //publish edilmemiş tüm outbox mesajlarını bulalım
            var messages = await db.OutboxMessages
                .Where(x => !x.IsProcessed && x.RetryCount < MaxRetryCount)
                .OrderBy(m => m.CreatedAt)
                .Take(BatchSize)
                .ToListAsync(stoppingToken);

            //Liste boşsa false dönsün
            if (!messages.Any())
            {
                return;
            }

            _logger.LogInformation("{Count} outbox mesajı işlenecek", messages.Count);

            //Her mesajı dönelim listedeki
            foreach (var message in messages)
            {
                try
                {
                    //Önce payloadı alalım
                    object? eventObj = DeserializeEvent(message.EventType, message.Payload);

                    //Eğer boşsa hata fırlatalım, process ettik ama event tipi bilinmiyor
                    if (eventObj == null)
                    {
                        _logger.LogWarning(
                            "[{CorrelationId}] Bilinmeyen event tipi: {EventType}",
                            message.CorrelationId, message.EventType);

                        //İşlediğimizi işaretleyelim
                        message.IsProcessed = true;
                        message.LastError = "Unknown event type";
                        //Save edelim değişiklikleri
                        await db.SaveChangesAsync(stoppingToken);
                        //Devam edelim
                        continue;
                    }
                    else
                    {
                        //Mesajımızı publish edelim
                        await publisher.Publish(eventObj, stoppingToken);
                        //eğer payloadı alabildiysek işlendi işareti yapalım
                        message.IsProcessed = true;
                        //DateTime'ı şu an yapalım
                        message.ProcessedAt = DateTime.UtcNow;
                        //Değişiklikleri kaydedelim
                        await db.SaveChangesAsync(stoppingToken);

                        _logger.LogInformation(
                            "📤 [STOCK OUTBOX] [{CorrelationId}] Outbox event'i gönderildi: {EventType}",
                            message.CorrelationId, message.EventType);
                    }
                }
                catch (Exception ex)
                {
                    message.RetryCount++;
                    message.LastError = ex.Message;
                    await db.SaveChangesAsync(stoppingToken);

                    if (message.RetryCount >= MaxRetryCount)
                    {
                        _logger.LogError(
                            ex,
                            "❌ [STOCK OUTBOX] [{CorrelationId}] Event gönderilemedi - MAX RETRY!",
                            message.CorrelationId);
                    }
                    else
                    {
                        _logger.LogWarning(
                            ex,
                            "⚠️ [STOCK OUTBOX] [{CorrelationId}] Event gönderilemedi (Deneme {RetryCount}/{MaxRetry})",
                            message.CorrelationId, message.RetryCount, MaxRetryCount);
                    }
                }
            }
        }

        //deserialize edelim
        private object? DeserializeEvent(string eventType, string payload)
        {
            if (eventType == nameof(StockReservedEvent))
            {
                return JsonSerializer.Deserialize<StockReservedEvent>(payload);
            }

            if (eventType == nameof(StockInsufficientEvent))
            {
                return JsonSerializer.Deserialize<StockInsufficientEvent>(payload);
            }

            if (eventType == nameof(StockReleasedEvent))
            {
                return JsonSerializer.Deserialize<StockReleasedEvent>(payload);
            }

            else
            {
                return null;
            }
        }
    }
}
