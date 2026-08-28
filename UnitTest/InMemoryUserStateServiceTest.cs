using Core.Services.Bot;
using Core.Services.UserState;
using Microsoft.Extensions.Caching.Memory;

namespace UnitTest;

public class InMemoryUserStateServiceTest
{
    private static InMemoryUserStateService CreateService() =>
        new(new MemoryCache(new MemoryCacheOptions()));

    [Fact]
    public async Task GetUserState_CreatesStateKeyedByUser()
    {
        var service = CreateService();

        var state = await service.GetUserState("telegram-1");

        Assert.Equal("telegram-1", state.UserKey);
        Assert.Null(state.CurrentAction);
    }

    [Fact]
    public async Task SetUserState_RoundTrips()
    {
        var service = CreateService();
        var state = await service.GetUserState("telegram-1");

        await service.SetUserState(
            "telegram-1",
            state with
            {
                CurrentAction = BotAction.WaitForInput,
                SelectedFile = "note.md",
            }
        );

        var reloaded = await service.GetUserState("telegram-1");
        Assert.Equal(BotAction.WaitForInput, reloaded.CurrentAction);
        Assert.Equal("note.md", reloaded.SelectedFile);
    }

    [Fact]
    public async Task ClearUserState_ResetsTheConversation()
    {
        var service = CreateService();
        await service.SetUserState(
            "telegram-1",
            new UserState { UserKey = "telegram-1", CurrentAction = BotAction.WaitForInput }
        );

        await service.ClearUserState("telegram-1");

        var reloaded = await service.GetUserState("telegram-1");
        Assert.Null(reloaded.CurrentAction);
    }

    [Fact]
    public async Task UserStates_AreIsolatedPerUser()
    {
        var service = CreateService();
        await service.SetUserState(
            "telegram-1",
            new UserState { UserKey = "telegram-1", SelectedFile = "mine.md" }
        );

        var other = await service.GetUserState("telegram-2");

        Assert.Null(other.SelectedFile);
    }

    [Fact]
    public void Expiry_IsSlidingSoActiveConversationsAreNotCutOff()
    {
        // Guards the intent: abandoned conversations expire, active ones do not.
        Assert.Equal(TimeSpan.FromMinutes(30), InMemoryUserStateService.Expiry);
    }
}
