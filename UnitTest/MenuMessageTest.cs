using System.Text;
using Core.Services.Bot;
using Core.Services.UserState;

namespace UnitTest;

public class MenuMessageTest
{
    private static MenuMessage BuildMenu() =>
        new()
        {
            HeaderText = "Upsert a note... Multiple options found:",
            FileOptions =
            [
                new MenuOption { Name = "recipes/cake.md", Action = BotAction.UpdateNote },
                new MenuOption { Name = "recipes/bread.md", Action = BotAction.UpdateNote },
            ],
            ExtraOptions =
            [
                new MenuOption { Name = "Create new note", Action = BotAction.CreateNewNote },
                new MenuOption { Name = "None", Action = BotAction.Finish },
            ],
        };

    [Fact]
    public void AllOptions_KeepsFilesBeforeExtras()
    {
        var menu = BuildMenu();

        Assert.Equal(
            ["recipes/cake.md", "recipes/bread.md", "Create new note", "None"],
            menu.AllOptions.Select(o => o.Name)
        );
    }

    [Theory]
    [InlineData(0, "recipes/cake.md", BotAction.UpdateNote, true)]
    [InlineData(1, "recipes/bread.md", BotAction.UpdateNote, true)]
    [InlineData(2, "Create new note", BotAction.CreateNewNote, false)]
    [InlineData(3, "None", BotAction.Finish, false)]
    public void Resolve_MapsCallbackDataBackToItsOption(
        int index,
        string expectedName,
        BotAction expectedAction,
        bool expectedIsFile
    )
    {
        var menu = BuildMenu();

        var selection = menu.Resolve(menu.CallbackDataFor(index));

        Assert.NotNull(selection);
        Assert.Equal(expectedName, selection.Option.Name);
        Assert.Equal(expectedAction, selection.Option.Action);
        Assert.Equal(expectedIsFile, selection.IsFileOption);
    }

    [Fact]
    public void Resolve_RejectsCallbackDataFromAnotherMenu()
    {
        var menu = BuildMenu();
        var supersededMenu = BuildMenu();

        // Same shape, different menu: tapping the old buttons must not act on the new menu.
        Assert.NotEqual(menu.Id, supersededMenu.Id);
        Assert.Null(menu.Resolve(supersededMenu.CallbackDataFor(0)));
    }

    [Theory]
    [InlineData("")]
    [InlineData("garbage")]
    [InlineData(":0")]
    [InlineData("notamenu:0")]
    public void Resolve_RejectsMalformedCallbackData(string callbackData)
    {
        Assert.Null(BuildMenu().Resolve(callbackData));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(4)]
    [InlineData(99)]
    public void Resolve_RejectsOutOfRangeIndexes(int index)
    {
        var menu = BuildMenu();

        Assert.Null(menu.Resolve($"{menu.Id}:{index}"));
    }

    [Fact]
    public void CallbackData_FitsTelegramsLimit()
    {
        var menu = new MenuMessage
        {
            FileOptions =
            [
                new MenuOption
                {
                    // Note paths blow past the 64 byte cap, which is why buttons carry an index.
                    Name = string.Join("/", Enumerable.Repeat("a-very-long-folder-name", 8)) + ".md",
                    Action = BotAction.UpdateNote,
                },
            ],
        };

        Assert.True(Encoding.UTF8.GetByteCount(menu.CallbackDataFor(0)) <= 64);
    }

    [Fact]
    public void ToString_NoLongerNumbersTheOptions()
    {
        // Options are rendered as buttons now, so the text must not repeat them as a numbered list.
        var text = BuildMenu().ToString();

        Assert.Contains("Upsert a note", text);
        Assert.DoesNotContain("1 - ", text);
        Assert.DoesNotContain("recipes/cake.md", text);
    }
}
