using common.Events;
using common.Interfaces;
using common.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;
using PaymentService.Application.Command;
using PaymentService.Domain;
using PaymentService.Infrastructure;
using System.Text.Json;

namespace PaymentService.Application.Handlers
{
    public class ProcessPaymentHandler : IRequestHandler<ProcessPaymentCommand, bool>
    {
        private readonly PaymentDbContext _db;
        private readonly IUnitOfWork _unitOfWork;
        private readonly ILogger<ProcessPaymentHandler> _logger;

        public ProcessPaymentHandler(PaymentDbContext db, IUnitOfWork unitOfWork, ILogger<ProcessPaymentHandler> logger)
        {
            _db = db;
            _unitOfWork = unitOfWork;
            _logger = logger;
        }

        public async Task<bool> Handle(ProcessPaymentCommand request, CancellationToken cancellationToken)
        {
            _logger.LogInformation(
                "💳 [PAYMENT] [{CorrelationId}] Ödeme başladı: OrderId={OrderId}, Amount={Amount}",
                request.CorrelationId, request.OrderId, request.Amount);

            // Idempotency: bu Order için daha önce payment oluşturulmuş mu?
            var existing = await _db.Payments
                .FirstOrDefaultAsync(p => p.OrderId == request.OrderId, cancellationToken);

            if (existing != null)
            {
                _logger.LogWarning(
                    "⚠️ [PAYMENT] [{CorrelationId}] Zaten işlenmiş: OrderId={OrderId}, Status={Status}",
                    request.CorrelationId, request.OrderId, existing.Status);

                // Eğer daha önce başarılı olduysa, event'i tekrar outbox'a yaz (retry senaryosu)
                if (existing.Status == "Completed")
                {
                    var retryEvent = new PaymentCompletedEvent(
                        request.OrderId,
                        existing.Id,
                        request.ProductId,
                        request.Quantity,
                        request.Amount
                    )
                    {
                        CorrelationId = request.CorrelationId
                    };

                    var outbox = new OutboxMessage
                    {
                        Id = Guid.NewGuid(),
                        EventType = nameof(PaymentCompletedEvent),
                        Payload = JsonSerializer.Serialize(retryEvent),
                        CorrelationId = request.CorrelationId
                    };

                    _db.OutboxMessages.Add(outbox);
                    await _db.SaveChangesAsync(cancellationToken);
                }

                return existing.Status == "Completed";
            }

            //eğer işlenmemişse payment oluşturalım
            await _unitOfWork.BeginTransactionAsync(cancellationToken);

            try
            {
                var failureReason = GetFailureReason(request);

                // Payment kaydı oluşturuyoruz
                var payment = new Payment
                {
                    Id = Guid.NewGuid(),
                    OrderId = request.OrderId,
                    Amount = request.Amount,
                    Status = failureReason is null ? "Pending" : "Failed",
                    FailureReason = failureReason
                };

                _db.Payments.Add(payment);
                await _unitOfWork.SaveAsync(cancellationToken);

                if (failureReason is null)
                {
                    // 1 saniye cevap süresi bekleyelim (demo amaçlı)
                    await Task.Delay(1000, cancellationToken);

                    // Başarılı olarak güncelleyelim
                    payment.Status = "Completed";
                    payment.CompletedAt = DateTime.UtcNow;

                    // Bu payment bilgilerini kullanarak PaymentCompletedEvent üretelim (Shipping Service alacak)
                    var successEvent = new PaymentCompletedEvent(
                        request.OrderId,
                        payment.Id,
                        request.ProductId,
                        request.Quantity,
                        request.Amount
                    )
                    {
                        CorrelationId = request.CorrelationId
                    };

                    var outbox = new OutboxMessage
                    {
                        Id = Guid.NewGuid(),
                        EventType = nameof(PaymentCompletedEvent),
                        Payload = JsonSerializer.Serialize(successEvent),
                        CorrelationId = request.CorrelationId
                    };

                    _db.OutboxMessages.Add(outbox);
                    await _unitOfWork.CommitAsync(cancellationToken);

                    _logger.LogInformation(
                        "✅ [PAYMENT] [{CorrelationId}] Ödeme başarılı: OrderId={OrderId}",
                        request.CorrelationId, request.OrderId);

                    return true;
                }
                else
                {
                    // Başarısız olursa
                    // Başarısızlık event'i üretelim (Stock Service dinleyip stoğu geri yükleyecek)
                    var failEvent = new PaymentFailedEvent(
                        request.OrderId,
                        request.ProductId,
                        request.Quantity,
                        failureReason
                    )
                    {
                        CorrelationId = request.CorrelationId
                    };

                    var outbox = new OutboxMessage
                    {
                        Id = Guid.NewGuid(),
                        EventType = nameof(PaymentFailedEvent),
                        Payload = JsonSerializer.Serialize(failEvent),
                        CorrelationId = request.CorrelationId
                    };

                    _db.OutboxMessages.Add(outbox);
                    await _unitOfWork.CommitAsync(cancellationToken);

                    _logger.LogWarning(
                        "❌ [PAYMENT] [CorrelationId={CorrelationId}] [OrderId={OrderId}] Action=PaymentFailed Reason={Reason}",
                        request.CorrelationId, request.OrderId, failureReason);

                    return false;
                }
            }
            catch (Exception ex)
            {
                // hata durumunda rollback yapıyoruz
                _logger.LogError(
                    ex,
                    "❌ [PAYMENT] [{CorrelationId}] Hata: OrderId={OrderId}",
                    request.CorrelationId, request.OrderId);

                await _unitOfWork.RollbackAsync(cancellationToken);
                throw;
            }
        }

        private static string? GetFailureReason(ProcessPaymentCommand request)
        {
            if (request.OrderId == Guid.Empty)
            {
                return "Geçersiz sipariş";
            }

            if (request.ProductId == Guid.Empty)
            {
                return "Geçersiz ürün";
            }

            if (request.Quantity <= 0)
            {
                return "Geçersiz ürün adedi";
            }

            if (request.Amount <= 0)
            {
                return "Geçersiz ödeme tutarı";
            }

            return null;
        }
    }
}
