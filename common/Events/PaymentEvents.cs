using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace common.Events;

// 3. Ödeme alındı (PaymentService → ShippingService)
public record PaymentCompletedEvent(
    Guid OrderId,
    Guid PaymentId,
    Guid ProductId,    // Rollback için
    int Quantity,      // Rollback için
    decimal Amount
) : BaseEvent;

// 3a. Ödeme başarısız (PaymentService → StockService + API Gateway)
public record PaymentFailedEvent(
    Guid OrderId,
    Guid ProductId,    // StockService için
    int Quantity,      // StockService için
    string Reason
) : BaseEvent;

// Rollback: Parayı iade et (ShippingService → PaymentService)
public record PaymentRefundRequestedEvent(
    Guid OrderId,
    Guid PaymentId,
    string Reason
) : BaseEvent;

public record PaymentRefundedEvent(
    Guid OrderId,
    Guid PaymentId,
    decimal Amount
) : BaseEvent;
