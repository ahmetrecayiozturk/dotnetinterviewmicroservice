using common.Events;
using MassTransit;
using MediatR;
using Microsoft.EntityFrameworkCore;
using ShippingService.Application.Commands;
using ShippingService.Domain;
using ShippingService.Infrastructure;

namespace ShippingService.Consumer;
public class ShippingConsumer : IConsumer<PaymentCompletedEvent>
{
    private readonly ShippingDbContext _db;
    private readonly IMediator _mediator;
    private readonly ILogger<ShippingConsumer> _logger;

    public ShippingConsumer(ShippingDbContext db, IMediator mediator, ILogger<ShippingConsumer> logger)
    {
        _db = db;
        _mediator = mediator;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<PaymentCompletedEvent> context)
    {
        var messageId = context.MessageId ?? Guid.NewGuid();
        var correlationId = context.Message.CorrelationId;

        _logger.LogInformation("📦 [SHIPPING] [{CorrelationId}] PaymentCompletedEvent alındı: {OrderId}",
            correlationId, context.Message.OrderId);

        var processed = await _db.ProcessedEvents.AnyAsync(e => e.MessageId == messageId);
        if (processed)
        {
            _logger.LogWarning("⚠️ [SHIPPING] [{CorrelationId}] Event zaten işlenmiş: {MessageId}",
                correlationId, messageId);
            return;
        }

        await _mediator.Send(new PrepareShipmentCommand(
            context.Message.OrderId,
            correlationId,
            context.Message.PaymentId,
            context.Message.ProductId,
            context.Message.Quantity,
            context.Message.Amount
        ));

        _db.ProcessedEvents.Add(new ProcessedEvent
        {
            Id = Guid.NewGuid(),
            MessageId = messageId,
            EventType = nameof(PaymentCompletedEvent)
        });
        await _db.SaveChangesAsync();
    }
}