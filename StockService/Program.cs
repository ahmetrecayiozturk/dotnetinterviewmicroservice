using common.Interfaces;
using common.Services;
using Microsoft.EntityFrameworkCore;
using MassTransit;
using MediatR;
using StockService.Application.Commands;
using StockService.Consumers;
using StockService.Infrastructure;
using StockService.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<StockDbContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("DefaultConnection")
        ?? "Server=sqlserver;Database=StockDb;User Id=sa;Password=Pass@word123!;TrustServerCertificate=True"
    ));

builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();

builder.Services.AddMediatR(cfg =>
    cfg.RegisterServicesFromAssembly(typeof(ReserveStockCommand).Assembly));

builder.Services.AddHostedService<OutboxProcessor>();

builder.Services.AddMassTransit(x =>
{
    x.AddConsumer<OrderStartedConsumer>();
    x.AddConsumer<StockReleaseConsumer>();

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

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<StockDbContext>();
    await db.Database.MigrateAsync();
}

app.Run();