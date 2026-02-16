using common.Events;
using common.Interfaces;
using common.Models;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using StockService.Domain;
using StockService.Infrastructure;
using System.Text.Json;

namespace StockService.Consumers;

public class StockReleaseConsumer :
    IConsumer<PaymentFailedEvent>,
    IConsumer<ShippingFailedEvent>
{
    private readonly StockDbContext _db;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<StockReleaseConsumer> _logger;

    public StockReleaseConsumer(StockDbContext db, IUnitOfWork unitOfWork, ILogger<StockReleaseConsumer> logger)
    {
        _db = db;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<PaymentFailedEvent> context)
    {
        await ReleaseStock(
            context.Message.OrderId,
            context.Message.CorrelationId,
            context.Message.ProductId,
            context.Message.Quantity,
            $"Payment failed: {context.Message.Reason}"
        );
    }

    public async Task Consume(ConsumeContext<ShippingFailedEvent> context)
    {
        await ReleaseStock(
            context.Message.OrderId,
            context.Message.CorrelationId,
            context.Message.ProductId,
            context.Message.Quantity,
            $"Shipping failed: {context.Message.Reason}"
        );
    }

    private async Task ReleaseStock(Guid orderId, Guid correlationId, Guid productId, int quantity, string reason)
    {
        _logger.LogWarning(
            "📦 [STOCK] [CorrelationId={CorrelationId}] [OrderId={OrderId}] Action=StockReleaseRequested Reason={Reason}",
            correlationId, orderId, reason);

        _logger.LogInformation("[STOCK-ROLLBACK] [{CorrelationId}] Stok geri yükleniyor: OrderId={OrderId}",
            correlationId, orderId);

        await _unitOfWork.BeginTransactionAsync();

        try
        {
            var sagaState = await _db.OrderSagaStates
                .FirstOrDefaultAsync(s => s.OrderId == orderId);

            if (sagaState != null)
                sagaState.CurrentStep = "Rollback";

            var product = await _db.Products.FindAsync(productId);
            if (product != null)
            {
                product.Release(quantity);
                await _unitOfWork.SaveAsync();

                var releaseEvent = new StockReleasedEvent(orderId, productId, quantity)
                {
                    CorrelationId = correlationId
                };

                var outbox = new OutboxMessage
                {
                    Id = Guid.NewGuid(),
                    EventType = nameof(StockReleasedEvent),
                    Payload = JsonSerializer.Serialize(releaseEvent),
                    CorrelationId = correlationId
                };
                _db.OutboxMessages.Add(outbox);

                await _unitOfWork.CommitAsync();

                _logger.LogInformation(
                    "✅ [STOCK] [CorrelationId={CorrelationId}] [OrderId={OrderId}] Action=StockReleased Product={Product} NewQuantity={Quantity}",
                    correlationId, orderId, product.Name, product.Quantity);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[STOCK-ROLLBACK] [{CorrelationId}] Hata: OrderId={OrderId}",
                correlationId, orderId);
            await _unitOfWork.RollbackAsync();
            throw;
        }
    }
}
