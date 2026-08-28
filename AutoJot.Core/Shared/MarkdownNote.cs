using System.Text.RegularExpressions;

namespace Core.Shared;

/// <summary>
/// Splits a note into its YAML front matter tags and the prose below, which are scored differently
/// by the search index.
/// </summary>
public static class MarkdownNote
{
    // Start of string, optional BOM, then '---' on its own line, capturing until the next '---' on
    // its own line. (?s) so '.' matches newlines. Accepts both \n and \r\n.
    private static readonly Regex FrontMatter = new(
        @"\A(\uFEFF)?---\s*\r?\n(?s)(.*?)\r?\n---\s*\r?\n?",
        RegexOptions.Compiled
    );

    public static (List<string> Tags, string Body) Parse(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return ([], string.Empty);
        }

        var match = FrontMatter.Match(text);

        // No front matter: the whole document is body.
        return match.Success
            ? (YamlHelper.ExtractTags(match.Groups[2].Value), text[match.Length..])
            : ([], text);
    }
}
