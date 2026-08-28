using System.ComponentModel.DataAnnotations;
using Core.Services.AI;

namespace Core.Shared;

/// <summary>
/// Options are validated at startup rather than on the first message, so a missing vault path or
/// token fails loudly while someone is watching, instead of surfacing as a swallowed exception.
/// </summary>
public class AutoJotOptions
{
    public const string SectionName = "AutoJot";

    [Required(
        AllowEmptyStrings = false,
        ErrorMessage = "AutoJot:RootPath must point at your notes vault."
    )]
    public string RootPath { get; set; } = null!;

    public string AiProvider { get; set; } = IAiService.Types.Ollama;

    /// <summary>
    /// Telegram user ids allowed to talk to the bot. Empty means nobody: the bot can read and
    /// overwrite the whole vault, so access is opt-in.
    /// </summary>
    public long[] AllowedUserIds { get; set; } = [];

    public bool UsesOpenAi =>
        AiProvider.Equals(IAiService.Types.OpenAi, StringComparison.InvariantCultureIgnoreCase);
}

public class OpenAiOptions
{
    public const string SectionName = "OpenAi";

    [Required(AllowEmptyStrings = false, ErrorMessage = "OpenAi:ApiKey is required.")]
    public string ApiKey { get; set; } = null!;

    [Required(AllowEmptyStrings = false)]
    public string Model { get; set; } = "gpt-5-nano";
}

public class OllamaOptions
{
    public const string SectionName = "Ollama";

    [Required(AllowEmptyStrings = false, ErrorMessage = "Ollama:Endpoint is required.")]
    [Url]
    public string Endpoint { get; set; } = null!;

    [Required(AllowEmptyStrings = false, ErrorMessage = "Ollama:Model is required.")]
    public string Model { get; set; } = null!;
}

public class TelegramOptions
{
    public const string SectionName = "Telegram";

    [Required(AllowEmptyStrings = false, ErrorMessage = "Telegram:BotToken is required.")]
    public string BotToken { get; set; } = null!;
}
