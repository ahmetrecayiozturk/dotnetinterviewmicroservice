namespace APIGateway.Models;

public class OrderStatus
{
    public Guid Id { get; set; }
    public Guid OrderId { get; set; }
    public Guid CorrelationId { get; set; }
    public string Status { get; set; } = "Processing"; // Processing, Completed, Failed
    public string? TrackingNumber { get; set; }
    public string? FailureReason { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }
}