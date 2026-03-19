using FixOrderBooking.Client.FIX;
using FixOrderBooking.Client.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddHttpClient("server", client =>
    client.BaseAddress = new Uri(builder.Configuration["ServerBaseUrl"] ?? "http://localhost:5000"));

builder.Services.AddTransient(sp =>
    sp.GetRequiredService<IHttpClientFactory>().CreateClient("server"));

builder.Services.AddSingleton<FixClientApplication>();
builder.Services.AddSingleton<FixAcceptorApplication>();
builder.Services.AddHostedService<FixClientService>();
builder.Services.AddHostedService<FixAcceptorService>();

var app = builder.Build();

app.MapOrderEndpoints();

app.Run();
