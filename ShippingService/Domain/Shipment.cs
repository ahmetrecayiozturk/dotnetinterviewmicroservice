namespace ShippingService.Domain;

public class Shipment
{
    public Guid Id { get; set; }
    public Guid OrderId { get; set; }
    public string TrackingNumber { get; set; } = string.Empty;
    public string Status { get; set; } = "Preparing"; // Preparing, Shipped, Failed
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ShippedAt { get; set; }
}