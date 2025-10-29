using System.Net.Http.Json;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Configuration;

namespace Core.Services.AI;

public class OllamaService : IAiService
{
    private readonly HttpClient _httpClient;
    private readonly string _model;

    public OllamaService(IConfiguration configuration, HttpClient httpClient)
    {
        _httpClient = httpClient;
        _model =
            configuration.GetValue<string?>("Ollama:Model")
            ?? throw new InvalidOperationException("Ollama:Model is missing");
    }

    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter() },
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    private async Task<TPrompt> QueryOllamaAsync<TPrompt>(string prompt)
        where TPrompt : IPrompt
    {
        var response = await _httpClient.PostAsJsonAsync(
            "api/generate",
            new
            {
                model = _model,
                stream = false,
                system = $"Use the language used by the user input \n\n {TPrompt.OllamaInstructions}",
                format = JsonSerializer.Deserialize<dynamic>(TPrompt.JsonSchema),
                prompt,
            }
        );

        response.EnsureSuccessStatusCode();

        var responseString = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(responseString);
        var responseNode = doc.RootElement.GetProperty("response").GetString();

        var result = JsonSerializer.Deserialize<TPrompt>(
            responseNode ?? string.Empty,
            _jsonOptions
        );

        return result!;
    }

    public async Task<MessageClassificationResult> ClassifyMessage(string userInput)
    {
        var prompt = $"User input: {userInput}";

        var resultJson = await QueryOllamaAsync<MessageClassificationResult>(prompt);

        return resultJson;
    }

    public async Task<CreateNoteResult> CreateNote(string userInput, List<string> existingFolders)
    {
        var prompt = $"""
            Existing folders: {string.Join(", ", existingFolders)}
            ---
            User input: {userInput}
            """;

        var result = await QueryOllamaAsync<CreateNoteResult>(prompt);

        return result;
    }

    public async Task<UpdateNoteResult> UpdateNote(string input, string existingFileContent)
    {
        var prompt = $"""
            Existing file content: {existingFileContent}
            ---
            Current date: {DateTime.Now:yyyy-MM-dd}
            ---
            User input: {input}
            """;
        var result = await QueryOllamaAsync<UpdateNoteResult>(prompt);
        return result;
    }

    public async Task<List<string>> GetKeyWords(string input)
    {
        var prompt = $"User input: {input}";
        var result = await QueryOllamaAsync<GetKeywordsResult>(prompt);
        return result.Keywords;
    }
}
