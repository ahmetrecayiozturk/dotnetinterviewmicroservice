using MediatR;

namespace ShippingService.Application.Commands;

// Kargonun hazırlanması için kullanılan komut
public class PrepareShipmentCommand : IRequest<bool>
{
    public Guid OrderId { get; set; }
    public Guid CorrelationId { get; set; }
    public Guid PaymentId { get; set; }
    public Guid ProductId { get; set; }
    public int Quantity { get; set; }
    public decimal Amount { get; set; }

    public PrepareShipmentCommand(
    Guid orderId,
    Guid correlationId,
    Guid paymentId,
    Guid productId,
    int quantity,
    decimal amount)
    {
        OrderId = orderId;
        CorrelationId = correlationId;
        PaymentId = paymentId;
        ProductId = productId;
        Quantity = quantity;
        Amount = amount;
    }
}