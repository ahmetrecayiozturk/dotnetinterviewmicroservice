namespace PaymentService.Domain;
public class ProcessedEvent
{
    public Guid Id { get; set; }
    public Guid MessageId { get; set; }
    public string EventType { get; set; } = string.Empty;
    public DateTime ProcessedAt { get; set; } = DateTime.UtcNow;
}