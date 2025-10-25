using System.Text.Json;
using Core.Services.AI;
using Core.Services.Files;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Core.Services.Bot;

public class BotService : IBotService
{
    private readonly string _rootPath;
    private readonly IMessageService _messageService;
    private readonly IAiService _aiService;
    private readonly IFilesService _fileService;
    private readonly ILogger<BotService> _logger;
    private readonly IUserStateService _userStateService;

    public BotService(
        IMessageService messageService,
        IAiService aiService,
        IFilesService fileService,
        ILogger<BotService> logger,
        IConfiguration configuration,
        IUserStateService userStateService
    )
    {
        _messageService = messageService;
        _aiService = aiService;
        _fileService = fileService;
        _logger = logger;
        _userStateService = userStateService;
        _rootPath =
            configuration.GetValue<string?>("AutoJot:RootPath")
            ?? throw new NullReferenceException("RootPath configuration is missing");
    }

    public async Task ReceiveMessage(string userKey, string message)
    {
        try
        {
            var userState = await _userStateService.GetUserState(userKey);
            userState = userState.AddMessage(
                new TextMessage { TextOrigin = TextOrigin.User, Message = message }
            );

            do
            {
                switch (userState)
                {
                    case { CurrentAction: BotAction.DisplayNoteContent }:
                        userState = await HandleDisplayNoteContent(userState);
                        break;
                    case { CurrentAction: BotAction.CreateNewNote }:
                        userState = await HandleCreateNote(userState);
                        break;
                    case { CurrentAction: BotAction.UpdateNote }:
                        userState = await HandleUpdateNote(userState);
                        break;
                    case { CurrentAction: BotAction.WaitForInput }:
                        userState = await TryNextAction(userState, message);
                        break;
                    case { CurrentAction: BotAction.Finish }:
                        await _userStateService.ClearUserState(userKey);
                        return;
                    default:
                        userState = await GetAction(userState, message);
                        break;
                }

                await _userStateService.SetUserState(userState.UserKey, userState);
            } while (userState.CurrentAction is not BotAction.WaitForInput);
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Error processing received message");
            await _userStateService.ClearUserState(userKey);
        }
    }

    private async Task<UserState> GetAction(UserState userState, string message)
    {
        if (message.StartsWith("/search"))
        {
            return await HandleQuery(
                userState,
                message["/search".Length..].Trim().Split(" ").ToList()
            );
        }

        if (message.StartsWith("/upsert"))
        {
            return await HandleUpsert(userState, message["/upsert".Length..].Trim());
        }

        if (message.StartsWith("/test"))
        {
            await _aiService.Test();
        }

        var classification = await _aiService.ClassifyMessage(message);

        userState = userState with { FileKeywords = classification.Keywords };

        _logger.LogDebug(
            "Received message: {Message}, classification: {Classification}",
            message,
            JsonSerializer.Serialize(classification)
        );

        if (classification.ClassificationType is ClassificationType.Query)
        {
            return await HandleQuery(userState, classification.Keywords);
        }

        if (classification.ClassificationType is ClassificationType.Upsert)
        {
            return await HandleUpsert(userState, message);
        }

        await _messageService.SendMessage(
            userState.UserKey,
            "Message couldn't be classified. Message received: " + message
        );
        return userState with { CurrentAction = BotAction.Finish };
    }

    private async Task<UserState> TryNextAction(UserState userState, string message)
    {
        var menu =
            userState.Messages.OfType<MenuMessage>().LastOrDefault()
            ?? throw new Exception("Menu message not found");

        var selectedOption = menu.GetActionFromOption(message);

        if (selectedOption is null)
        {
            await _messageService.SendMessage(userState.UserKey, "Please, select a valid option");
            return userState;
        }

        return userState with
        {
            SelectedFile = menu.FileOptions.Contains(selectedOption) ? selectedOption.Name : null,
            CurrentAction = selectedOption.Action,
        };
    }

    private async Task<UserState> HandleUpsert(UserState userState, string input)
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
                : await _aiService.GetKeyWords(input);

