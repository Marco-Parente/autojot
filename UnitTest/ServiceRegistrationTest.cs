using Core.Shared;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace UnitTest;

/// <summary>
/// Misconfiguration used to surface as a NullReferenceException on the first message, swallowed by
/// the bot's catch-all. These assert it now fails at startup instead.
/// </summary>
public class ServiceRegistrationTest : IDisposable
{
    private readonly string _vault = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());

    public ServiceRegistrationTest()
    {
        Directory.CreateDirectory(_vault);
    }

    public void Dispose()
    {
        if (Directory.Exists(_vault))
        {
            Directory.Delete(_vault, true);
        }
    }

    private IHost BuildHost(Dictionary<string, string?> overrides)
    {
        var settings = new Dictionary<string, string?>
        {
            ["AutoJot:RootPath"] = _vault,
            ["AutoJot:AiProvider"] = "OpenAi",
            ["OpenAi:ApiKey"] = "test-key",
            ["Telegram:BotToken"] = "test-token",
        };

        foreach (var (key, value) in overrides)
        {
            settings[key] = value;
        }

        var builder = Host.CreateEmptyApplicationBuilder(null);
        builder.Configuration.AddInMemoryCollection(settings);
        builder.Services.AddLogging();
        builder.Services.AddCoreServices(builder.Configuration);

        return builder.Build();
    }

    [Fact]
    public async Task Start_SucceedsWithValidConfiguration()
    {
        using var host = BuildHost([]);

        await host.StartAsync();
        await host.StopAsync();
    }

    [Theory]
    [InlineData("AutoJot:RootPath", null)]
    [InlineData("AutoJot:RootPath", "")]
    [InlineData("Telegram:BotToken", null)]
    [InlineData("OpenAi:ApiKey", null)]
    public async Task Start_FailsWhenRequiredSettingIsMissing(string key, string? value)
    {
        using var host = BuildHost(new Dictionary<string, string?> { [key] = value });

        await Assert.ThrowsAsync<OptionsValidationException>(() => host.StartAsync());
    }

    [Fact]
    public async Task Start_FailsWhenVaultDoesNotExist()
    {
        using var host = BuildHost(
            new Dictionary<string, string?>
            {
                ["AutoJot:RootPath"] = Path.Combine(_vault, "does-not-exist"),
            }
        );

        var exception = await Assert.ThrowsAsync<OptionsValidationException>(
            () => host.StartAsync()
        );
        Assert.Contains("does not point at an existing directory", exception.Message);
    }

    [Fact]
    public async Task Start_ValidatesOllamaSettingsWhenOllamaIsSelected()
    {
        using var host = BuildHost(
            new Dictionary<string, string?>
            {
                ["AutoJot:AiProvider"] = "Ollama",
                ["Ollama:Endpoint"] = null,
            }
        );

        await Assert.ThrowsAsync<OptionsValidationException>(() => host.StartAsync());
    }

    [Fact]
    public async Task Start_IgnoresOpenAiSettingsWhenOllamaIsSelected()
    {
        using var host = BuildHost(
            new Dictionary<string, string?>
            {
                ["AutoJot:AiProvider"] = "Ollama",
                ["Ollama:Endpoint"] = "http://localhost:11434/",
                ["Ollama:Model"] = "deepseek-r1:8b",
                ["OpenAi:ApiKey"] = null,
            }
        );

        await host.StartAsync();
        await host.StopAsync();
    }
}
