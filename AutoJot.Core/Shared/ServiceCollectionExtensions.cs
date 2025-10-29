using Core.Services.AI;
using Core.Services.Bot;
using Core.Services.Files;
using Core.Services.Message;
using Core.Services.UserState;
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
        services.AddScoped<IBotService, BotService>();

        if (
            configuration
                .GetValue<string?>("AutoJot:AiProvider")
                ?.Equals(IAiService.Types.OpenAi, StringComparison.InvariantCultureIgnoreCase)
            ?? false
        )
        {
            services.AddScoped<IAiService, OpenAiService>();
        }
        else
        {
            services.AddHttpClient<IAiService, OllamaService>(c =>
            {
                c.BaseAddress = new Uri(
                    configuration.GetValue<string?>("Ollama:Endpoint")
                        ?? throw new InvalidOperationException("Ollama:Endpoint not configured")
                );
            });
        }

        return services;
    }
}
