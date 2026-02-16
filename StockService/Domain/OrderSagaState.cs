namespace StockService.Domain;

public class OrderSagaState
{
    public Guid OrderId { get; set; }
    public Guid CorrelationId { get; set; }
    public Guid ProductId { get; set; }
    public int Quantity { get; set; }
    public decimal Amount { get; set; }
    public string CurrentStep { get; set; } = "Started"; // Started, StockReserved, Rollback, Failed
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }
}