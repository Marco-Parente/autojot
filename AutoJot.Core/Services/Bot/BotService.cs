using System.Text.Json;
using Core.Services.AI;
using Core.Services.Files;
using Core.Services.Message;
using Core.Services.Search;
using Core.Services.UserState;
using Core.Shared;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Core.Services.Bot;

public class BotService : IBotService
{
    private readonly string _rootPath;
    private readonly IMessageService _messageService;
    private readonly IAiService _aiService;
    private readonly IFilesService _fileService;
    private readonly INoteIndex _noteIndex;
    private readonly ILogger<BotService> _logger;
    private readonly IUserStateService _userStateService;
    private readonly UserLocks _userLocks;

    public BotService(
        IMessageService messageService,
        IAiService aiService,
        IFilesService fileService,
        INoteIndex noteIndex,
        ILogger<BotService> logger,
        IOptions<AutoJotOptions> options,
        IUserStateService userStateService,
        UserLocks userLocks
    )
    {
        _messageService = messageService;
        _aiService = aiService;
        _fileService = fileService;
        _noteIndex = noteIndex;
        _logger = logger;
        _userStateService = userStateService;
        _userLocks = userLocks;
        _rootPath = options.Value.RootPath;
    }

    private const string HelpText = """
        AutoJot keeps your Markdown notes for you.

        Send me anything and I will work out whether you are asking about your notes or adding to
        them:

        - A question or a few keywords: I search your notes and show you the best match.
        - A statement, a recipe, anything worth keeping: I file it as a new note, or fold it into
          an existing one.

        Commands:
        /search <keywords> - search, skipping the classification step
        /upsert <text> - save, skipping the classification step
        /cancel - forget what we were doing and start over
        /help - show this message
        """;

    public async Task ReceiveMessage(
        string userKey,
        string message,
        CancellationToken cancellationToken = default
    )
    {
        using var activity = AutoJotDiagnostics.ActivitySource.StartActivity("bot.message");
        using var userLock = await _userLocks.AcquireAsync(userKey, cancellationToken);

        try
        {
            if (await TryHandleCommand(userKey, message, cancellationToken))
            {
                return;
            }

            var userState = await _userStateService.GetUserState(userKey);

            // A menu is open, so the next step is a button tap. Typing here is either a stray
            // message or someone who has not noticed the buttons.
            if (userState.CurrentAction is BotAction.WaitForInput)
            {
                await _messageService.SendMessage(
                    userKey,
                    "Please pick one of the options above, or send /cancel to start over.",
                    cancellationToken
                );
                return;
            }

            userState = userState.AddMessage(
                new TextMessage { TextOrigin = TextOrigin.User, Message = message }
            );

            await RunStateMachine(userState, cancellationToken);
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Error processing received message");
            await NotifyFailure(userKey, cancellationToken);
            await _userStateService.ClearUserState(userKey);
        }
    }

    public async Task ReceiveSelection(
        string userKey,
        string callbackData,
        CancellationToken cancellationToken = default
    )
    {
        using var activity = AutoJotDiagnostics.ActivitySource.StartActivity("bot.selection");
        using var userLock = await _userLocks.AcquireAsync(userKey, cancellationToken);

        try
        {
            var userState = await _userStateService.GetUserState(userKey);
            var selection = userState.CurrentMenu?.Resolve(callbackData);

            // The menu this button belongs to is gone - the conversation moved on, or the state
            // expired. Acting on it would apply the tap to whatever menu is current instead.
            if (selection is null)
            {
                await _messageService.SendMessage(
                    userKey,
                    "That menu has expired. Send your message again.",
                    cancellationToken
                );
                await _userStateService.ClearUserState(userKey);
                return;
            }

            userState = userState with
            {
                SelectedFile = selection.IsFileOption
                    ? selection.Option.Name
                    : userState.SelectedFile,
                CurrentAction = selection.Option.Action,
            };

            await RunStateMachine(userState, cancellationToken);
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Error processing menu selection");
            await NotifyFailure(userKey, cancellationToken);
            await _userStateService.ClearUserState(userKey);
        }
    }

    private async Task RunStateMachine(
        UserState.UserState userState,
        CancellationToken cancellationToken
    )
    {
        do
        {
            switch (userState)
            {
                case { CurrentAction: BotAction.DisplayNoteContent }:
                    userState = await HandleDisplayNoteContent(userState, cancellationToken);
                    break;
                case { CurrentAction: BotAction.CreateNewNote }:
                    userState = await HandleCreateNote(userState, cancellationToken);
                    break;
                case { CurrentAction: BotAction.UpdateNote }:
                    userState = await HandleUpdateNote(userState, cancellationToken);
                    break;
                case { CurrentAction: BotAction.Finish }:
                    await _userStateService.ClearUserState(userState.UserKey);
                    return;
                default:
                    userState = await GetAction(userState, cancellationToken);
                    break;
            }

            await _userStateService.SetUserState(userState.UserKey, userState);
        } while (userState.CurrentAction is not BotAction.WaitForInput);
    }

