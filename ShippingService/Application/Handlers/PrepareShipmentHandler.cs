using common.Events;
using common.Interfaces;
using common.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;
using ShippingService.Application.Commands;
using ShippingService.Domain;
using ShippingService.Infrastructure;
using System.Text.Json;

namespace ShippingService.Application.Handlers;

public class PrepareShipmentHandler : IRequestHandler<PrepareShipmentCommand, bool>
{
    private readonly ShippingDbContext _dbContext;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<PrepareShipmentHandler> _logger;

    public PrepareShipmentHandler(
        ShippingDbContext dbContext,
        IUnitOfWork unitOfWork,
        ILogger<PrepareShipmentHandler> logger)
    {
        _dbContext = dbContext;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<bool> Handle(PrepareShipmentCommand request, CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "📦 [SHIPPING] [{CorrelationId}] Kargo hazırlanıyor: OrderId={OrderId}",
            request.CorrelationId, request.OrderId);

        // 1) Idempotency: Bu sipariş için daha önce shipment var mı?
        var existingShipment = await _dbContext.Shipments
            .FirstOrDefaultAsync(s => s.OrderId == request.OrderId, cancellationToken);

        if (existingShipment != null)
        {
            _logger.LogWarning(
                "⚠️ [SHIPPING] [{CorrelationId}] Zaten işlenmiş: OrderId={OrderId}, Status={Status}",
                request.CorrelationId, request.OrderId, existingShipment.Status);

            if (existingShipment.Status == "Shipped")
            {
                // Başarılı shipment için event'i tekrar outbox'a yaz (retry senaryosu)
                var retryEvent = new ShippingPreparedEvent(
                    existingShipment.OrderId,
                    existingShipment.TrackingNumber)
                {
                    CorrelationId = request.CorrelationId
                };

                var outboxMessage = new OutboxMessage
                {
                    Id = Guid.NewGuid(),
                    EventType = nameof(ShippingPreparedEvent),
                    Payload = JsonSerializer.Serialize(retryEvent),
                    CorrelationId = request.CorrelationId
                };

                _dbContext.OutboxMessages.Add(outboxMessage);
                await _dbContext.SaveChangesAsync(cancellationToken);
            }

            // Sadece başarı bilgisini döndürüyoruz
            return existingShipment.Status == "Shipped";
        }

        // 2) Yeni shipment için transaction başlat
        await _unitOfWork.BeginTransactionAsync(cancellationToken);

        try
        {
            var failureReason = GetFailureReason(request);

            // Yeni shipment kaydı oluştur
            var shipment = new Shipment
            {
                Id = Guid.NewGuid(),
                OrderId = request.OrderId,
                TrackingNumber = failureReason is null
                    ? $"TRK-{Guid.NewGuid().ToString()[..8].ToUpper()}"
                    : string.Empty,
                Status = failureReason is null ? "Preparing" : "Failed"
            };

            _dbContext.Shipments.Add(shipment);
            await _unitOfWork.SaveAsync(cancellationToken);

            if (failureReason is null)
            {
                // Kargo API simülasyonu (dış sistem çağrısı gibi düşün)
                await Task.Delay(1500, cancellationToken);

                shipment.Status = "Shipped";
                shipment.ShippedAt = DateTime.UtcNow;

                var shippingPreparedEvent = new ShippingPreparedEvent(
                    request.OrderId,
                    shipment.TrackingNumber)
                {
                    CorrelationId = request.CorrelationId
                };

                var outboxMessage = new OutboxMessage
                {
                    Id = Guid.NewGuid(),
                    EventType = nameof(ShippingPreparedEvent),
                    Payload = JsonSerializer.Serialize(shippingPreparedEvent),
                    CorrelationId = request.CorrelationId
                };

                _dbContext.OutboxMessages.Add(outboxMessage);

                await _unitOfWork.CommitAsync(cancellationToken);

                _logger.LogInformation(
                    "✅ [SHIPPING] [{CorrelationId}] Kargo hazır: OrderId={OrderId}, Tracking={TrackingNumber}",
                    request.CorrelationId, request.OrderId, shipment.TrackingNumber);

                return true;
            }
            else
            {
                shipment.Status = "Failed";

                var shippingFailedEvent = new ShippingFailedEvent(
                    request.OrderId,
                    request.PaymentId,
                    request.ProductId,
                    request.Quantity,
                    request.Amount,
                    failureReason)
                {
                    CorrelationId = request.CorrelationId
                };

                var outboxMessage = new OutboxMessage
                {
                    Id = Guid.NewGuid(),
                    EventType = nameof(ShippingFailedEvent),
                    Payload = JsonSerializer.Serialize(shippingFailedEvent),
                    CorrelationId = request.CorrelationId
                };

                _dbContext.OutboxMessages.Add(outboxMessage);

                await _unitOfWork.CommitAsync(cancellationToken);

                _logger.LogWarning(
                    "❌ [SHIPPING] [CorrelationId={CorrelationId}] [OrderId={OrderId}] Action=ShippingFailed Reason={Reason}",
                    request.CorrelationId, request.OrderId, failureReason);

                return false;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "❌ [SHIPPING] [{CorrelationId}] Hata: OrderId={OrderId}",
                request.CorrelationId, request.OrderId);

            await _unitOfWork.RollbackAsync(cancellationToken);
            throw;
        }
    }

