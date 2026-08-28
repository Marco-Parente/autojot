using Core.Shared;

namespace UnitTest;

public class AutoJotDiagnosticsTest
{
    private const string Token = "111111111:FAKE-TOKEN-FOR-TESTS-ONLY-AAAAAAAAAAA";

    [Theory]
    [InlineData("https://api.telegram.org/bot{0}/sendMessage", "https://api.telegram.org/bot***/sendMessage")]
    [InlineData("https://api.telegram.org/bot{0}/getUpdates", "https://api.telegram.org/bot***/getUpdates")]
    [InlineData("https://api.telegram.org/file/bot{0}/photos/a.jpg", "https://api.telegram.org/file/bot***/photos/a.jpg")]
    public void RedactTelegramToken_RemovesTheTokenFromTheUrl(string template, string expected)
    {
        var uri = new Uri(string.Format(template, Token));

        var redacted = AutoJotDiagnostics.RedactTelegramToken(uri);

        Assert.Equal(expected, redacted);
        Assert.DoesNotContain(Token, redacted);
    }

    [Fact]
    public void RedactTelegramToken_LeavesUnrelatedUrlsAlone()
    {
        var uri = new Uri("http://localhost:11434/api/generate");

        Assert.Equal("http://localhost:11434/api/generate", AutoJotDiagnostics.RedactTelegramToken(uri));
    }
}
