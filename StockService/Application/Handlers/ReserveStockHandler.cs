using common.Events;
using common.Interfaces;
using common.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;
using StockService.Application.Commands;
using StockService.Domain;
using StockService.Infrastructure;
using System.Text.Json;

namespace StockService.Application.Commands
{
    public class ReserveStockHandler : IRequestHandler<ReserveStockCommand, bool>
    {
        //db'ye erişim
        private readonly StockDbContext _db;
        //transactional mantık
        private readonly IUnitOfWork _unitOfWork;
        //logalama
        private readonly ILogger<ReserveStockHandler> _logger;

        public ReserveStockHandler(StockDbContext db, IUnitOfWork unitOfWork, ILogger<ReserveStockHandler> logger)
        {
            _db = db;
            _unitOfWork = unitOfWork;
            _logger = logger;
        }

        public async Task<bool> Handle(ReserveStockCommand request, CancellationToken cancellationToken)
        {
            _logger.LogInformation("📦 [STOCK] [{CorrelationId}] Stok kontrolü başladı: OrderId={OrderId}",
                request.CorrelationId, request.OrderId);

            await _unitOfWork.BeginTransactionAsync(cancellationToken);

            //önce güncel durumu iş akışı olan saga stateye kaydedelim
            try
            {
                var sagaState = new OrderSagaState
                {
                    OrderId = request.OrderId,
                    CorrelationId = request.CorrelationId,
                    ProductId = request.ProductId,
                    Quantity = request.Quantity,
                    Amount = 0,
                    CurrentStep = "Started"
                };
                _db.OrderSagaStates.Add(sagaState);

                //stok var mı kontrol edelim
                var product = await _db.Products
                    .FirstOrDefaultAsync(p => p.Id == request.ProductId, cancellationToken);

                if (product == null || !product.HasStock(request.Quantity))
                {
                    sagaState.CurrentStep = "Failed";
                    await _unitOfWork.SaveAsync(cancellationToken);

                    //Şimdi outbox'a atıp oradan sürekli yayınlayacağımız eventi oluşturuyoruz
                    var failEvent = new StockInsufficientEvent(request.OrderId, "Stok yetersiz")
                    {
                        //base eventteki constructoru yapıyoruz
                        CorrelationId = request.CorrelationId
                    };

                    var outbox = new OutboxMessage
                    {
                        Id = Guid.NewGuid(),
                        EventType = nameof(StockInsufficientEvent),
                        Payload = JsonSerializer.Serialize(failEvent),
                        CorrelationId = request.CorrelationId
                    };
                    _db.OutboxMessages.Add(outbox);

                    //db ye yolluyoruz
                    await _unitOfWork.CommitAsync(cancellationToken);

                    _logger.LogWarning("❌ [STOCK] [{CorrelationId}] Stok yetersiz: OrderId={OrderId}",
                        request.CorrelationId, request.OrderId);

                    // dışarıya sadece başarısız (false) dönüyoruz
                    return false;
                }

                //eğer stok varsa rezerve edelim
                product.Reserve(request.Quantity);
                //toplam fiyatı bulalım
                var amount = product.Price * request.Quantity;
                sagaState.Amount = amount;
                sagaState.CurrentStep = "StockReserved";

                //kaydedelim
                await _unitOfWork.SaveAsync(cancellationToken);

                //Başarılı eventi oluşturalım
                var successEvent = new StockReservedEvent(
                    request.OrderId,
                    request.ProductId,
                    request.Quantity,
                    sagaState.Amount
                )
                {
                    CorrelationId = request.CorrelationId
                };

                var outboxSuccess = new OutboxMessage
                {
                    Id = Guid.NewGuid(),
                    EventType = nameof(StockReservedEvent),
                    Payload = JsonSerializer.Serialize(successEvent),
                    CorrelationId = request.CorrelationId
                };
                _db.OutboxMessages.Add(outboxSuccess);

                //db ye yollayalım
                await _unitOfWork.CommitAsync(cancellationToken);

                _logger.LogInformation("✅ [STOCK] [{CorrelationId}] Stok rezerve edildi: OrderId={OrderId}, Kalan={Quantity}",
                    request.CorrelationId, request.OrderId, product.Quantity);

                // dışarıya sadece başarılı (true) bilgisi dönüyoruz
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ [STOCK] [{CorrelationId}] Hata: OrderId={OrderId}",
                    request.CorrelationId, request.OrderId);
                await _unitOfWork.RollbackAsync(cancellationToken);
                throw;
            }
        }
    }
}