    private async Task<bool> TryHandleCommand(
        string userKey,
        string message,
        CancellationToken cancellationToken
    )
    {
        switch (message.Trim().Split(' ')[0].ToLowerInvariant())
        {
            case "/cancel":
                await _userStateService.ClearUserState(userKey);
                await _messageService.SendMessage(
                    userKey,
                    "Cancelled. Send me anything to start again.",
                    cancellationToken
                );
                return true;
            case "/start":
            case "/help":
                await _messageService.SendMessage(userKey, HelpText, cancellationToken);
                return true;
            default:
                return false;
        }
    }

    private async Task NotifyFailure(string userKey, CancellationToken cancellationToken)
    {
        try
        {
            await _messageService.SendMessage(
                userKey,
                "Something went wrong handling that message. Please try again.",
                cancellationToken
            );
        }
        catch (Exception e)
        {
            // The failure path must not throw on top of the failure it is reporting.
            _logger.LogError(e, "Failed to notify the user about an earlier error");
        }
    }

    private async Task<UserState.UserState> GetAction(
        UserState.UserState userState,
        CancellationToken cancellationToken
    )
    {
        var message =
            userState
                .Messages.OfType<TextMessage>()
                .LastOrDefault(x => x.TextOrigin == TextOrigin.User)
                ?.Message ?? throw new Exception("No user input to act on");

        if (message.StartsWith("/search"))
        {
            return await HandleQuery(
                userState,
                message["/search".Length..].Trim().Split(" ").ToList(),
                cancellationToken
            );
        }

        if (message.StartsWith("/upsert"))
        {
            return await HandleUpsert(
                userState,
                message["/upsert".Length..].Trim(),
                cancellationToken
            );
        }

        var classification = await _aiService.ClassifyMessage(message, cancellationToken);

        userState = userState with { FileKeywords = classification.Keywords };

        _logger.LogDebug(
            "Received message: {Message}, classification: {Classification}",
            message,
            JsonSerializer.Serialize(classification)
        );

        if (classification.ClassificationType is ClassificationType.Query)
        {
            return await HandleQuery(userState, classification.Keywords, cancellationToken);
        }

        if (classification.ClassificationType is ClassificationType.Upsert)
        {
            return await HandleUpsert(userState, message, cancellationToken);
        }

        await _messageService.SendMessage(
            userState.UserKey,
            "Message couldn't be classified. Message received: " + message,
            cancellationToken
        );
        return userState with { CurrentAction = BotAction.Finish };
    }

    private async Task<UserState.UserState> HandleUpsert(
        UserState.UserState userState,
        string input,
        CancellationToken cancellationToken
    )
    {
        string? fileName = null;

        using var reader = new StringReader(input);

        var firstLine = await reader.ReadLineAsync();
        if (
            !string.IsNullOrEmpty(firstLine)
            && firstLine.IndexOfAny(Path.GetInvalidFileNameChars()) == -1
            && firstLine.EndsWith(".md")
        )
        {
            fileName = firstLine;
            input = input[fileName.Length..];
        }

        if (!string.IsNullOrEmpty(fileName) && _fileService.FileExists(_rootPath, fileName))
        {
            return userState with { SelectedFile = fileName, CurrentAction = BotAction.UpdateNote };
        }

        var keyWords =
            userState.FileKeywords.Count > 0
                ? userState.FileKeywords
                : await _aiService.GetKeyWords(input, cancellationToken);

        // Only scoring matches are worth offering. Without this filter a vault where nothing
        // matched comes back with every note tied at score 0, and the whole vault gets listed.
        var matches = _noteIndex.Search(keyWords).Where(x => x.Score > 0).ToList();

        if (!string.IsNullOrEmpty(fileName) || matches.Count == 0)
        {
            return userState with
            {
                SelectedFile = fileName,
                CurrentAction = BotAction.CreateNewNote,
            };
        }

        userState = await SendMessage(
            userState,
            new MenuMessage
            {
                HeaderText = "Upsert a note... Multiple options found:",
                FileOptions = matches
                    .Select(x => new MenuOption
                    {
                        Name = x.RelativeFilePath,
                        Action = BotAction.UpdateNote,
                    })
                    .ToList(),
                ExtraOptions =
                [
                    new MenuOption { Name = "Create new note", Action = BotAction.CreateNewNote },
                    new MenuOption { Name = "None", Action = BotAction.Finish },
                ],
            },
            cancellationToken
        );

        return userState with
        {
            CurrentAction = BotAction.WaitForInput,
        };
    }

