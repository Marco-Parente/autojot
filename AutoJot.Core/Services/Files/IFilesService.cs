namespace Core.Services.Files;

/// <summary>
/// Raw access to the notes vault. Searching lives in <see cref="Search.INoteIndex"/>.
/// </summary>
public interface IFilesService
{
    List<string> GetFolders(string? rootPath = null);
    string GetFileContent(string rootPath, string relativeFilePath);
    bool FileExists(string rootPath, string relativeFilePath);
    void UpsertFile(string rootPath, string relativeFilePath, string content);
}

/// <summary>
/// Thrown when a path would resolve outside the notes vault, or is otherwise not a note we are
/// willing to touch. File paths reaching the file service can be suggested by the AI model, so they
/// are treated as untrusted input.
/// </summary>
public class VaultPathException : Exception
{
    public VaultPathException(string message)
        : base(message) { }
}
