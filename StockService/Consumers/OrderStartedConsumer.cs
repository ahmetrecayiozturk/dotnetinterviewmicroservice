using common.Events;
using MassTransit;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using StockService.Application.Commands;
using StockService.Domain;
using StockService.Infrastructure;

namespace StockService.Consumers;

public class OrderStartedConsumer : IConsumer<OrderStartedEvent>
{
    private readonly StockDbContext _stockDbContext;
    private readonly IMediator _mediator;
    private readonly ILogger<OrderStartedEvent> _logger;

    public OrderStartedConsumer(StockDbContext stockDbContext, IMediator mediator, ILogger<OrderStartedEvent> logger)
    {
        _stockDbContext = stockDbContext;
        _mediator = mediator;
        _logger = logger;
    }

    // Direkt sadece OrderStartedEvent gelince çalışacak
    public async Task Consume(ConsumeContext<OrderStartedEvent> context)
    {
        var messageId = context.MessageId;
        var correlationId = context.CorrelationId;

        _logger.LogInformation(
            "📦 [STOCK] [{CorrelationId}] OrderStartedEvent alındı: {OrderId}",
            correlationId, context.Message.OrderId);

        // EF Core AnyAsync
        var processed = await _stockDbContext.ProcessedEvents
            .AnyAsync(e => e.MessageId == messageId, context.CancellationToken);

        if (processed)
        {
            _logger.LogWarning(
                "⚠️ [STOCK] [{CorrelationId}] Event zaten işlenmiş: {MessageId}",
                correlationId, messageId);
            return;
        }

        var command = new ReserveStockCommand
        {
            OrderId = context.Message.OrderId,
            CorrelationId = correlationId ?? Guid.Empty, // MassTransit Guid? ise
            ProductId = context.Message.ProductId,
            Quantity = context.Message.Quantity
        };

        // Burada kullandığımız MediatR ile komutu handler'a gönderiyoruz
        var result = await _mediator.Send(command, context.CancellationToken);

        _stockDbContext.ProcessedEvents.Add(new ProcessedEvent
        {
            Id = Guid.NewGuid(),
            MessageId = messageId ?? Guid.Empty,
            EventType = nameof(OrderStartedEvent)
        });

        await _stockDbContext.SaveChangesAsync(context.CancellationToken);
    }
}