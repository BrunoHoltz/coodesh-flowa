using FixOrderBooking.Client.Application;
using FixOrderBooking.Client.Infra;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddHttpClient<OrdersService>();

builder.Services.AddSingleton<FixClientApplication>();
builder.Services.AddSingleton<FixAcceptorApplication>();
builder.Services.AddHostedService<FixClientService>();
builder.Services.AddHostedService<FixAcceptorService>();

var app = builder.Build();

app.MapOrderEndpoints();

app.Run();
