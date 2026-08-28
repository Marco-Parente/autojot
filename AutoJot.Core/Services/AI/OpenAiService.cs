using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using Core.Shared;
using Microsoft.Extensions.Options;
using OpenAI.Responses;

#pragma warning disable OPENAI001

namespace Core.Services.AI;

public class OpenAiService : IAiService
{
    private readonly OpenAIResponseClient _client;
    private readonly string _model;

    public OpenAiService(IOptions<OpenAiOptions> options)
    {
        _model = options.Value.Model;
        _client = new OpenAIResponseClient(_model, options.Value.ApiKey);
    }

    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter() },
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    private async Task<TPrompt> QueryOpenAiAsync<TPrompt>(
        string prompt,
        CancellationToken cancellationToken
    )
        where TPrompt : IPrompt
    {
        using var activity = AutoJotDiagnostics.ActivitySource.StartActivity(
            $"ai.openai {typeof(TPrompt).Name}"
        );
        activity?.SetTag("ai.provider", "openai");
        activity?.SetTag("ai.model", _model);

        var options = new ResponseCreationOptions
        {
            TextOptions = new ResponseTextOptions
            {
                TextFormat = ResponseTextFormat.CreateJsonSchemaFormat(
                    "autojot_output",
                    BinaryData.FromBytes(Encoding.UTF8.GetBytes(TPrompt.JsonSchema)),
                    TPrompt.JsonSchemaDescription,
                    true
                ),
            },
            Instructions = TPrompt.OpenAiInstructions,
        };

        var response = await _client.CreateResponseAsync(prompt, options, cancellationToken);

        var final = JsonSerializer.Deserialize<TPrompt>(
            response.Value.GetOutputText(),
            _jsonOptions
        );

        return final!;
    }

    public async Task<MessageClassificationResult> ClassifyMessage(
        string userInput,
        CancellationToken cancellationToken = default
    )
    {
        var conversationHistory = new List<dynamic> { new { role = "user", content = userInput } };

        return await QueryOpenAiAsync<MessageClassificationResult>(
            JsonSerializer.Serialize(conversationHistory, _jsonOptions),
            cancellationToken
        );
    }

    public async Task<CreateNoteResult> CreateNote(
        string userInput,
        List<string> existingFolders,
        CancellationToken cancellationToken = default
    )
    {
        var conversationHistory = new List<dynamic>
        {
            new { type = "new-input", content = userInput },
            new { type = "existing-folders", content = existingFolders },
        };

        return await QueryOpenAiAsync<CreateNoteResult>(
            JsonSerializer.Serialize(conversationHistory, _jsonOptions),
            cancellationToken
        );
    }

    public async Task<UpdateNoteResult> UpdateNote(
        string input,
        string existingFileContent,
        CancellationToken cancellationToken = default
    )
    {
        var conversationHistory = new List<dynamic>
        {
            new { type = "new-input", content = input },
            new { type = "existing-file-content", content = existingFileContent },
        };

        return await QueryOpenAiAsync<UpdateNoteResult>(
            JsonSerializer.Serialize(conversationHistory, _jsonOptions),
            cancellationToken
        );
    }

    public async Task<List<string>> GetKeyWords(
        string input,
        CancellationToken cancellationToken = default
    )
    {
        var conversationHistory = new List<dynamic> { new { type = "input", content = input } };

        return (
            await QueryOpenAiAsync<GetKeywordsResult>(
                JsonSerializer.Serialize(conversationHistory, _jsonOptions),
                cancellationToken
            )
        ).Keywords;
    }
}
