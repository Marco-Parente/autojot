using Core.Services.AI;
using Core.Services.Bot;
using Core.Services.Files;
using Core.Services.Message;
using Core.Services.Search;
using Core.Services.UserState;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Telegram.Bot;

namespace Core.Shared;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddCoreServices(
        this IServiceCollection services,
        IConfiguration configuration
    )
    {
        services
            .AddOptions<AutoJotOptions>()
            .Bind(configuration.GetSection(AutoJotOptions.SectionName))
            .ValidateDataAnnotations()
            .Validate(
                o => string.IsNullOrEmpty(o.RootPath) || Directory.Exists(o.RootPath),
                "AutoJot:RootPath does not point at an existing directory."
            )
            .ValidateOnStart();

        services
            .AddOptions<TelegramOptions>()
            .Bind(configuration.GetSection(TelegramOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddMemoryCache();

        // Singleton: the index is a process-wide cache of the vault, watched for changes.
        services.AddSingleton<INoteIndex, NoteIndex>();
        services.AddSingleton<IUserStateService, InMemoryUserStateService>();
        services.AddSingleton<UserLocks>();

        // One client, one pooled HttpClient. Creating a TelegramBotClient per outbound message
        // creates a new HttpClient each time and exhausts sockets under load.
        services
            .AddHttpClient(nameof(TelegramBotClient))
            .AddTypedClient<ITelegramBotClient>(
                (httpClient, sp) => new TelegramBotClient(
                    sp.GetRequiredService<IOptions<TelegramOptions>>().Value.BotToken,
                    httpClient
                )
            )
            // The bot token is part of every Telegram request URL, and the default HttpClient
            // logger writes that URL at Information level — which would print the token into the
            // logs on every call. Telegram.Bot surfaces its own errors, so drop those loggers.
            .RemoveAllLoggers();
        // No resilience handler here on purpose: getUpdates is a long-poll that stays open for
        // tens of seconds by design, and the standard handler would sever it on its 30s timeout.

        services.AddScoped<IFilesService, LocalFileService>();
        services.AddScoped<IMessageService, TelegramMessageService>();
        services.AddScoped<IBotService, BotService>();

        var usesOpenAi =
            configuration.GetSection(AutoJotOptions.SectionName).Get<AutoJotOptions>()?.UsesOpenAi
            ?? false;

        if (usesOpenAi)
        {
            services
                .AddOptions<OpenAiOptions>()
                .Bind(configuration.GetSection(OpenAiOptions.SectionName))
                .ValidateDataAnnotations()
                .ValidateOnStart();

            // The OpenAI SDK applies its own retry policy, so no resilience handler is added here.
            services.AddScoped<IAiService, OpenAiService>();
        }
        else
        {
            services
                .AddOptions<OllamaOptions>()
                .Bind(configuration.GetSection(OllamaOptions.SectionName))
                .ValidateDataAnnotations()
                .ValidateOnStart();

            services
                .AddHttpClient<IAiService, OllamaService>(
                    (sp, c) =>
                        c.BaseAddress = new Uri(
                            sp.GetRequiredService<IOptions<OllamaOptions>>().Value.Endpoint
                        )
                )
                .AddStandardResilienceHandler(o =>
                {
                    // The defaults (10s per attempt, 30s total) are sized for ordinary web APIs.
                    // A local model generating a whole note routinely runs longer than that.
                    o.AttemptTimeout.Timeout = TimeSpan.FromMinutes(2);
                    o.TotalRequestTimeout.Timeout = TimeSpan.FromMinutes(5);
                    o.CircuitBreaker.SamplingDuration = TimeSpan.FromMinutes(4);
                    o.Retry.MaxRetryAttempts = 2;
                });
        }

        return services;
    }
}
