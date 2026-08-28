using Core.Services.Bot;
using Core.Services.Message;
using Core.Services.UserState;

namespace UnitTest;

public class TelegramMessageServiceTest
{
    private const int TelegramLimit = 4096;

    [Fact]
    public void Chunk_LeavesShortMessagesIntact()
    {
        var chunks = TelegramMessageService.Chunk("a short note").ToList();

        Assert.Equal(["a short note"], chunks);
    }

    [Fact]
    public void Chunk_ReturnsNothingForEmptyInput()
    {
        Assert.Empty(TelegramMessageService.Chunk(string.Empty));
    }

    [Fact]
    public void Chunk_SplitsLongMessagesOnLineBoundaries()
    {
        // 500 lines of 20 characters ≈ 10 KB, comfortably over the limit.
        var lines = Enumerable.Range(0, 500).Select(i => $"line {i,-14}").ToList();
        var message = string.Join("\n", lines);

        var chunks = TelegramMessageService.Chunk(message).ToList();

        Assert.True(chunks.Count > 1);
        Assert.All(chunks, chunk => Assert.True(chunk.Length <= TelegramLimit));

        // No content is lost, and no chunk starts mid-line.
        Assert.Equal(lines, chunks.SelectMany(c => c.Split('\n')).ToList());
    }

    [Fact]
    public void Chunk_HardSplitsTextWithNoLineBreaks()
    {
        var message = new string('x', TelegramLimit * 2 + 10);

        var chunks = TelegramMessageService.Chunk(message).ToList();

        Assert.Equal(3, chunks.Count);
        Assert.All(chunks, chunk => Assert.True(chunk.Length <= TelegramLimit));
        Assert.Equal(message, string.Concat(chunks));
    }

    [Fact]
    public void BuildKeyboard_PutsEachOptionOnItsOwnRow()
    {
        var menu = new MenuMessage
        {
            FileOptions = [new MenuOption { Name = "recipes/cake.md", Action = BotAction.UpdateNote }],
            ExtraOptions =
            [
                new MenuOption { Name = "Create new note", Action = BotAction.CreateNewNote },
                new MenuOption { Name = "None", Action = BotAction.Finish },
            ],
        };

        var rows = TelegramMessageService.BuildKeyboard(menu).InlineKeyboard.ToList();

        // Note paths are too long to sit side by side.
        Assert.Equal(3, rows.Count);
        Assert.All(rows, row => Assert.Single(row));
        Assert.Equal(
            ["recipes/cake.md", "Create new note", "None"],
            rows.Select(r => r.Single().Text)
        );
    }

    [Fact]
    public void BuildKeyboard_CarriesCallbackDataThatResolvesBack()
    {
        var menu = new MenuMessage
        {
            FileOptions = [new MenuOption { Name = "recipes/cake.md", Action = BotAction.UpdateNote }],
            ExtraOptions = [new MenuOption { Name = "None", Action = BotAction.Finish }],
        };

        var buttons = TelegramMessageService
            .BuildKeyboard(menu)
            .InlineKeyboard.SelectMany(row => row)
            .ToList();

        Assert.Equal(BotAction.UpdateNote, menu.Resolve(buttons[0].CallbackData!)?.Option.Action);
        Assert.Equal(BotAction.Finish, menu.Resolve(buttons[1].CallbackData!)?.Option.Action);
    }

    [Fact]
    public void BuildKeyboard_TruncatesLongLabelsKeepingTheFileName()
    {
        var deepPath = string.Join("/", Enumerable.Repeat("a-long-folder-name", 6)) + "/cake.md";
        var menu = new MenuMessage
        {
            FileOptions = [new MenuOption { Name = deepPath, Action = BotAction.UpdateNote }],
        };

        var label = TelegramMessageService
            .BuildKeyboard(menu)
            .InlineKeyboard.Single()
            .Single()
            .Text;

        Assert.True(label.Length <= 60);
        // The tail is the useful half of a note path, so that is the half kept.
        Assert.EndsWith("cake.md", label);
        Assert.StartsWith("…", label);
    }
}
