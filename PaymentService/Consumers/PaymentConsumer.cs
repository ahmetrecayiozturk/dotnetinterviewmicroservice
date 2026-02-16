using common.Events;
using MassTransit;
using MediatR;
using Microsoft.EntityFrameworkCore;
using PaymentService.Application.Command;
using PaymentService.Domain;
using PaymentService.Infrastructure;

namespace PaymentService.Consumers;

public class PaymentConsumer : IConsumer<StockReservedEvent>
{
    private readonly PaymentDbContext _db;
    private readonly IMediator _mediator;
    private readonly ILogger<PaymentConsumer> _logger;

    public PaymentConsumer(PaymentDbContext db, IMediator mediator, ILogger<PaymentConsumer> logger)
    {
        _db = db;
        _mediator = mediator;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<StockReservedEvent> context)
    {
        var messageId = context.MessageId ?? Guid.NewGuid();
        var correlationId = context.Message.CorrelationId;

        _logger.LogInformation("💳 [PAYMENT] [{CorrelationId}] StockReservedEvent alındı: {OrderId}",
            correlationId, context.Message.OrderId);
        //mesaj işlenmiş mi idempotnect için kontrol
        var processed = await _db.ProcessedEvents
            .AnyAsync(e => e.MessageId == messageId);

        if (processed)
        {
            _logger.LogWarning("⚠️ [PAYMENT] [{CorrelationId}] Event zaten işlenmiş: {MessageId}",
                correlationId, messageId);
            return;
        }

        await _mediator.Send(new ProcessPaymentCommand(
            context.Message.OrderId,
            correlationId,
            context.Message.ProductId,
            context.Message.Quantity,
            context.Message.Amount
        ));

        _db.ProcessedEvents.Add(new ProcessedEvent
        {
            Id = Guid.NewGuid(),
            MessageId = messageId,
            EventType = nameof(StockReservedEvent)
        });
        await _db.SaveChangesAsync();
    }
}