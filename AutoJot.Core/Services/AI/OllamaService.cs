using System.Net.Http.Json;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using Core.Shared;
using Microsoft.Extensions.Options;

namespace Core.Services.AI;

public class OllamaService : IAiService
{
    private readonly HttpClient _httpClient;
    private readonly string _model;

    public OllamaService(IOptions<OllamaOptions> options, HttpClient httpClient)
    {
        _httpClient = httpClient;
        _model = options.Value.Model;
    }

    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter() },
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    private async Task<TPrompt> QueryOllamaAsync<TPrompt>(
        string prompt,
        CancellationToken cancellationToken
    )
        where TPrompt : IPrompt
    {
        using var activity = AutoJotDiagnostics.ActivitySource.StartActivity(
            $"ai.ollama {typeof(TPrompt).Name}"
        );
        activity?.SetTag("ai.provider", "ollama");
        activity?.SetTag("ai.model", _model);

        var response = await _httpClient.PostAsJsonAsync(
            "api/generate",
            new
            {
                model = _model,
                stream = false,
                system = $"Use the language used by the user input \n\n {TPrompt.OllamaInstructions}",
                format = JsonSerializer.Deserialize<dynamic>(TPrompt.JsonSchema),
                prompt,
            },
            cancellationToken
        );

        response.EnsureSuccessStatusCode();

        var responseString = await response.Content.ReadAsStringAsync(cancellationToken);
        using var doc = JsonDocument.Parse(responseString);
        var responseNode = doc.RootElement.GetProperty("response").GetString();

        var result = JsonSerializer.Deserialize<TPrompt>(
            responseNode ?? string.Empty,
            _jsonOptions
        );

        return result!;
    }

    public async Task<MessageClassificationResult> ClassifyMessage(
        string userInput,
        CancellationToken cancellationToken = default
    )
    {
        var prompt = $"User input: {userInput}";

        return await QueryOllamaAsync<MessageClassificationResult>(prompt, cancellationToken);
    }

    public async Task<CreateNoteResult> CreateNote(
        string userInput,
        List<string> existingFolders,
        CancellationToken cancellationToken = default
    )
    {
        var prompt = $"""
            Existing folders: {string.Join(", ", existingFolders)}
            ---
            User input: {userInput}
            """;

        return await QueryOllamaAsync<CreateNoteResult>(prompt, cancellationToken);
    }

    public async Task<UpdateNoteResult> UpdateNote(
        string input,
        string existingFileContent,
        CancellationToken cancellationToken = default
    )
    {
        var prompt = $"""
            Existing file content: {existingFileContent}
            ---
            Current date: {DateTime.Now:yyyy-MM-dd}
            ---
            User input: {input}
            """;

        return await QueryOllamaAsync<UpdateNoteResult>(prompt, cancellationToken);
    }

    public async Task<List<string>> GetKeyWords(
        string input,
        CancellationToken cancellationToken = default
    )
    {
        var prompt = $"User input: {input}";
        var result = await QueryOllamaAsync<GetKeywordsResult>(prompt, cancellationToken);
        return result.Keywords;
    }
}
