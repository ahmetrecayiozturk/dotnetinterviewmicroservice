using common.Models;
using Microsoft.EntityFrameworkCore;
using PaymentService.Domain;

namespace PaymentService.Infrastructure
{
    public class PaymentDbContext:DbContext
    {
        public PaymentDbContext(DbContextOptions<PaymentDbContext> options) : base(options) { }

        public DbSet<Payment> Payments => Set<Payment>();
        public DbSet<ProcessedEvent> ProcessedEvents => Set<ProcessedEvent>();
        public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<ProcessedEvent>()
                .HasIndex(e => e.MessageId)
                .IsUnique();

            modelBuilder.Entity<Payment>()
                .HasIndex(p => p.OrderId)
                .IsUnique();
        }


    }
}
