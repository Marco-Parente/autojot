using System.Text.RegularExpressions;
using Core.Shared;

namespace Core.Services.Files;

public class LocalFileService : IFilesService
{
    private static List<string> GetFiles(string? rootPath = null, string? searchPattern = "*")
    {
        searchPattern ??= "*";
        rootPath ??= Directory.GetCurrentDirectory();

        var result = new List<string>();

        try
        {
            foreach (var file in Directory.EnumerateFiles(rootPath, searchPattern))
            {
                var attr = File.GetAttributes(file);

                if ((attr & (FileAttributes.Hidden | FileAttributes.System)) == 0)
                {
                    result.Add(Path.GetRelativePath(rootPath, file));
                }
            }

            foreach (var dir in Directory.EnumerateDirectories(rootPath))
            {
                var attr = File.GetAttributes(dir);

                // skip hidden or system folders
                if ((attr & (FileAttributes.Hidden | FileAttributes.System)) == 0)
                {
                    result.AddRange(
                        GetFiles(dir, searchPattern)
                            .Select(sub => Path.Combine(Path.GetFileName(dir), sub))
                    );
                }
            }
        }
        catch (UnauthorizedAccessException)
        {
            // skip folders/files you can’t access
        }

        return result;
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
        return File.ReadAllText(Path.Combine(rootPath, relativeFilePath));
    }

    public bool FileExists(string rootPath, string relativeFilePath)
    {
        return File.Exists(Path.Combine(rootPath, relativeFilePath));
    }

    public List<string>? GetFileTags(string rootPath, string relativeFilePath)
    {
        var text = File.ReadAllText(Path.Combine(rootPath, relativeFilePath));

        if (string.IsNullOrEmpty(text))
            return null;

        // Match start-of-string, optional BOM, then '---' on its own line, then capture until next '---' on its own line.
        // DOTALL equivalent: (?s) so '.' matches newlines.
        // Accept both \n and \r\n line endings.
        var pattern = @"\A(\uFEFF)?---\s*\r?\n(?s)(.*?)\r?\n---\s*\r?\n?";
        var match = Regex.Match(text, pattern, RegexOptions.None);

        if (!match.Success)
        {
            // no front matter -> full text is content
            return null;
        }

        var yaml = match.Groups[2].Value;
        // var content = text.Substring(match.Length); // rest of document after closing ---

        return string.IsNullOrEmpty(yaml) ? null : YamlHelper.ExtractTags(yaml);
    }

    public List<FileSummary> GetFilesWithTags(string rootPath)
    {
        var files = GetFiles(rootPath);

        return files
            .Select(file => new FileSummary { FilePath = file, Tags = GetFileTags(rootPath, file) })
            .ToList();
    }

    public void UpsertFile(string rootPath, string relativeFilePath, string content)
    {
        if (string.IsNullOrWhiteSpace(rootPath))
            throw new ArgumentNullException(nameof(rootPath));

        if (string.IsNullOrWhiteSpace(relativeFilePath))
            throw new ArgumentNullException(nameof(relativeFilePath));

        var fullPath = Path.Combine(rootPath, relativeFilePath);

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

    public List<MatchResult> GetMatchResults(string rootPath, List<string> keywords)
    {
        if (string.IsNullOrWhiteSpace(rootPath))
            throw new ArgumentNullException(nameof(rootPath));

        if (keywords.Count == 0)
            return [];

        if (!Directory.Exists(rootPath))
            throw new DirectoryNotFoundException($"Directory not found: {rootPath}");

        var results = new List<MatchResult>();

        // Enumerate all Markdown files (you can extend to .txt, etc.)
        var files = Directory.EnumerateFiles(rootPath, "*.md", SearchOption.AllDirectories);

        foreach (var filePath in files)
        {
            var tags = GetFileTags(rootPath, filePath) ?? [];

            var score = tags.Count(tag => keywords.Contains(tag, StringComparer.OrdinalIgnoreCase));

            var fileName = Path.GetFileNameWithoutExtension(filePath);
            score += keywords
                .Where(keyword => fileName.Contains(keyword, StringComparison.OrdinalIgnoreCase))
                .Sum(_ => 2);

            results.Add(
                new MatchResult
                {
                    RelativeFilePath = Path.GetRelativePath(rootPath, filePath),
                    Score = score,
                }
            );
        }

        // Sort results by descending score
        return results.OrderByDescending(r => r.Score).ThenBy(x => x.RelativeFilePath).ToList();
    }
}
