using common.Interfaces;
using MassTransit;
using MediatR;
using Microsoft.EntityFrameworkCore;
using ShippingService.Application.Commands;
using ShippingService.Consumer;
using ShippingService.Infrastructure;
using ShippingService.Service;


var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<ShippingDbContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("DefaultConnection")
        ?? "Server=sqlserver;Database=ShippingDb;User Id=sa;Password=Pass@word123!;TrustServerCertificate=True"
    ));

builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();

builder.Services.AddMediatR(cfg =>
    cfg.RegisterServicesFromAssembly(typeof(PrepareShipmentCommand).Assembly));

builder.Services.AddHostedService<OutboxProcessor>();

builder.Services.AddMassTransit(x =>
{
    x.AddConsumer<ShippingConsumer>();

    x.UsingRabbitMq((context, cfg) =>
    {
        cfg.Host(builder.Configuration["RabbitMQ:Host"] ?? "localhost", "/", h =>
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
    var db = scope.ServiceProvider.GetRequiredService<ShippingDbContext>();
    await db.Database.MigrateAsync();
}

app.Run();
/*
using common.Interfaces;
using MassTransit;
using MediatR;
using Microsoft.EntityFrameworkCore;
using ShippingService.Application.Commands;
using ShippingService.Consumer;
using ShippingService.Infrastructure;
using ShippingService.Service;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<ShippingDbContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("DefaultConnection") ??
        "Server=localhost;Database=ShippingDb;User Id=sa;Password=Pass@word123!;TrustServerCertificate=True",
        sql => sql.EnableRetryOnFailure()
    ));

builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();

builder.Services.AddMediatR(cfg =>
    cfg.RegisterServicesFromAssembly(typeof(PrepareShipmentCommand).Assembly));

builder.Services.AddHostedService<OutboxProcessor>();

builder.Services.AddMassTransit(x =>
{
    x.AddConsumer<ShippingConsumer>();

    x.UsingRabbitMq((context, cfg) =>
    {
        cfg.Host(builder.Configuration["RabbitMQ:Host"] ?? "localhost", "/", h =>
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
    var db = scope.ServiceProvider.GetRequiredService<ShippingDbContext>();
    await db.Database.MigrateAsync();
}

app.Run();
*/