using System.Text.RegularExpressions;
using Core.Shared;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Core.Services.Search;

/// <summary>
/// An in-memory index of the vault. Searching used to re-read every note on every message; now each
/// note is read once and re-read only when it changes.
/// </summary>
public sealed class NoteIndex : INoteIndex, IDisposable
{
    private const int FileNameWeight = 3;
    private const int TagWeight = 2;
    private const int BodyWeight = 1;

    /// <summary>
    /// Cap on how much one keyword can earn from repetition in the body, so a note that happens to
    /// say "cake" thirty times cannot bury a note that is actually about cake.
    /// </summary>
    private const int MaxBodyHitsPerKeyword = 3;

    private const int MinTokenLength = 2;

    private static readonly Regex TokenPattern = new(
        @"[\p{L}\p{Nd}][\p{L}\p{Nd}'-]*",
        RegexOptions.Compiled
    );

    private readonly string _rootPath;
    private readonly ILogger<NoteIndex> _logger;
    private readonly Lock _gate = new();

    private FileSystemWatcher? _watcher;
    private IReadOnlyList<NoteIndexEntry> _entries = [];
    private bool _built;
    private volatile bool _stale;

    public NoteIndex(IOptions<AutoJotOptions> options, ILogger<NoteIndex> logger)
    {
        _rootPath = options.Value.RootPath;
        _logger = logger;

        StartWatching();
    }

    public int Count
    {
        get
        {
            EnsureFresh();
            return _entries.Count;
        }
    }

    public void Invalidate() => _stale = true;

    public List<MatchResult> Search(IReadOnlyCollection<string> keywords)
    {
        if (keywords.Count == 0)
        {
            return [];
        }

        EnsureFresh();

        return _entries
            .Select(entry => new MatchResult
            {
                RelativeFilePath = entry.RelativePath,
                Score = Score(entry, keywords),
            })
            .OrderByDescending(result => result.Score)
            .ThenBy(result => result.RelativeFilePath, StringComparer.Ordinal)
            .ToList();
    }

    private static int Score(NoteIndexEntry entry, IReadOnlyCollection<string> keywords)
    {
        var score = 0;

        foreach (var keyword in keywords)
        {
            if (string.IsNullOrWhiteSpace(keyword))
            {
                continue;
            }

            // Substring, not whole word: note names are hyphenated, so "cake" should find
            // "chocolate-cake.md".
            if (entry.FileName.Contains(keyword, StringComparison.OrdinalIgnoreCase))
            {
                score += FileNameWeight;
            }

            if (entry.Tags.Contains(keyword))
            {
                score += TagWeight;
            }

            if (entry.BodyTokens.TryGetValue(keyword, out var occurrences))
            {
                score += BodyWeight * Math.Min(occurrences, MaxBodyHitsPerKeyword);
            }
        }

        return score;
    }

    private void EnsureFresh()
    {
        // The watcher only flips a flag, so a burst of events — which is exactly what a synced
        // folder like Google Drive produces — collapses into a single rebuild here.
        if (_built && !_stale && !HasChangedOnDisk())
        {
            return;
        }

        lock (_gate)
        {
            if (_built && !_stale && !HasChangedOnDisk())
            {
                return;
            }

            Rebuild();
        }
    }

    /// <summary>
    /// Fallback for when the watcher could not start: compare what is on disk against what was
    /// indexed. Stats each file rather than reading it, so it stays much cheaper than a rebuild.
    /// </summary>
    private bool HasChangedOnDisk()
    {
        if (_watcher is not null)
        {
            return false;
        }

        try
        {
            var onDisk = EnumerateNotes()
                .ToDictionary(file => file, File.GetLastWriteTimeUtc, StringComparer.Ordinal);

            var indexed = _entries;

            return onDisk.Count != indexed.Count
                || indexed.Any(entry =>
                    !onDisk.TryGetValue(
                        Path.Combine(_rootPath, entry.RelativePath),
                        out var lastWrite
                    ) || lastWrite != entry.LastWriteUtc
                );
        }
        catch (DirectoryNotFoundException)
        {
            return false;
        }
    }

    private void Rebuild()
    {
        var entries = new List<NoteIndexEntry>();

        foreach (var filePath in EnumerateNotes())
        {
            try
            {
                entries.Add(BuildEntry(filePath));
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                // One unreadable note should not cost us the whole index.
                _logger.LogWarning(e, "Skipping unreadable note {FilePath}", filePath);
            }
        }

        _entries = entries;
        _built = true;
        _stale = false;

        _logger.LogDebug("Indexed {NoteCount} notes from {RootPath}", entries.Count, _rootPath);
    }

    private NoteIndexEntry BuildEntry(string filePath)
    {
        var (tags, body) = MarkdownNote.Parse(File.ReadAllText(filePath));

        return new NoteIndexEntry(
            RelativePath: Path.GetRelativePath(_rootPath, filePath),
            FileName: Path.GetFileNameWithoutExtension(filePath),
            Tags: tags.ToHashSet(StringComparer.OrdinalIgnoreCase),
            BodyTokens: Tokenize(body),
            LastWriteUtc: File.GetLastWriteTimeUtc(filePath)
        );
    }

    private IEnumerable<string> EnumerateNotes()
    {
        if (!Directory.Exists(_rootPath))
        {
            return [];
        }

        return Directory.EnumerateFiles(_rootPath, "*.md", SearchOption.AllDirectories);
    }

    public static IReadOnlyDictionary<string, int> Tokenize(string text)
    {
        var tokens = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        foreach (var match in TokenPattern.EnumerateMatches(text))
        {
            var token = text.Substring(match.Index, match.Length);

            if (token.Length < MinTokenLength)
            {
                continue;
            }

            tokens[token] = tokens.GetValueOrDefault(token) + 1;
        }

        return tokens;
    }

    private void StartWatching()
    {
        try
        {
            _watcher = new FileSystemWatcher(_rootPath, "*.md")
            {
                IncludeSubdirectories = true,
                NotifyFilter =
                    NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.DirectoryName,
            };

            _watcher.Created += OnVaultChanged;
            _watcher.Changed += OnVaultChanged;
            _watcher.Deleted += OnVaultChanged;
            _watcher.Renamed += OnVaultChanged;
            _watcher.Error += OnWatcherError;
            _watcher.EnableRaisingEvents = true;
        }
        catch (Exception e) when (e is ArgumentException or IOException or PlatformNotSupportedException)
        {
            // Network shares and some synced folders refuse to be watched. Searching still works;
            // it just falls back to comparing timestamps.
            _logger.LogWarning(
                e,
                "Could not watch {RootPath} for changes; falling back to timestamp checks",
                _rootPath
            );
            DisposeWatcher();
        }
    }

    private void OnVaultChanged(object sender, FileSystemEventArgs e) => _stale = true;

    private void OnWatcherError(object sender, ErrorEventArgs e)
    {
        _logger.LogWarning(
            e.GetException(),
            "Vault watcher failed; falling back to timestamp checks"
        );

        DisposeWatcher();
        _stale = true;
    }

    private void DisposeWatcher()
    {
        _watcher?.Dispose();
        _watcher = null;
    }

    public void Dispose() => DisposeWatcher();
}
