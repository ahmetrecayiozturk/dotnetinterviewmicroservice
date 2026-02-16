using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace common.Events;
// 4. Kargo hazırlandı (ShippingService → API Gateway)
public record ShippingPreparedEvent(
    Guid OrderId,
    string TrackingNumber
) : BaseEvent;

// 4a. Kargo başarısız (ShippingService → PaymentService + StockService + API Gateway)
public record ShippingFailedEvent(
    Guid OrderId,
    Guid PaymentId,    // PaymentService için
    Guid ProductId,    // StockService için
    int Quantity,      // StockService için
    decimal Amount,    // PaymentService için
    string Reason
) : BaseEvent;