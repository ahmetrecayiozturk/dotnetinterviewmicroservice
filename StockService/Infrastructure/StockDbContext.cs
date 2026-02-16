using common.Models;
using Microsoft.EntityFrameworkCore;
using StockService.Domain;

namespace StockService.Infrastructure;

public class StockDbContext : DbContext
{
    public StockDbContext(DbContextOptions<StockDbContext> options) : base(options) { }

    public DbSet<Product> Products => Set<Product>();
    public DbSet<OrderSagaState> OrderSagaStates => Set<OrderSagaState>();
    public DbSet<ProcessedEvent> ProcessedEvents => Set<ProcessedEvent>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Product seed
        modelBuilder.Entity<Product>().HasData(
            new Product
            {
                Id = Guid.Parse("11111111-1111-1111-1111-111111111111"),
                Name = "Laptop",
                Quantity = 10,
                Price = 15000
            },
            new Product
            {
                Id = Guid.Parse("22222222-2222-2222-2222-222222222222"),
                Name = "Mouse",
                Quantity = 50,
                Price = 500
            }
        );

        // ProcessedEvent için unique MessageId
        modelBuilder.Entity<ProcessedEvent>()
            .HasIndex(e => e.MessageId)
            .IsUnique();

        // 🔑 OrderSagaState primary key
        modelBuilder.Entity<OrderSagaState>()
            .HasKey(x => x.OrderId); // OrderId’yi PK yapıyoruz

        // Opsiyonel: CorrelationId için index
        modelBuilder.Entity<OrderSagaState>()
            .HasIndex(x => x.CorrelationId)
            .IsUnique();

        // OutboxMessage mapping (en azından PK)
        modelBuilder.Entity<OutboxMessage>()
            .HasKey(x => x.Id);
    }
}