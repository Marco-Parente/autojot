using AutoJot.ServiceDefaults;
using AutoJot.Worker;
using Core.Shared;

var builder = Host.CreateApplicationBuilder(args);
builder.AddServiceDefaults();
builder.Services.AddCoreServices(builder.Configuration);
builder.Services.AddHostedService<TelegramBotWorker>();

var host = builder.Build();
host.Run();
