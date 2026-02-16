using MediatR;

namespace StockService.Application.Commands;
    public record ReserveStockCommand : IRequest<bool>
    {
    public Guid OrderId { get; set; }
    public Guid CorrelationId { get; set; }
    public Guid ProductId { get; set; }
    public int Quantity { get; set; }
    }

/*
using MediatR;

namespace StockService.Application.Commands;

public record ReserveStockCommand(
    Guid OrderId,
    Guid CorrelationId,
    Guid ProductId,
    int Quantity
) : IRequest<(bool Success, string? ErrorMessage, decimal Amount)>;
 */