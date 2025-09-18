using FoodBot;
using FoodBot.Logging;

var builder = WebApplication.CreateBuilder(args);

builder.Logging.AddProvider(new FileLoggerProvider("c:/log/fb"));

builder.Services
    .AddBotServices(builder.Configuration)
    .AddBotAuth(builder.Configuration);

var app = builder.Build();

app.UseBotEndpoints();

app.Run();
