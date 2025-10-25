namespace Core.Services.Files;

public interface IFilesService
{
    List<string> GetFolders(string? rootPath = null);
    string GetFileContent(string rootPath, string relativeFilePath);
    bool FileExists(string rootPath, string relativeFilePath);
    void UpsertFile(string rootPath, string relativeFilePath, string content);
    List<MatchResult> GetMatchResults(string rootPath, List<string> keywords);
}

public class MatchResult
{
    public string RelativeFilePath { get; set; } = null!;
    public int Score { get; set; }
}

public class FileSummary
{
    public string FilePath { get; set; } = null!;
    public List<string>? Tags { get; set; } = [];
}
