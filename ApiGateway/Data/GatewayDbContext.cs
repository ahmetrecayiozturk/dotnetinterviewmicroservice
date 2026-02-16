using APIGateway.Models;
using Microsoft.EntityFrameworkCore;

namespace APIGateway.Data;

public class GatewayDbContext : DbContext
{
    public GatewayDbContext(DbContextOptions<GatewayDbContext> options) : base(options) { }

    public DbSet<OrderStatus> OrderStatuses => Set<OrderStatus>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<OrderStatus>()
            .HasIndex(e => e.OrderId)
            .IsUnique();

        modelBuilder.Entity<OrderStatus>()
            .HasIndex(e => e.CorrelationId)
            .IsUnique();
    }
}