namespace Core.Services.Search;

public interface INoteIndex
{
    /// <summary>
    /// Scores every note against the keywords. Notes that match nothing come back with a score of
    /// zero rather than being dropped, so callers decide what counts as a match.
    /// </summary>
    List<MatchResult> Search(IReadOnlyCollection<string> keywords);

    /// <summary>
    /// Marks the index stale. Called after the bot writes a note, so its own change is visible on
    /// the next search without waiting for a file system event to arrive.
    /// </summary>
    void Invalidate();

    /// <summary>Number of notes currently indexed. Built on first use if it has not been yet.</summary>
    int Count { get; }
}

public class MatchResult
{
    public string RelativeFilePath { get; set; } = null!;
    public int Score { get; set; }
}

public sealed record NoteIndexEntry(
    string RelativePath,
    string FileName,
    IReadOnlySet<string> Tags,
    IReadOnlyDictionary<string, int> BodyTokens,
    DateTime LastWriteUtc
);
