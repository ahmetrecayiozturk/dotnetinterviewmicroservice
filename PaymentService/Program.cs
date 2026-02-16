using common.Interfaces;
using common.Services;
using MassTransit;
using MediatR;
using Microsoft.EntityFrameworkCore;
using PaymentService.Application.Command;
using PaymentService.Consumers;
using PaymentService.Infrastructure;
using PaymentService.Services;

var builder = WebApplication.CreateBuilder(args);

// DB
builder.Services.AddDbContext<PaymentDbContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("DefaultConnection")
        ?? "Server=sqlserver;Database=PaymentDb;User Id=sa;Password=Pass@word123!;TrustServerCertificate=True"
    ));

builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();

// MediatR
builder.Services.AddMediatR(cfg =>
    cfg.RegisterServicesFromAssembly(typeof(ProcessPaymentCommand).Assembly));

// Outbox worker
builder.Services.AddHostedService<OutboxProcessor>();

// MassTransit + RabbitMQ
builder.Services.AddMassTransit(x =>
{
    x.AddConsumer<PaymentConsumer>();
    x.AddConsumer<PaymentRefundConsumer>();

    x.UsingRabbitMq((context, cfg) =>
    {
        cfg.Host(builder.Configuration["RabbitMQ:Host"] ?? "rabbitmq", "/", h =>
        {
            h.Username(builder.Configuration["RabbitMQ:Username"] ?? "guest");
            h.Password(builder.Configuration["RabbitMQ:Password"] ?? "guest");
        });

        cfg.UseMessageRetry(r => r.Intervals(1000, 5000, 10000));
        cfg.ConfigureEndpoints(context);
    });
});

builder.Logging.AddConsole();

var app = builder.Build();

// EF Core migrations
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<PaymentDbContext>();
    await db.Database.MigrateAsync();
}

app.Run();