using APIGateway.Data;
using common.Events;
using MassTransit;
using Microsoft.EntityFrameworkCore;

namespace ApiGateway.Consumers;

public class OrderStatusConsumers : IConsumer<ShippingPreparedEvent>
{
    private readonly GatewayDbContext _db;
    private readonly ILogger<OrderStatusConsumers> _logger;

    public OrderStatusConsumers(GatewayDbContext db, ILogger<OrderStatusConsumers> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<ShippingPreparedEvent> context)
    {
        var correlationId = context.Message.CorrelationId;
        var orderId = context.Message.OrderId;

        _logger.LogInformation("🌐 [GATEWAY] [{CorrelationId}] ✅ Sipariş tamamlandı: {OrderId}, Tracking: {TrackingNumber}",
            correlationId, orderId, context.Message.TrackingNumber);

        var orderStatus = await _db.OrderStatuses
            .FirstOrDefaultAsync(o => o.CorrelationId == correlationId);

        if (orderStatus != null)
        {
            orderStatus.Status = "Completed";
            orderStatus.TrackingNumber = context.Message.TrackingNumber;
            orderStatus.CompletedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();
        }
    }

}

public class StockInsufficientConsumer : IConsumer<StockInsufficientEvent>
{
    private readonly GatewayDbContext _db;
    private readonly ILogger<StockInsufficientConsumer> _logger;

    public StockInsufficientConsumer(GatewayDbContext db, ILogger<StockInsufficientConsumer> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<StockInsufficientEvent> context)
    {
        var correlationId = context.Message.CorrelationId;
        var orderId = context.Message.OrderId;

        _logger.LogWarning("🌐 [GATEWAY] [{CorrelationId}] ❌ Sipariş başarısız (Stok): {OrderId}, Reason: {Reason}",
            correlationId, orderId, context.Message.Reason);

        var orderStatus = await _db.OrderStatuses
            .FirstOrDefaultAsync(o => o.CorrelationId == correlationId);

        if (orderStatus != null)
        {
            orderStatus.Status = "Failed";
            orderStatus.FailureReason = $"Stok: {context.Message.Reason}";
            orderStatus.CompletedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();
        }
    }
}

public class PaymentFailedConsumer : IConsumer<PaymentFailedEvent>
{
    private readonly GatewayDbContext _db;
    private readonly ILogger<PaymentFailedConsumer> _logger;

    public PaymentFailedConsumer(GatewayDbContext db, ILogger<PaymentFailedConsumer> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<PaymentFailedEvent> context)
    {
        var correlationId = context.Message.CorrelationId;
        var orderId = context.Message.OrderId;

        _logger.LogWarning("🌐 [GATEWAY] [{CorrelationId}] ❌ Sipariş başarısız (Ödeme): {OrderId}, Reason: {Reason}",
            correlationId, orderId, context.Message.Reason);

        var orderStatus = await _db.OrderStatuses
            .FirstOrDefaultAsync(o => o.CorrelationId == correlationId);

        if (orderStatus != null)
        {
            orderStatus.Status = "Failed";
            orderStatus.FailureReason = $"Ödeme: {context.Message.Reason}";
            orderStatus.CompletedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();
        }
    }
}

public class ShippingFailedConsumer : IConsumer<ShippingFailedEvent>
{
    private readonly GatewayDbContext _db;
    private readonly ILogger<ShippingFailedConsumer> _logger;

    public ShippingFailedConsumer(GatewayDbContext db, ILogger<ShippingFailedConsumer> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<ShippingFailedEvent> context)
    {
        var correlationId = context.Message.CorrelationId;
        var orderId = context.Message.OrderId;

        _logger.LogWarning("🌐 [GATEWAY] [{CorrelationId}] ❌ Sipariş başarısız (Kargo): {OrderId}, Reason: {Reason}",
            correlationId, orderId, context.Message.Reason);

        var orderStatus = await _db.OrderStatuses
            .FirstOrDefaultAsync(o => o.CorrelationId == correlationId);

        if (orderStatus != null)
        {
            orderStatus.Status = "Failed";
            orderStatus.FailureReason = $"Kargo: {context.Message.Reason}";
            orderStatus.CompletedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();
        }
    }
}