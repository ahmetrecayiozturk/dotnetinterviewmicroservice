using common.Events;
using common.Interfaces;
using common.Models;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using PaymentService.Infrastructure;
using System.Text.Json;

public class PaymentRefundConsumer : IConsumer<ShippingFailedEvent>
{
    private readonly PaymentDbContext _db;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<PaymentRefundConsumer> _logger;

    public PaymentRefundConsumer(PaymentDbContext db, IUnitOfWork unitOfWork, ILogger<PaymentRefundConsumer> logger)
    {
        _db = db;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<ShippingFailedEvent> context)
    {
        var correlationId = context.Message.CorrelationId;
        var orderId = context.Message.OrderId;

        _logger.LogInformation("🔄 [PAYMENT-REFUND] [{CorrelationId}] İade başladı: OrderId={OrderId}",
            correlationId, orderId);

        await _unitOfWork.BeginTransactionAsync();

        try
        {
            var payment = await _db.Payments
                .FirstOrDefaultAsync(p => p.OrderId == orderId && p.Status == "Completed");

            if (payment != null)
            {
                payment.Status = "Refunded";
                payment.RefundedAt = DateTime.UtcNow;

                var refundEvent = new PaymentRefundedEvent(orderId, payment.Id, payment.Amount)
                {
                    CorrelationId = correlationId
                };

                var outbox = new OutboxMessage
                {
                    Id = Guid.NewGuid(),
                    EventType = nameof(PaymentRefundedEvent),
                    Payload = JsonSerializer.Serialize(refundEvent),
                    CorrelationId = correlationId
                };
                _db.OutboxMessages.Add(outbox);

                await _unitOfWork.CommitAsync();

                _logger.LogInformation("✅ [PAYMENT-REFUND] [{CorrelationId}] İade tamamlandı: OrderId={OrderId}, Amount={Amount}",
                    correlationId, orderId, payment.Amount);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ [PAYMENT-REFUND] [{CorrelationId}] Hata: OrderId={OrderId}",
                correlationId, orderId);
            await _unitOfWork.RollbackAsync();
            throw;
        }
    }
}



/*using common.Events;
using common.Interfaces;
using common.Models;
using MassTransit;
using MediatR;
using Microsoft.EntityFrameworkCore;
using PaymentService.Infrastructure;
using System.Text.Json;

namespace PaymentService.Consumers
{
    public class PaymentRefundConsumer:IConsumer<ShippingFailedEvent>
    {
        private IUnitOfWork _unitOfWork;
        private PaymentDbContext _paymentDbContext;
        private ILogger _logger;

        public PaymentRefundConsumer(IUnitOfWork unitOfWork, PaymentDbContext paymentDbContext, ILogger logger)
        {
            _unitOfWork = unitOfWork;
            _paymentDbContext = paymentDbContext;
            _logger = logger;
        }

        public async Task Consume(ConsumeContext<ShippingFailedEvent> context)
        {
            //bu correlationId her yerde her eventte aynı olduğundan loglamada iş görecek
            var correlationId = context.Message.CorrelationId;
            var orderId = context.Message.OrderId;

            //önce transactional başlangıcı yapıcaz
            await _unitOfWork.BeginTransactionAsync();

            try 
            {
                //paymenti buluyoruz
                var payment = await _paymentDbContext.Payments.FirstOrDefaultAsync(x=>x.OrderId == orderId && x.Status =="Completed");
                if (payment != null) 
                {
                    //iade edildi diyelim ve güncelleyelim
                    payment.Status = "Refunded";
                    payment.RefundedAt = DateTime.UtcNow;


                    //refund eventi oluşturalım
                    var refundedEvent = new PaymentRefundedEvent(orderId,payment.Id,payment.Amount)
                    {
                        CorrelationId = correlationId
                    };

                    //outboxmessageyi oluşturalım
                    var outboxMessage = new OutboxMessage
                    {
                        Id = Guid.NewGuid(),
                        EventType = nameof(ShippingFailedEvent),
                        Payload = JsonSerializer.Serialize(refundedEvent),
                        CorrelationId = correlationId
                    };
                    //outboxu kaydedelim
                    _paymentDbContext.Add(outboxMessage);
                    
                    //bitti rolback bununla begisn transaction arasında olacak
                    await _unitOfWork.CommitAsync();

                    _logger.LogInformation("[PAYMENT-REFUND] [{CorrelationId}] İade tamamlandı: OrderId={OrderId}, Amount={Amount}",
    correlationId, orderId, payment.Amount);
                }


            }
            catch (Exception ex) 
            {
                _logger.LogError(ex, "[PAYMENT-REFUND] [{CorrelationId}] Hata: OrderId={OrderId}",
    correlationId, orderId);
                //hata alırsak rollback yapıyoruz
                await _unitOfWork.RollbackAsync();
                throw;
            }
        }
    }
}
*/