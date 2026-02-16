namespace common.Events;

public record OrderStartedEvent(
    Guid OrderId,
    Guid ProductId,
    int Quantity
) : BaseEvent;

// 2. Stok rezerve edildi (StockService → PaymentService)
public record StockReservedEvent(
    Guid OrderId,
    Guid ProductId,
    int Quantity,
    decimal Amount
) : BaseEvent;

// 2a. Stok yetersiz (StockService → API Gateway)
public record StockInsufficientEvent(
    Guid OrderId,
    string Reason
) : BaseEvent;

// Rollback: Stoğu geri yükle (PaymentService/ShippingService → StockService)
public record StockReleaseRequestedEvent(
    Guid OrderId,
    Guid ProductId,
    int Quantity,
    string Reason
) : BaseEvent;

public record StockReleasedEvent(
    Guid OrderId,
    Guid ProductId,
    int Quantity
) : BaseEvent;