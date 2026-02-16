using common.Models;
using Microsoft.EntityFrameworkCore;
using ShippingService.Domain;

namespace ShippingService.Infrastructure;

public class ShippingDbContext : DbContext
{
    public ShippingDbContext(DbContextOptions<ShippingDbContext> options) : base(options) { }

    public DbSet<Shipment> Shipments => Set<Shipment>();
    public DbSet<ProcessedEvent> ProcessedEvents => Set<ProcessedEvent>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ProcessedEvent>()
            .HasIndex(e => e.MessageId)
            .IsUnique();

        modelBuilder.Entity<Shipment>()
            .HasIndex(e => e.OrderId)
            .IsUnique();
    }
}