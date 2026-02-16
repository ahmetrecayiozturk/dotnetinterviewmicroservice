using ApiGateway.Consumers;
using APIGateway.Data;
using APIGateway.Models;
using common.Events;
using MassTransit;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "API Gateway",
        Version = "v1"
    });
});

// DB
builder.Services.AddDbContext<GatewayDbContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("DefaultConnection")
        // Docker içi default
        ?? "Server=sqlserver;Database=GatewayDb;User Id=sa;Password=Pass@word123!;TrustServerCertificate=True",
        sql => sql.EnableRetryOnFailure()
    ));

// MassTransit + RabbitMQ
builder.Services.AddMassTransit(x =>
{
    // Burada senin consumer sýnýf isimlerine göre düzenle
    x.AddConsumer<OrderStatusConsumers>();
    x.AddConsumer<StockInsufficientConsumer>();
    x.AddConsumer<PaymentFailedConsumer>();
    x.AddConsumer<ShippingFailedConsumer>();

    x.UsingRabbitMq((context, cfg) =>
    {
        cfg.Host(builder.Configuration["RabbitMQ:Host"] ?? "rabbitmq", "/", h =>
        {
            h.Username(builder.Configuration["RabbitMQ:Username"] ?? "guest");
            h.Password(builder.Configuration["RabbitMQ:Password"] ?? "guest");
        });

        cfg.ConfigureEndpoints(context);
    });
});

builder.Logging.AddConsole();

var app = builder.Build();

// EF Core migrations otomatik çalýþsýn
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<GatewayDbContext>();
    await db.Database.MigrateAsync();
}

app.UseSwagger();
app.UseSwaggerUI();

// ==============================
// Create order
// ==============================
app.MapPost("/api/orders", async (
    [FromBody] CreateOrderRequest request,
    [FromServices] IPublishEndpoint publisher,
    [FromServices] GatewayDbContext db,
    [FromServices] ILogger<Program> logger) =>
{
    var orderId = Guid.NewGuid();
    var correlationId = Guid.NewGuid();

    logger.LogInformation("[GATEWAY] [{CorrelationId}] Order received: {OrderId}, ProductId={ProductId}, Quantity={Quantity}",
        correlationId, orderId, request.ProductId, request.Quantity);

    var orderStatus = new OrderStatus
    {
        Id = Guid.NewGuid(),
        OrderId = orderId,
        CorrelationId = correlationId,
        Status = "Processing"
    };
    db.OrderStatuses.Add(orderStatus);
    await db.SaveChangesAsync();

    var orderStartedEvent = new OrderStartedEvent(orderId, request.ProductId, request.Quantity)
    {
        CorrelationId = correlationId
    };
    await publisher.Publish(orderStartedEvent);

    return Results.Ok(new
    {
        OrderId = orderId,
        CorrelationId = correlationId,
        Status = "Processing",
        Message = "Your order is being processed...",
        PollingUrl = $"/api/orders/{orderId}"
    });
});

// ==============================
// Get order status
// ==============================
app.MapGet("/api/orders/{orderId:guid}", async (
    [FromRoute] Guid orderId,
    [FromServices] GatewayDbContext db) =>
{
    var orderStatus = await db.OrderStatuses
        .FirstOrDefaultAsync(o => o.OrderId == orderId);

    if (orderStatus == null)
        return Results.NotFound(new { Message = "Order not found" });

    return Results.Ok(new
    {
        orderStatus.OrderId,
        orderStatus.CorrelationId,
        orderStatus.Status,
        orderStatus.TrackingNumber,
        orderStatus.FailureReason,
        orderStatus.CreatedAt,
        orderStatus.CompletedAt
    });
});

// ==============================
// Last 50 orders
// ==============================
app.MapGet("/api/orders", async ([FromServices] GatewayDbContext db) =>
{
    var orders = await db.OrderStatuses
        .OrderByDescending(o => o.CreatedAt)
        .Take(50)
        .ToListAsync();

    return Results.Ok(orders);
});

app.Logger.LogInformation("API Gateway is running...");
app.Run();

// DTO
record CreateOrderRequest(Guid ProductId, int Quantity);