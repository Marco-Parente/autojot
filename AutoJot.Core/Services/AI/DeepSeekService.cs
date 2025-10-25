using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using Core.Services.Files;
using Microsoft.Extensions.Configuration;

namespace Core.Services.AI;

public class DeepSeekService : IAiService
{
    private const string Model = "deepseek-nano"; // Example model name for DeepSeek
    private readonly IConfiguration _configuration;
    private readonly IFilesService _filesService;

    public DeepSeekService(IConfiguration configuration, IFilesService filesService)
    {
        _configuration = configuration;
        _filesService = filesService;
    }

    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter() },
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    public async Task<MessageClassificationResult> ClassifyMessage(string userInput)
    {
        // TODO: Replace with DeepSeek API call
        await Task.Delay(10); // Simulate async call
        return new MessageClassificationResult
        {
            ClassificationType = ClassificationType.Query,
            Keywords = new List<string> { "stub-keyword" },
        };
    }

    public async Task<CreateNoteResult> CreateNote(string input, List<string> existingFolders)
    {
        // TODO: Replace with DeepSeek API call
        await Task.Delay(10);
        return new CreateNoteResult
        {
            FilePath = "stub/path/note.md",
            Content = "---\ntags: [stub]\ncreated_at: 2025-10-25\n---\nStub content",
        };
    }

    public async Task<UpdateNoteResult> UpdateNote(string input, string existingFileContent)
    {
        // TODO: Replace with DeepSeek API call
        await Task.Delay(10);
        return new UpdateNoteResult
        {
            Content =
                "---\ntags: [stub]\ncreated_at: 2025-10-25\nupdated_at: 2025-10-25\n---\nUpdated stub content",
        };
    }

    public async Task<List<string>> GetKeyWords(string input)
    {
        // TODO: Replace with DeepSeek API call
        await Task.Delay(10);
        return new List<string>
        {
            "stub-keyword1",
            "stub-keyword2",
            "stub-keyword3",
            "stub-keyword4",
            "stub-keyword5",
        };
    }

    public async Task Test()
    {
        // TODO: Replace with DeepSeek API call
        await Task.Delay(10);
    }
}
