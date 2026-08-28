using Core.Services.Search;
using Core.Shared;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace UnitTest;

public class NoteIndexTest : IDisposable
{
    private readonly string _vault = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
    private readonly List<NoteIndex> _indexes = [];

    public NoteIndexTest()
    {
        Directory.CreateDirectory(_vault);
    }

    public void Dispose()
    {
        foreach (var index in _indexes)
        {
            index.Dispose();
        }

        if (Directory.Exists(_vault))
        {
            Directory.Delete(_vault, true);
        }
    }

    private NoteIndex CreateIndex()
    {
        var index = new NoteIndex(
            Options.Create(new AutoJotOptions { RootPath = _vault }),
            NullLogger<NoteIndex>.Instance
        );

        _indexes.Add(index);

        return index;
    }

    private void WriteNote(string relativePath, string content)
    {
        var fullPath = Path.Combine(_vault, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        File.WriteAllText(fullPath, content);
    }

    private static int ScoreOf(List<MatchResult> results, string relativePath) =>
        results.Single(r => r.RelativeFilePath == relativePath).Score;

    [Fact]
    public void Search_ScoresFileNameAboveTagsAboveBody()
    {
        WriteNote("note1.md", "---\ntags:\n  - keyword\n  - test\n---\nContent 1");
        WriteNote("project-keyword.md", "---\ntags:\n  - other\n---\nContent 2");
        WriteNote("random.md", "No front matter here.");

        var results = CreateIndex().Search(["keyword", "project"]);

        // note1: tag hit only (2). project-keyword: file name hit for both keywords (3 + 3).
        Assert.Equal(2, ScoreOf(results, "note1.md"));
        Assert.Equal(6, ScoreOf(results, "project-keyword.md"));
        Assert.Equal(0, ScoreOf(results, "random.md"));

        Assert.Equal(
            ["project-keyword.md", "note1.md", "random.md"],
            results.Select(r => r.RelativeFilePath)
        );
    }

    [Fact]
    public void Search_FindsNotesByTheirBodyText()
    {
        // The headline gap in the old implementation: a keyword that appears only in the prose
        // scored nothing at all, so the note was unfindable.
        WriteNote(
            "recipes/cake.md",
            "---\ntags:\n  - dessert\n---\nAdd espresso powder to the dry ingredients."
        );

        var results = CreateIndex().Search(["espresso"]);

        Assert.Equal(1, ScoreOf(results, Path.Combine("recipes", "cake.md")));
    }

    [Fact]
    public void Search_CapsWhatRepetitionInTheBodyCanEarn()
    {
        WriteNote("spammy.md", string.Join(" ", Enumerable.Repeat("cake", 40)));
        WriteNote("cake-recipe.md", "A short note.");

        var results = CreateIndex().Search(["cake"]);

        // Without the cap this note would score 40 and bury everything else; capped, forty
        // mentions in the body are worth no more than one mention in a note's name.
        Assert.Equal(3, ScoreOf(results, "spammy.md"));
        Assert.Equal(3, ScoreOf(results, "cake-recipe.md"));
    }

    [Fact]
    public void Search_MatchesTagsAndBodyCaseInsensitively()
    {
        WriteNote("note.md", "---\ntags:\n  - Baking\n---\nPreheat the OVEN first.");

        var results = CreateIndex().Search(["baking", "oven"]);

        Assert.Equal(3, ScoreOf(results, "note.md"));
    }

    [Fact]
    public void Search_ReturnsNothingWithoutKeywords()
    {
        WriteNote("note.md", "content");

        Assert.Empty(CreateIndex().Search([]));
    }

    [Fact]
    public void Search_CoversNestedFolders()
    {
        WriteNote("recipes/desserts/cake.md", "---\ntags:\n  - dessert\n---\nContent");

        var results = CreateIndex().Search(["dessert"]);

        var match = Assert.Single(results);
        Assert.Equal(Path.Combine("recipes", "desserts", "cake.md"), match.RelativeFilePath);
    }

    [Fact]
    public void Invalidate_MakesTheBotsOwnWritesVisibleImmediately()
    {
        var index = CreateIndex();
        Assert.DoesNotContain(index.Search(["cake"]), r => r.Score > 0);

        WriteNote("cake.md", "content");
        index.Invalidate();

        Assert.Equal(3, ScoreOf(index.Search(["cake"]), "cake.md"));
    }

    [Fact]
    public async Task Index_PicksUpNotesEditedOutsideTheBot()
    {
        var index = CreateIndex();
        Assert.Equal(0, index.Count);

        WriteNote("added-in-obsidian.md", "---\ntags:\n  - dessert\n---\nContent");

        // The watcher is asynchronous, so poll rather than assume it has already fired.
        var deadline = DateTime.UtcNow.AddSeconds(10);
        while (index.Count == 0 && DateTime.UtcNow < deadline)
        {
            await Task.Delay(50);
        }

        Assert.Equal(1, index.Count);
        Assert.Equal(2, ScoreOf(index.Search(["dessert"]), "added-in-obsidian.md"));
    }

    [Theory]
    [InlineData("hello world", new[] { "hello", "world" })]
    [InlineData("freezer-friendly notes", new[] { "freezer-friendly", "notes" })]
    // Single characters and punctuation are noise, not search terms.
    [InlineData("a b ## Heading!", new[] { "Heading" })]
    [InlineData("350°F (175°C)", new[] { "350", "175" })]
    public void Tokenize_KeepsWordsWorthSearching(string text, string[] expected)
    {
        Assert.Equal(expected.Order(), NoteIndex.Tokenize(text).Keys.Order());
    }

    [Fact]
    public void Tokenize_CountsRepeats()
    {
        var tokens = NoteIndex.Tokenize("cake CAKE cake bread");

        Assert.Equal(3, tokens["cake"]);
        Assert.Equal(1, tokens["bread"]);
    }
}