        var matches = _fileService.GetMatchResults(_rootPath, keyWords);
        if (matches.Any(x => x.Score > 0))
        {
            matches = matches.Where(x => x.Score > 0).ToList();
        }

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
            }
        );

        return userState with
        {
            CurrentAction = BotAction.WaitForInput,
        };
    }

    private async Task<UserState> HandleQuery(
        UserState userState,
        List<string> classificationKeywords
    )
    {
        var matchResults = _fileService.GetMatchResults(_rootPath, classificationKeywords);
        var bestMatches = matchResults
            .Where(x => x.Score == matchResults.MaxBy(match => match.Score)?.Score)
            .ToList();

        if (matchResults.Count == 0)
        {
            await _messageService.SendMessage(userState.UserKey, "No matches found");
            return userState with { CurrentAction = BotAction.Finish };
        }

        if (bestMatches.Count == 1)
        {
            var bestMatch = bestMatches.First();

            return userState with
            {
                CurrentAction = BotAction.DisplayNoteContent,
                SelectedFile = bestMatch.RelativeFilePath,
                FileContent = _fileService.GetFileContent(_rootPath, bestMatch.RelativeFilePath),
            };
        }

        userState = await SendMessage(
            userState,
            new MenuMessage
            {
                FileOptions = bestMatches
                    .Select(x => new MenuOption
                    {
                        Name = x.RelativeFilePath,
                        Action = BotAction.DisplayNoteContent,
                    })
                    .ToList(),
                ExtraOptions = [new MenuOption { Name = "None", Action = BotAction.Finish }],
            }
        );

        return userState with
        {
            CurrentAction = BotAction.WaitForInput,
        };
    }

    private async Task<UserState> HandleDisplayNoteContent(UserState userState)
    {
        userState = await SendMessage(
            userState,
            new TextMessage
            {
                Message =
                    $"File: \"{userState.SelectedFile}\"\n\n"
                    + $"{_fileService.GetFileContent(_rootPath, userState.SelectedFile!)}",
            }
        );

        return userState with
        {
            CurrentAction = BotAction.Finish,
        };
    }

    private async Task<UserState> HandleUpdateNote(UserState userState)
    {
        var userInput =
            userState
                .Messages.OfType<TextMessage>()
                .FirstOrDefault(x => x.TextOrigin == TextOrigin.User)
            ?? throw new Exception("User input not found while trying to create note");

        var updatedNote = await _aiService.UpdateNote(
            userInput.Message,
            _fileService.GetFileContent(_rootPath, userState.SelectedFile!)
        );

        // TODO: Confirm update?
        _fileService.UpsertFile(_rootPath, userState.SelectedFile!, updatedNote.Content);

        // TODO: Use diffplex
        userState = await SendMessage(
            userState,
            new TextMessage
            {
                Message = $"""
                File Updated:"{userState.SelectedFile}"

                {updatedNote.Content}
                """,
                TextOrigin = TextOrigin.System,
            }
        );

        return userState with
        {
            CurrentAction = BotAction.Finish,
        };
    }

    private async Task<UserState> HandleCreateNote(UserState userState)
    {
        var userInput =
            userState
                .Messages.OfType<TextMessage>()
                .FirstOrDefault(x => x.TextOrigin == TextOrigin.User)
            ?? throw new Exception("User input not found while trying to create note");

        var newNote = await _aiService.CreateNote(
            userInput.Message,
            _fileService.GetFolders(_rootPath)
        );

        _fileService.UpsertFile(_rootPath, newNote.FilePath, newNote.Content);

        userState = await SendMessage(
            userState,
            new TextMessage
            {
                Message = $"""
                New file created:"{newNote.FilePath}"

                {newNote.Content}
                """,
                TextOrigin = TextOrigin.System,
            }
        );

        return userState with
        {
            CurrentAction = BotAction.Finish,
        };
    }

    private async Task<UserState> SendMessage(UserState userState, IChatMessage message)
    {
        var txtMessage = message.ToString() ?? throw new Exception("Cant send empty message");
        await _messageService.SendMessage(userState.UserKey, txtMessage);

        return userState with
        {
            Messages = new List<IChatMessage>(userState.Messages) { message },
        };
    }
}
