namespace Core.Services.Files;

public class LocalFileService : IFilesService
{
    /// <summary>
    /// Resolves a vault-relative path to an absolute one, refusing anything that escapes the vault.
    /// Path.Combine silently drops <paramref name="rootPath"/> when the second argument is rooted,
    /// so an absolute or "../" path suggested by the AI would otherwise write outside the vault.
    /// </summary>
    private static string ResolveVaultPath(
        string rootPath,
        string relativeFilePath,
        bool requireMarkdown = false
    )
    {
        if (string.IsNullOrWhiteSpace(rootPath))
            throw new ArgumentException("Root path is required", nameof(rootPath));

        if (string.IsNullOrWhiteSpace(relativeFilePath))
            throw new ArgumentException("File path is required", nameof(relativeFilePath));

        var root = Path.GetFullPath(rootPath);
        var rootPrefix = root.EndsWith(Path.DirectorySeparatorChar)
            ? root
            : root + Path.DirectorySeparatorChar;

        var fullPath = Path.GetFullPath(Path.Combine(root, relativeFilePath));

        if (!fullPath.StartsWith(rootPrefix, StringComparison.Ordinal))
        {
            throw new VaultPathException(
                $"Path '{relativeFilePath}' resolves outside the notes vault."
            );
        }

        if (requireMarkdown && !fullPath.EndsWith(".md", StringComparison.OrdinalIgnoreCase))
        {
            throw new VaultPathException($"Path '{relativeFilePath}' is not a Markdown note.");
        }

        return fullPath;
    }

    public List<string> GetFolders(string? rootPath = null)
    {
        rootPath ??= Directory.GetCurrentDirectory();

        var result = new List<string>();

        try
        {
            foreach (var dir in Directory.EnumerateDirectories(rootPath))
            {
                var attr = File.GetAttributes(dir);

                // Skip hidden or system folders
                if ((attr & (FileAttributes.Hidden | FileAttributes.System)) == 0)
                {
                    // Add relative path
                    result.Add(Path.GetRelativePath(rootPath, dir));

                    // Recurse into subdirectories
                    result.AddRange(
                        GetFolders(dir).Select(sub => Path.Combine(Path.GetFileName(dir), sub))
                    );
                }
            }
        }
        catch (UnauthorizedAccessException)
        {
            // Skip folders you can’t access
        }

        return result;
    }

    public string GetFileContent(string rootPath, string relativeFilePath)
    {
        return File.ReadAllText(ResolveVaultPath(rootPath, relativeFilePath));
    }

    public bool FileExists(string rootPath, string relativeFilePath)
    {
        try
        {
            return File.Exists(ResolveVaultPath(rootPath, relativeFilePath));
        }
        catch (VaultPathException)
        {
            return false;
        }
    }

    public void UpsertFile(string rootPath, string relativeFilePath, string content)
    {
        var fullPath = ResolveVaultPath(rootPath, relativeFilePath, requireMarkdown: true);

        try
        {
            var directory = Path.GetDirectoryName(fullPath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllText(fullPath, content ?? string.Empty);
        }
        catch (Exception ex)
        {
            throw new IOException(
                $"Failed to write file '{relativeFilePath}' in '{rootPath}'.",
                ex
            );
        }
    }
}
