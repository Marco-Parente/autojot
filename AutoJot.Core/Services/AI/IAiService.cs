namespace Core.Services.AI;

public interface IAiService
{
    Task<MessageClassificationResult> ClassifyMessage(string userInput);
    Task<CreateNoteResult> CreateNote(string input, List<string> existingFolders);
    Task<UpdateNoteResult> UpdateNote(string input, string existingFileContent);
    Task<List<string>> GetKeyWords(string input);
    Task Test();
}

public class CreateNoteResult
{
    public string FilePath { get; set; } = null!;
    public string Content { get; set; } = null!;
}

public class UpdateNoteResult
{
    public string Content { get; set; } = null!;
}

public class MessageClassificationResult
{
    public ClassificationType ClassificationType { get; set; }
    public List<string> Keywords { get; set; } = [];
}

public class GetKeywordsResult
{
    public List<string> Keywords { get; set; } = [];
}

public enum ClassificationType
{
    Query = 10,
    Upsert = 20,
}
