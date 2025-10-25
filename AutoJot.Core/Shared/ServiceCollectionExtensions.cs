using Core.Services;
using Core.Services.AI;
using Core.Services.Bot;
using Core.Services.Files;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Core.Shared;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddCoreServices(
        this IServiceCollection services,
        IConfiguration configuration
    )
    {
        services.AddSingleton<IUserStateService, InMemoryUserStateService>();

        services.AddScoped<IFilesService, LocalFileService>();
        services.AddScoped<IMessageService, TelegramMessageService>();
        services.AddScoped<IAiService, OpenAiService>();
        services.AddScoped<IBotService, BotService>();

        return services;
    }
}