    private static string? GetFailureReason(PrepareShipmentCommand request)
    {
        if (request.PaymentId == Guid.Empty)
        {
            return "Geçersiz ödeme bilgisi";
        }

        if (request.Quantity <= 0)
        {
            return "Geçersiz ürün adedi";
        }

        if (request.ProductId == Guid.Empty)
        {
            return "Geçersiz ürün";
        }

        if (request.OrderId == Guid.Empty)
        {
            return "Geçersiz sipariş";
        }

        return null;
    }
}

/*
 using ECommerce.Common.Abstractions;
using ECommerce.Common.Events;
using ECommerce.Common.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;
using ShippingService.Domain;
using ShippingService.Infrastructure;
using System.Text.Json;

namespace ShippingService.Application.Commands;

public class PrepareShipmentHandler : IRequestHandler<PrepareShipmentCommand, (bool Success, string? TrackingNumber)>
{
    private readonly ShippingDbContext _db;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<PrepareShipmentHandler> _logger;

    public PrepareShipmentHandler(ShippingDbContext db, IUnitOfWork unitOfWork, ILogger<PrepareShipmentHandler> logger)
    {
        _db = db;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<(bool Success, string? TrackingNumber)> Handle(PrepareShipmentCommand request, CancellationToken cancellationToken)
    {
        _logger.LogInformation("📦 [SHIPPING] [{CorrelationId}] Kargo hazırlanıyor: OrderId={OrderId}",
            request.CorrelationId, request.OrderId);

        // Idempotency
        var existing = await _db.Shipments.FirstOrDefaultAsync(s => s.OrderId == request.OrderId, cancellationToken);
        if (existing != null)
        {
            _logger.LogWarning("⚠️ [SHIPPING] [{CorrelationId}] Zaten işlenmiş: OrderId={OrderId}, Status={Status}",
                request.CorrelationId, request.OrderId, existing.Status);

            if (existing.Status == "Shipped")
            {
                var retryEvent = new ShippingPreparedEvent(request.OrderId, existing.TrackingNumber)
                {
                    CorrelationId = request.CorrelationId
                };

                var outbox = new OutboxMessage
                {
                    Id = Guid.NewGuid(),
                    EventType = nameof(ShippingPreparedEvent),
                    Payload = JsonSerializer.Serialize(retryEvent),
                    CorrelationId = request.CorrelationId
                };
                _db.OutboxMessages.Add(outbox);
                await _db.SaveAsync(cancellationToken);
            }

            return (existing.Status == "Shipped", existing.Status == "Shipped" ? existing.TrackingNumber : null);
        }

        await _unitOfWork.BeginTransactionAsync(cancellationToken);

        try
        {
            var trackingNumber = $"TRK-{Guid.NewGuid().ToString()[..8].ToUpper()}";

            var shipment = new Shipment
            {
                Id = Guid.NewGuid(),
                OrderId = request.OrderId,
                TrackingNumber = trackingNumber,
                Status = "Preparing"
            };
            _db.Shipments.Add(shipment);
            await _unitOfWork.SaveAsync(cancellationToken);

            // Kargo API simülasyonu
            await Task.Delay(1500, cancellationToken);
            var success = Random.Shared.Next(100) < 90;

            if (success)
            {
                shipment.Status = "Shipped";
                shipment.ShippedAt = DateTime.UtcNow;

                var successEvent = new ShippingPreparedEvent(request.OrderId, trackingNumber)
                {
                    CorrelationId = request.CorrelationId
                };

                var outbox = new OutboxMessage
                {
                    Id = Guid.NewGuid(),
                    EventType = nameof(ShippingPreparedEvent),
                    Payload = JsonSerializer.Serialize(successEvent),
                    CorrelationId = request.CorrelationId
                };
                _db.OutboxMessages.Add(outbox);

                await _unitOfWork.CommitAsync(cancellationToken);

                _logger.LogInformation("✅ [SHIPPING] [{CorrelationId}] Kargo hazır: OrderId={OrderId}, Tracking={TrackingNumber}",
                    request.CorrelationId, request.OrderId, trackingNumber);

                return (true, trackingNumber);
            }
            else
            {
                var reason = "Kargo firması yanıt vermiyor";
                shipment.Status = "Failed";

                var failEvent = new ShippingFailedEvent(
                    request.OrderId,
                    request.PaymentId,
                    request.ProductId,
                    request.Quantity,
                    request.Amount,
                    reason
                )
                {
                    CorrelationId = request.CorrelationId
                };

                var outbox = new OutboxMessage
                {
                    Id = Guid.NewGuid(),
                    EventType = nameof(ShippingFailedEvent),
                    Payload = JsonSerializer.Serialize(failEvent),
                    CorrelationId = request.CorrelationId
                };
                _db.OutboxMessages.Add(outbox);

                await _unitOfWork.CommitAsync(cancellationToken);

                _logger.LogWarning("❌ [SHIPPING] [{CorrelationId}] Kargo başarısız: OrderId={OrderId}, Reason={Reason}",
                    request.CorrelationId, request.OrderId, reason);

                return (false, null);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ [SHIPPING] [{CorrelationId}] Hata: OrderId={OrderId}",
                request.CorrelationId, request.OrderId);
            await _unitOfWork.RollbackAsync(cancellationToken);
            throw;
        }
    }
}
 */
