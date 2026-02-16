using MediatR;

namespace PaymentService.Application.Command;

// Ödeme işlemini başlatmak için kullanılan komut
public class ProcessPaymentCommand : IRequest<bool>
{
    public Guid OrderId { get; set; }
    public Guid CorrelationId { get; set; }
    public Guid ProductId { get; set; }
    public int Quantity { get; set; }
    public decimal Amount { get; set; }

    public ProcessPaymentCommand()
    {
    }

    public ProcessPaymentCommand(Guid orderId, Guid correlationId, Guid productId, int quantity, decimal amount)
    {
        OrderId = orderId;
        CorrelationId = correlationId;
        ProductId = productId;
        Quantity = quantity;
        Amount = amount;
    }
}