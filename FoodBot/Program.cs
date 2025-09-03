using FoodBot;

var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddBotServices(builder.Configuration)
    .AddBotAuth(builder.Configuration);

var app = builder.Build();

app.UseBotEndpoints();

app.Run();
