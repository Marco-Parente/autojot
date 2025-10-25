using AutoJot.Worker;
using Core;
using Core.Shared;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddCoreServices(builder.Configuration);
builder.Services.AddHostedService<TelegramBotWorker>();

var host = builder.Build();
host.Run();
