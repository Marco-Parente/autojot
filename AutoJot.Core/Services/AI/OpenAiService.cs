using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Configuration;
using OpenAI.Responses;

#pragma warning disable OPENAI001

namespace Core.Services.AI;

public class OpenAiService : IAiService
{
    private const string Model = "gpt-5-nano";

    private readonly IConfiguration _configuration;

    public OpenAiService(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter() },
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    private async Task<TPrompt> QueryOpenAiAsync<TPrompt>(string prompt)
        where TPrompt : IPrompt
    {
        var client = new OpenAIResponseClient(
            Model,
            _configuration.GetValue<string?>("OpenAi:ApiKey")
                ?? throw new Exception("The open ai api key is missing.")
        );

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

        var response = await client.CreateResponseAsync(prompt, options);

        var final = JsonSerializer.Deserialize<TPrompt>(
            response.Value.GetOutputText(),
            _jsonOptions
        );

        return final!;
    }

    public async Task<MessageClassificationResult> ClassifyMessage(string userInput)
    {
        var conversationHistory = new List<dynamic> { new { role = "user", content = userInput } };

        return await QueryOpenAiAsync<MessageClassificationResult>(
            JsonSerializer.Serialize(conversationHistory, _jsonOptions)
        );
    }

    public async Task<CreateNoteResult> CreateNote(
        string userInput,
        List<string>? existingFolders = null
    )
    {
        var conversationHistory = new List<dynamic>
        {
            new { type = "new-input", content = userInput },
            new { type = "existing-folders", content = existingFolders },
        };

        return await QueryOpenAiAsync<CreateNoteResult>(
            JsonSerializer.Serialize(conversationHistory, _jsonOptions)
        );
    }

    public async Task<UpdateNoteResult> UpdateNote(string input, string existingFileContent)
    {
        var conversationHistory = new List<dynamic>
        {
            new { type = "new-input", content = input },
            new { type = "existing-file-content", content = existingFileContent },
        };

        return await QueryOpenAiAsync<UpdateNoteResult>(
            JsonSerializer.Serialize(conversationHistory, _jsonOptions)
        );
    }

    public async Task<List<string>> GetKeyWords(string input)
    {
        var conversationHistory = new List<dynamic> { new { type = "input", content = input } };

        return (
            await QueryOpenAiAsync<GetKeywordsResult>(
                JsonSerializer.Serialize(conversationHistory, _jsonOptions)
            )
        ).Keywords;
    }
}
