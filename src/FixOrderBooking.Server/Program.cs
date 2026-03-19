using FixOrderBooking.Server.Application;
using FixOrderBooking.Server.Application.Abstractions;
using FixOrderBooking.Server.Infra;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton<IOrderRepository, InMemoryOrderRepository>();
builder.Services.AddSingleton<IOrderBookService, OrderBookService>();
builder.Services.AddSingleton<FixServerApplication>();
builder.Services.AddHostedService<FixServerService>();

var app = builder.Build();

app.MapOrderBookHttp();

app.Run();