    private async Task<UserState.UserState> HandleQuery(
        UserState.UserState userState,
        List<string> classificationKeywords,
        CancellationToken cancellationToken
    )
    {
        var matchResults = _noteIndex
            .Search(classificationKeywords)
            .Where(x => x.Score > 0)
            .ToList();

        if (matchResults.Count == 0)
        {
            await _messageService.SendMessage(
                userState.UserKey,
                "No matches found",
                cancellationToken
            );
            return userState with { CurrentAction = BotAction.Finish };
        }

        var topScore = matchResults.Max(x => x.Score);
        var bestMatches = matchResults.Where(x => x.Score == topScore).ToList();

        if (bestMatches.Count == 1)
        {
            var bestMatch = bestMatches.First();

            return userState with
            {
                CurrentAction = BotAction.DisplayNoteContent,
                SelectedFile = bestMatch.RelativeFilePath,
            };
        }

        userState = await SendMessage(
            userState,
            new MenuMessage
            {
                HeaderText = "Querying notes... Multiple options found:",
                FileOptions = bestMatches
                    .Select(x => new MenuOption
                    {
                        Name = x.RelativeFilePath,
                        Action = BotAction.DisplayNoteContent,
                    })
                    .ToList(),
                ExtraOptions = [new MenuOption { Name = "None", Action = BotAction.Finish }],
            },
            cancellationToken
        );

        return userState with
        {
            CurrentAction = BotAction.WaitForInput,
        };
    }

    private async Task<UserState.UserState> HandleDisplayNoteContent(
        UserState.UserState userState,
        CancellationToken cancellationToken
    )
    {
        userState = await SendMessage(
            userState,
            new TextMessage
            {
                Message =
                    $"File: \"{userState.SelectedFile}\"\n\n"
                    + $"{_fileService.GetFileContent(_rootPath, userState.SelectedFile!)}",
            },
            cancellationToken
        );

        return userState with
        {
            CurrentAction = BotAction.Finish,
        };
    }

    private async Task<UserState.UserState> HandleUpdateNote(
        UserState.UserState userState,
        CancellationToken cancellationToken
    )
    {
        if (!string.IsNullOrEmpty(userState.FileContent))
        {
            _fileService.UpsertFile(_rootPath, userState.SelectedFile!, userState.FileContent);
            _noteIndex.Invalidate();
            return userState with { CurrentAction = BotAction.Finish };
        }

        var userInput =
            userState
                .Messages.OfType<TextMessage>()
                .FirstOrDefault(x => x.TextOrigin == TextOrigin.User)
            ?? throw new Exception("User input not found while trying to update note");

        var originalFile = _fileService.GetFileContent(_rootPath, userState.SelectedFile!);

        var updatedNote = await _aiService.UpdateNote(userInput.Message, originalFile, cancellationToken);

        userState = await SendMessage(
            userState,
            new MenuMessage
            {
                HeaderText = $"""
                Updated note:
                {updatedNote.Content}
                """,
                ExtraOptions =
                [
                    new MenuOption { Name = "Confirm update", Action = BotAction.UpdateNote },
                    new MenuOption { Name = "Discard changes", Action = BotAction.Finish },
                ],
            },
            cancellationToken
        ) with
        {
            CurrentAction = BotAction.WaitForInput,
            FileContent = updatedNote.Content,
        };

        return userState;
    }

    private async Task<UserState.UserState> HandleCreateNote(
        UserState.UserState userState,
        CancellationToken cancellationToken
    )
    {
        var userInput =
            userState
                .Messages.OfType<TextMessage>()
                .FirstOrDefault(x => x.TextOrigin == TextOrigin.User)
            ?? throw new Exception("User input not found while trying to create note");

        var newNote = await _aiService.CreateNote(
            userInput.Message,
            _fileService.GetFolders(_rootPath),
            cancellationToken
        );

        _fileService.UpsertFile(_rootPath, newNote.FilePath, newNote.Content);
        _noteIndex.Invalidate();

        userState = await SendMessage(
            userState,
            new TextMessage
            {
                Message = $"""
                New file created:"{newNote.FilePath}"

                {newNote.Content}
                """,
                TextOrigin = TextOrigin.System,
            },
            cancellationToken
        );

        return userState with
        {
            CurrentAction = BotAction.Finish,
        };
    }

    private async Task<UserState.UserState> SendMessage(
        UserState.UserState userState,
        IChatMessage message,
        CancellationToken cancellationToken
    )
    {
        if (message is MenuMessage menu)
        {
            await _messageService.SendMenu(userState.UserKey, menu, cancellationToken);
        }
        else
        {
            var txtMessage = message.ToString() ?? throw new Exception("Cant send empty message");
            await _messageService.SendMessage(userState.UserKey, txtMessage, cancellationToken);
        }

        return userState with
        {
            Messages = new List<IChatMessage>(userState.Messages) { message },
        };
    }
}
