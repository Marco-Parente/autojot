using System.Diagnostics;
using System.Text.RegularExpressions;

namespace Core.Shared;

public static class AutoJotDiagnostics
{
    public const string ActivitySourceName = "AutoJot";

    public static readonly ActivitySource ActivitySource = new(ActivitySourceName);

    /// <summary>
    /// Telegram puts the bot token straight into the request path
    /// (<c>/bot&lt;token&gt;/sendMessage</c>), so a raw request URL must never reach a span
    /// attribute, a log line, or an exception message.
    /// </summary>
    private static readonly Regex TokenInPath = new(
        @"^(?<prefix>(?:/file)?/bot)[^/]+",
        RegexOptions.Compiled
    );

    public static string RedactTelegramToken(Uri uri)
    {
        var path = TokenInPath.Replace(uri.AbsolutePath, "${prefix}***");

        return $"{uri.Scheme}://{uri.Authority}{path}";
    }
}
